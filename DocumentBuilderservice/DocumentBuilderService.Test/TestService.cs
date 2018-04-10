namespace DocumentBuilderService.Test
{
    using System.Diagnostics.CodeAnalysis;
    using System.IO;
    using System.Net;
    using System.Threading;

    using DocumentBuilderservice;

    using Microsoft.VisualStudio.TestTools.UnitTesting;

    using QueueServer;

    [TestClass]
    public class TestService
    {
        private FileService _fileService;
        private string _currentDir;
        private string _badFilesDir;
        private string _outDir;
        private string _inDir;

        private QueueServer _queueserver;
        private string _outFile1;
        private string _outFile2;
        private string _outFile3;

        [SuppressMessage("StyleCop.CSharp.LayoutRules", "SA1503:CurlyBracketsMustNotBeOmitted", Justification = "Reviewed. Suppression is OK here.")]
        [TestInitialize]
        public void Initialize()
        {
            _inDir = @"C:\MP.ServicesTest";
            _currentDir = Directory.GetCurrentDirectory();
            _outDir = _currentDir;
            _badFilesDir = _currentDir;
            _queueserver = new QueueServer(_outDir);
            _fileService = new FileService(_inDir, _outDir, _badFilesDir, new PdfDocumentBuilder(), "TestService");

            using (var client = new WebClient())
            {
                client.DownloadFile("http://1.bp.blogspot.com/-MD44WcoiWJs/T-u-uhS5vhI/AAAAAAAAEqM/U_137198yeg/s1600/Tiger+3D+Wallpapers+1.jpg", Path.Combine(_inDir, "Image_001.jpg"));
                client.DownloadFile("http://3.bp.blogspot.com/-xELHFxk2K1Y/U04vzF7sjVI/AAAAAAAAcIY/nsYLKp2Y3uw/s1600/Toshiba+Wallpapers+(1).jpg", Path.Combine(_inDir, "Image_002.jpg"));

                client.DownloadFile("http://2.bp.blogspot.com/-W2uGp9ckgT4/U1YV39k_III/AAAAAAAAcOM/o2GEy-Krb0A/s1600/Lake+Powell+Wallpapers+%25281%2529.jpg", Path.Combine(_inDir, "Image_005.jpg"));

                _outFile1 = Path.Combine(_outDir, "Document0.pdf");
                _outFile2 = Path.Combine(_outDir, "Document1.pdf");
                _outFile3 = Path.Combine(_outDir, "Document2.pdf");
                DeleteOutFiles();
            }
        }

        [TestCleanup]
        public void Cleanup()
        {
            if (Directory.Exists(_inDir))
            { 
                Directory.Delete(_inDir, true);
            }

            DeleteOutFiles();
        }

        [TestMethod]
        public void TestWorkProcessFinishedIfInerrupded()
        {
          _fileService.Start();
            Thread.Sleep(10);
            _fileService.Stop();
            Assert.IsTrue(File.Exists(_outFile1));
        }

        [TestMethod]
        public void TestDocumentSequenceCreated()
        {
            _fileService.Start();
            Thread.Sleep(10000);
            _fileService.Stop();
            Assert.IsTrue(File.Exists(_outFile1));
            Assert.IsTrue(File.Exists(_outFile2));
        }

        [TestMethod]
        public void TestTimeOutElapsed()
        {
            _fileService.Start();
            Thread.Sleep(3000);
            using (var client = new WebClient())
            {
                client.DownloadFile("http://1.bp.blogspot.com/-MO5Siltacrg/UzQ7sD_O7oI/AAAAAAAAb2o/BONHWK0q8po/s1600/South+Sister+Sparks+Lake+Wallpapers+(2).jpg", Path.Combine(_inDir, "Image_001.jpg"));
            }

            Thread.Sleep(7000);
            _fileService.Stop();
            Assert.IsTrue(File.Exists(_outFile1));
            Assert.IsTrue(File.Exists(_outFile2));
            Assert.IsTrue(File.Exists(_outFile3));
        }

        [TestMethod]
        public void TestFileBusy()
        {
            var busyFile = Path.Combine(_inDir, "Image_005.jpg");
            var file = File.Open(busyFile, FileMode.Open, FileAccess.Read, FileShare.None);

            _fileService.Start();
            Thread.Sleep(10000);
            _fileService.Stop();
            Assert.IsTrue(File.Exists(Path.Combine(busyFile)));
            file.Close();
            File.Delete(busyFile);
        }

        [TestMethod]
        public void TestBadFile()
        {
            using (var client = new WebClient())
            {
                client.DownloadFile("https://www.google.com/url?sa=i&rct=j&q=&esrc=s&source=images&cd=&cad=rja&uact=8&ved=2ahUKEwjBjanTzYzaAhUBMZoKHd-RA3IQjRx6BAgAEAU&url=https%3A%2F%2Fpixabay.com%2Fen%2Fimage-editing-ebv-unleashed-101040%2F&psig=AOvVaw0hVPmCizN3qyf0ZOsUYZFU&ust=1522075755423892", Path.Combine(_inDir, "Image_003.jpg"));
            }

            _fileService.Start();
            Thread.Sleep(10000);
            _fileService.Stop();
            var badFile = Path.Combine(_badFilesDir + "Image_003.jpg");
            Assert.IsFalse(File.Exists(badFile));
            Assert.IsFalse(File.Exists(_outFile1));
            Assert.IsTrue(File.Exists(_outFile2));
        }

        [TestMethod]
        public void TestAzureFileSend()
        {
            _queueserver.Start();
            _fileService.Start();
            Thread.Sleep(17000);
            _fileService.Stop();
            var ourfile1 = Path.Combine(_outDir + @"\AzureDocument0.pdf");
            var ourfile2 = Path.Combine(_outDir + @"\AzureDocument1.pdf");
            Assert.IsTrue(File.Exists(ourfile1));
            Assert.IsTrue(File.Exists(ourfile2));
        }

        [TestMethod]
        public void TestAzureBroadcastMessage()
        {
            _queueserver.Start();
            _fileService.Start();
            _queueserver.SendBroadcastMessage("Update Status", null);
            Thread.Sleep(17000);
        }

        private void DeleteOutFiles()
        {
            if (File.Exists(_outFile1))
            {
                File.Delete(_outFile1);
            }

            if (File.Exists(_outFile2))
            {
                File.Delete(_outFile2);
            }

            if (File.Exists(_outFile3))
            {
                File.Delete(_outFile2);
            }
        }
    }
}
