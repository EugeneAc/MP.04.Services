namespace DocumentBuilderservice
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.IO;
    using System.Linq;
    using System.Text.RegularExpressions;
    using System.Threading;
    using System.Threading.Tasks;

    using NLog;

    public class FileService
    {
        private ManualResetEvent _stopWorkEvent;
        private Task _workTask;
        private Task _sendStatusTask;
        private Task _changeSettingsTask;
        private string _searchDir;
        private string _outDir;
        private string _badFilesDir;
        private PdfDocumentBuilder _documentBuilder;
        private Logger _logger;

        private string _currentStatus;
        private string _serviceName;

        private AzureQueueClient _azureQueueClient;

        public int _newFileWaitTimeout { get; private set; }

        public FileService(string searchDir, PdfDocumentBuilder documentBuilder, string uniqueServiceName)
            : this(
                searchDir,
                Path.GetDirectoryName(Process.GetCurrentProcess().MainModule.FileName),
                documentBuilder,
                uniqueServiceName)
        {
        }

        public FileService(string searchDir, string outDir, PdfDocumentBuilder documentBuilder, string uniqueServiceName)
            : this(
                searchDir,
                outDir,
                Path.GetDirectoryName(Process.GetCurrentProcess().MainModule.FileName),
                documentBuilder,
                uniqueServiceName)
        {
        }

        public FileService(string searchDir, string outDir, string badFilesDir, PdfDocumentBuilder documentBuilder, string uniqueServiceName)
        {
            _searchDir = searchDir;
            _documentBuilder = documentBuilder;
            _outDir = outDir;
            _badFilesDir = badFilesDir;
            _logger = LogManager.GetCurrentClassLogger();
            if (!Directory.Exists(_searchDir))
            {
                Directory.CreateDirectory(_searchDir);
            }

            _stopWorkEvent = new ManualResetEvent(false);
            _newFileWaitTimeout = 1000;
            _serviceName = uniqueServiceName;
            _azureQueueClient = new AzureQueueClient(_serviceName);
        }

        public void Start()
        {
            _workTask = Task.Factory.StartNew(WorkTask);
            _changeSettingsTask = Task.Factory.StartNew(SettingsChangeTask);
            _sendStatusTask = Task.Factory.StartNew(SendCurrentStatus);
            _logger.Trace("Started");
            _azureQueueClient.SendStatusMessage(_serviceName + " Service started");
        }

        public void Stop()
        {
            _logger.Trace("Stopping");
            _stopWorkEvent.Set();
            _workTask.Wait();
            _logger.Trace("Stopped");
            _azureQueueClient.SendStatusMessage(_serviceName + " Service Stopped");
        }

        private void SendCurrentStatus()
        {
            while (!_stopWorkEvent.WaitOne(TimeSpan.Zero))
            {
                _azureQueueClient.SendStatusMessage(_currentStatus);
                Thread.Sleep(60000);
            }
        }

        private void SettingsChangeTask()
        {
            while (!_stopWorkEvent.WaitOne(TimeSpan.Zero))
            {
                var settings = _azureQueueClient.ReceiveNewSettings();
                foreach (var set in settings)
                {
                    if (set.Key == "Timeout")
                    {
                        _newFileWaitTimeout = set.Value;
                    }

                    if ((set.Key == "StatusUpdate") && (set.Value == 1))
                    {
                        _azureQueueClient.SendStatusMessage(_currentStatus);
                    }
                }

                Thread.Sleep(100);
            }
        }

        private void WorkTask()
        {
            int outputCounter = 0;
            while (!_stopWorkEvent.WaitOne(TimeSpan.Zero))
            {
                var sequence = GetFileSequence();
                if (sequence.Count > 0)
                {
                    _currentStatus = _serviceName + " Processing new files";
                    if (!Directory.Exists(_outDir))
                    {
                        Directory.CreateDirectory(_outDir);
                    }

                    try
                    {
                        var doucument = _documentBuilder.BuildDocument(sequence);
                        var filePath = Path.Combine(_outDir + @"\" + "Document" + outputCounter + ".pdf");
                        _documentBuilder.SaveFile(doucument, filePath);
                        _azureQueueClient.SendFile(filePath);
                    }
                    catch (Exception)
                    {
                        if (!Directory.Exists(_badFilesDir))
                        {
                            Directory.CreateDirectory(_badFilesDir);
                        }

                        foreach (var file in sequence)
                        {
                            var outfile = Path.Combine(_badFilesDir + @"\" + Path.GetFileName(file));
                            if (File.Exists(outfile))
                            {
                                File.Delete(outfile);
                            }

                            File.Move(file, outfile);
                        }
                    }

                    foreach (var file in sequence)
                    {
                        File.Delete(file);
                    }

                    outputCounter++;
                }

                _currentStatus = _serviceName + " Idle" + @" Current Settings: {Timeout=" + _newFileWaitTimeout + "}";
                Thread.Sleep(1000);
            }
        }

        private List<string> GetFileSequence()
        {
            int filecounter = -1;
            string pattern = @"\d+";
            Regex regex = new Regex(pattern);
            List<string> sequence = new List<string>();
            int trycount = 0;

            while (trycount < 5)
            {
                foreach (var file in Directory.EnumerateFiles(_searchDir).OrderBy(f => f.ToString()))
                {
                    if (regex.IsMatch(file))
                    {
                        var filenumber = Convert.ToInt32(regex.Match(file).ToString());
                        if ((filecounter < 0) || (filenumber == filecounter + 1))
                        {
                            sequence.Add(file);
                            filecounter = filenumber;
                        }
                    }
                }

                Thread.Sleep(_newFileWaitTimeout);
                trycount++;
            }

            return sequence;
        }
    }
}
