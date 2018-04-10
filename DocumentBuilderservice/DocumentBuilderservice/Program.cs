namespace DocumentBuilderservice
{
    using Topshelf;

    public class Program
    {
        public static void Main(string[] args)
        {
            HostFactory.Run(
                      hostConf => hostConf.Service<FileService>(
                          s =>
                          {
                              s.ConstructUsing(() => new FileService(@"C:\MP.ServicesInDir", @"C:\MP.ServisesOutDir", @"C:\MP.ServisesBadSequencesDir", new PdfDocumentBuilder()));
                              s.WhenStarted(serv => serv.Start());
                              s.WhenStopped(serv => serv.Stop());
                        }).UseNLog());
        }
    }
}
