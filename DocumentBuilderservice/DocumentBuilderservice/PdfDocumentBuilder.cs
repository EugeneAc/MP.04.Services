namespace DocumentBuilderservice
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Threading;

    using MigraDoc.DocumentObjectModel;
    using MigraDoc.Rendering;

    using NLog;

    using PdfSharp.Pdf;

    public class PdfDocumentBuilder
    {
        private Logger _logger;

        public PdfDocument BuildDocument(IEnumerable<string> files)
        {
            _logger = LogManager.GetCurrentClassLogger();
            var document = new Document();
            var section = document.AddSection();
            var iterList = files.ToList();
            for (int i = 0; i < iterList.Count(); i++)
            {
                if (TryOpen(iterList[i], 3))
                {
                    var img = section.AddImage(iterList[i]);
                    img.LockAspectRatio = true;
                    img.Left = -70;
                    if (img.Height > img.Width)
                    {
                        img.Height = document.DefaultPageSetup.PageHeight;
                    }
                    else
                    { 
                        img.Width = document.DefaultPageSetup.PageWidth;
                    }

                    if (i < files.Count() - 1)
                    { 
                        section.AddPageBreak();
                    }
                }
                else
                { 
                    iterList.Remove(iterList[i]);
                }
            }

            var render = new PdfDocumentRenderer();
            render.Document = document;
            try
            {
                render.RenderDocument();
            }
            catch (Exception e)
            {
                _logger.Error(e.ToString());

                throw;
            }

            return render.PdfDocument;
        }

        public void SaveFile(PdfDocument document, string path)
        {
            document.Save(path);
            Logger logger = LogManager.GetCurrentClassLogger();
            logger.Trace("Document saved to " + path);
        }

        private bool TryOpen(string fileName, int tryCount)
        {
            for (int i = 0; i < tryCount; i++)
            {
                try
                {
                    var file = File.Open(fileName, FileMode.Open, FileAccess.Read, FileShare.None);
                    file.Close();

                    return true;
                }
                catch (IOException e)
                {
                    Thread.Sleep(1000);
                    _logger.Error("Message: " + e.Message + " Inner: " + e.InnerException);
                }
            }

            return false;
        }
    }
}
