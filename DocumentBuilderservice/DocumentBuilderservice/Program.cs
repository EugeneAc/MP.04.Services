namespace DocumentBuilderservice
{
    using System;
    using System.Diagnostics;
    using System.IO;

    using NLog;
    using NLog.Config;
    using NLog.Targets;

    using Topshelf;

    public class Program
    {
        public static void Main(string[] args)
        {
            var currentDir = Path.GetDirectoryName(Process.GetCurrentProcess().MainModule.FileName);

            var conf = new LoggingConfiguration();
            var exceptionFileTarget = new FileTarget()
            {
                Name = "Default",
                FileName = Path.Combine(currentDir ?? throw new InvalidOperationException(), "ExceptionLog.txt"),
                Layout = "${date} ${message} ${onexception:inner=${exception:format=toString}}"
            };

            conf.AddTarget(exceptionFileTarget);
            var rule = new LoggingRule("*", LogLevel.Error, exceptionFileTarget);
            conf.LoggingRules.Add(rule);

            var traceFileTarget = new FileTarget()
            {
                Name = "Trace",
                FileName = Path.Combine(currentDir, "TraceLog.txt"),
                Layout = "${longdate}::${logger}::${message}"
            };
            conf.AddTarget(traceFileTarget);
            var rule1 = new LoggingRule("*", LogLevel.Trace, traceFileTarget);
            conf.LoggingRules.Add(rule1);

            var logFactory = new LogFactory(conf);
            LogManager.Configuration = conf;

            HostFactory.Run(
                      hostConf => hostConf.Service<FileService>(
                          s =>
                          {
                              s.ConstructUsing(() => new FileService(@"C:\MP.ServicesInDir", @"C:\MP.ServisesOutDir", @"C:\MP.ServisesBadSequencesDir", new PdfDocumentBuilder()));
                              s.WhenStarted(serv => serv.Start());
                              s.WhenStopped(serv => serv.Stop());
                        }).UseNLog(logFactory));
        }
    }
}
