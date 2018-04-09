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
        private string _searchDir;
        private string _outDir;
        private string _badFilesDir;
        private PdfDocumentBuilder _documentBuilder;
        private Logger _logger;

        public FileService(string searchDir, PdfDocumentBuilder documentBuilder) 
            : this(
                searchDir,
                Path.GetDirectoryName(Process.GetCurrentProcess().MainModule.FileName),
                documentBuilder)
        {
        }

        public FileService(string searchDir, string outDir, PdfDocumentBuilder documentBuilder)
            : this(
                searchDir,
                outDir,
                Path.GetDirectoryName(Process.GetCurrentProcess().MainModule.FileName),
                documentBuilder)
        {
        }

        public FileService(string searchDir, string outDir, string badFilesDir, PdfDocumentBuilder documentBuilder)
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
        }

        public void Start()
        {
            _workTask = Task.Factory.StartNew(WorkTask);
            _logger.Trace("Started");
        }

        public void Stop()
        {
            _logger.Trace("Stopping");
            _stopWorkEvent.Set();
            _workTask.Wait();
            _logger.Trace("Stopped");
        }

        private void WorkTask()
        {
            int outputCounter = 0;
            while (!_stopWorkEvent.WaitOne(TimeSpan.Zero))
            {
                var sequence = GetFileSequence();
                if (sequence.Count > 0)
                {
                    if (!Directory.Exists(_outDir))
                    {
                        Directory.CreateDirectory(_outDir);
                    }

                    try
                    {
                        var doucument = _documentBuilder.BuildDocument(sequence);
                        _documentBuilder.SaveFile(
                            doucument,
                            Path.Combine(_outDir + @"\" + "Document" + outputCounter + ".pdf"));
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

                trycount++;
                Thread.Sleep(1000);
            }

            return sequence;
        }
    }
}
