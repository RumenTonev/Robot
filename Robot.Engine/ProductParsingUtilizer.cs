using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Robot.Models;
using System.Drawing.Imaging;
using System.Drawing;
using Data;
using System.Xml.Linq;
using System.IO;
using System.Data.Entity;
using HtmlAgilityPack;
using System.Net;
using Data.Migrations;
using Robot.Data;


namespace Robot.Engine
{
    public static class ProductParsingUtilizer
    {
        const string currentMainPath = @"C:\Users\Rumen\Desktop\RobotPics\category\";

        const string currentDestinationMain = @"C:\Users\Rumen\Desktop\ReworsenedPicsReady\worsenedCategories\";
        const string currentCategoriesPath = @"C:\Users\Rumen\Desktop\CheckUp\CheckUpOkFinal\catalogs\Robot-catalog\static\default\images\categories";
        const string currentCategoriesNewPath = @"C:\Users\Rumen\Desktop\CheckUp\CheckUpOkFinal\catalogs\Robot-catalog\static\default\images\categoriesOK";
        const int imageResolution = 72;

        public static void CorrectCategoryPictureNames()
        {
            string[] picList = Directory.GetFiles(currentCategoriesPath, "*.jpg");
            for (int i = 0; i < picList.Length; i++)
            {
                var newPath = MakeNewPath(picList[i]);
                File.Move(picList[i], newPath);

            }
        }

        private static string MakeNewPath(string currentPath)
        {
            int startind = currentPath.LastIndexOf('-');
            currentPath = currentPath.Remove(startind);
            currentPath = currentPath + ".jpg";
            return currentPath;
        }
        public static void ResizePictures()
        {
            string[] picLargeList = Directory.GetFiles(currentMainPath, "*.jpg");
            for (int i = 0; i < picLargeList.Length; i++)
            {

                LowerImageQuality(picLargeList[i], 220);
            }

        }
        private static void LowerImageQuality(string currentImagePath, int size)
        {
            //start from here to compress the JPEG file
            string[] stringSeparators = new string[] { "\\", };
            string[] result;
            result = currentImagePath.Split(stringSeparators, StringSplitOptions.RemoveEmptyEntries);
            string fName = result[result.Length - 1];
            string newImagePath = currentDestinationMain + fName;
            Bitmap ImgTemp;
            var imageCur = Image.FromFile(currentImagePath);

            ImgTemp = new Bitmap(imageCur, size, size);
            // ImgTemp.SetResolution(imageResolution, imageResolution);
            SaveJPGWithCompressionSetting(ImgTemp, newImagePath, 70L);
        }

        private static ImageCodecInfo GetEncoderInfo(String mimeType)
        {
            int j;
            ImageCodecInfo[] encoders;
            encoders = ImageCodecInfo.GetImageEncoders();
            for (j = 0; j < encoders.Length; ++j)
            {
                if (encoders[j].MimeType == mimeType)
                    return encoders[j];
            }
            return null;
        }
        private static void SaveJPGWithCompressionSetting(Image image, string szFileName, long lCompression)
        {
            EncoderParameters eps = new EncoderParameters(1);
            eps.Param[0] = new EncoderParameter(System.Drawing.Imaging.Encoder.Quality, lCompression);
            ImageCodecInfo ici = GetEncoderInfo("image/jpeg");
            image.Save(szFileName, ici, eps);
            image.Dispose();
        }
        public static void CreateCatalog(RobotContext db, List<Product> products)
        {
            XDocument document = new XDocument(
                new XDeclaration("1.0", "UTF-8", ""), new XElement("catalog",
                     new XAttribute("catalog-id", "Robot-catalog"),
                        new XElement("header")
                        )
                );
            document.Root.Add(new XElement("category", new XAttribute("category-id", "root"),

                           new XElement("display-name", "Robot Catalog", new XAttribute(XNamespace.Xml + "lang", "x-default")),
                           new XElement("online-flag", true),

                           new XElement("template"),
                           new XElement("page-attributes")));
            var categories = db.Categories.Include(x => x.Parent).Where(x => x.CategoryXmlId != "root").ToList();
            AttachCategoriesToXml(document, categories);
            AttachProductsToXmlCatalog(products, document);
            AttachProductsCategoriesDependencyXML(products, document);
            document.Save("RobotCatalogOK.xml");
        }

        private static void AttachProductsCategoriesDependencyXML(List<Product> products, XDocument document)
        {
            for (int i = 0; i < products.Count; i++)
            {
                var inner = products[i].Categories.ToList();
                if (inner.Count > 0)
                {
                    for (int k = 0; k < inner.Count; k++)
                    {
                        document.Root.Add(new XElement("category-assignment", new XAttribute("category-id", inner[k].CategoryXmlId),
                            new XAttribute("product-id", products[i].ProductId), new XElement("primary-flag", true)
                            ));
                    }
                }
            }
        }

        private static void AttachProductsToXmlCatalog(List<Product> products, XDocument document)
        {
            for (int i = 0; i < products.Count; i++)
            {
                AddMainProductXMLBody(products, document, i);
                AttachImageGroups(products, document, i);
            };
        }

        private static void AddMainProductXMLBody(List<Product> products, XDocument document, int i)
        {
            document.Root.Add(new XElement("product", new XAttribute("product-id", products[i].ProductId),
                 new XElement("ean"),
                 new XElement("upc", products[i].ProductId),
                 new XElement("unit"),
                  new XElement("min-order-quantity", 1),
                   new XElement("step-quantity", 1),
                       new XElement("display-name", products[i].ProductName, new XAttribute(XNamespace.Xml + "lang", "x-default")),
                          new XElement("short-description", products[i].ShortDesc, new XAttribute(XNamespace.Xml + "lang", "x-default")),
                           new XElement("long-description", products[i].LongDesc, new XAttribute(XNamespace.Xml + "lang", "x-default")),
                        new XElement("online-flag", true),
                         new XElement("available-flag", true),
                          new XElement("searchable-flag", true),
                        new XElement("images"),
                         new XElement("tax-class-id", "standard"),
                           new XElement("page-attributes"),
                             new XElement("custom-attributes")
                              ));
        }

        private static void AttachImageGroups(List<Product> products, XDocument document, int i)
        {
            var picturePaths = products[i].PicturesPaths.ToList();
            var currentProductObject = document.Root.Elements().FirstOrDefault(x => x.HasAttributes && x.Attribute("product-id") != null && x.Attribute("product-id").Value == products[i].ProductId);
            AttachSingleImgGroupByType(picturePaths, currentProductObject, "large");
            AttachSingleImgGroupByType(picturePaths, currentProductObject, "medium");
            AttachSingleImgGroupByType(picturePaths, currentProductObject, "small");
        }

        private static void AttachSingleImgGroupByType(List<PicturePath> picturePaths, XElement currentProductObject, string viewType)
        {
            var imgGroupXMLElement = new XElement("image-group", new XAttribute("view-type", viewType));
            List<PicturePath> viewTypePaths = new List<PicturePath>();
            if (viewType == "large")
            {
                viewTypePaths = picturePaths.Where(x => x.Content.Contains("_large")).ToList();
            }
            else if (viewType == "small")
            {
                viewTypePaths = picturePaths.Where(x => x.Content.Contains("_small")).ToList();
            }
            else
            {
                viewTypePaths = picturePaths.Where(x => !x.Content.Contains("_small") && !x.Content.Contains("_large")).ToList();
            }
            if (viewTypePaths.Count > 0)
            {

                for (int k = 0; k < viewTypePaths.Count; k++)
                {
                    imgGroupXMLElement.Add(new XElement("image", new XAttribute("path", viewTypePaths[k].Content)));
                }
            }
            currentProductObject.Element("images").Add(imgGroupXMLElement);
        }

        private static void AttachCategoriesToXml(XDocument document, List<Category> categories)
        {
            for (int i = 0; i < categories.Count; i++)
            {
                document.Root.Add(new XElement("category", new XAttribute("category-id", categories[i].CategoryXmlId),

                            new XElement("display-name", categories[i].Name, new XAttribute(XNamespace.Xml + "lang", "x-default")),
                            new XElement("online-flag", true),
                            new XElement("parent", categories[i].Parent.CategoryXmlId),
                            new XElement("image", "images/categories/" + categories[i].CategoryXmlId + ".jpg"),
                            new XElement("template"),
                            new XElement("page-attributes"),
                            new XElement("custom-attributes", new XElement("custom-attribute", true,
                                new XAttribute("attribute-id", "showInMenu"))
                            )));
            };
        }


        //Method for  moving producst from one directory to another
        public static void CopyPictures(List<Product> products)
        {
            string[] picLargeList = Directory.GetFiles(currentMainPath + "large", "*.jpg");
            string[] picMediumList = Directory.GetFiles(currentMainPath + "medium", "*.jpg");
            string[] picSmallList = Directory.GetFiles(currentMainPath + "small", "*.jpg");
            for (int i = 0; i < products.Count; i++)
            {
                var smallPaths = picSmallList.Where(x => x.Contains(products[i].ProductId) && x.Contains("_small")).ToList();
                CopyFiles(currentMainPath + "small", currentDestinationMain + "small", smallPaths);
                //medium
                var mediumPaths = picMediumList.Where(x => x.Contains(products[i].ProductId) && !x.Contains("_small")
                    && !x.Contains("_large")).ToList();
                CopyFiles(currentMainPath + "medium", currentDestinationMain + "medium", mediumPaths);
                //large
                var largePaths = picLargeList.Where(x => x.Contains(products[i].ProductId) && x.Contains("_large")).ToList();
                CopyFiles(currentMainPath + "large", currentDestinationMain + "large", largePaths);
            }
        }

        private static void CopyFiles(string currentPath, string destinationPath, List<string> variationPaths)
        {
            if (variationPaths.Count > 0)
            {
                for (int k = 0; k < variationPaths.Count; k++)
                {
                    string[] stringSeparators = new string[] { "\\", };
                    string[] result;
                    result = variationPaths[k].Split(stringSeparators, StringSplitOptions.RemoveEmptyEntries);
                    string fName = result[result.Length - 1];
                    File.Copy(Path.Combine(currentPath, fName), Path.Combine(destinationPath, fName), true);
                }
            }
        }
        public static void CreateInventoryList(List<Product> products)
        {
            Random rnd = new Random();
            XDocument document = new XDocument(
                          new XDeclaration("1.0", "UTF-8", ""),
                                    new XElement("inventory",
                                                 new XElement("inventory-list",
                                                            new XElement("header", new XAttribute("list-id", "inventory"),
                                                                new XElement("default-instock", false),
                                                                new XElement("description", "Robot inventory"),
                                                                new XElement("use-bundle-inventory-only", false)),
                                                            new XElement("records"))));
            var recordsElement = document.Root.Element("inventory-list").Element("records");
            for (int i = 0; i < products.Count(); i++)
            {
                var currentrandom = rnd.Next(5, 100);
                var currentRecordElement = new XElement("record", new XAttribute("product-id", products[i].ProductId),
                    new XElement("allocation", currentrandom),
                                                new XElement("allocation-timestamp", "2015-02-17T13:29:26.000Z"),
                                                 new XElement("perpetual", false),
                                                  new XElement("preorder-backorder-handling", "none"),
                                                   new XElement("preorder-backorder-allocation", 0),
                                                    new XElement("ats", currentrandom),
                                                     new XElement("on-order", 0),
                                                      new XElement("turnover", 0)
                    );
                recordsElement.Add(currentRecordElement);
            }
            document.Save("RobotInventoryOK.xml");
        }
        public static void CreatePriceBook(List<Product> products)
        {

            XDocument document = new XDocument(
                          new XDeclaration("1.0", "UTF-8", ""),
                                    new XElement("pricebooks",
                                                 new XElement("pricebook",
                                                            new XElement("header", new XAttribute("pricebook-id", "Robot-nonVat-prices"),
                                                                new XElement("currency", "GBP"),
                                                                new XElement("display-name", "Robot List Prices", new XAttribute(XNamespace.Xml + "lang", "x-default")),
                                                                new XElement("online-flag", true)),
                                                            new XElement("price-tables"))));
            var recordsElement = document.Root.Element("pricebook").Element("price-tables");


            for (int i = 0; i < products.Count(); i++)
            {

                if (products[i].PriceWithoutVat == null)
                {
                    products[i].PriceWithoutVat = "0.00";
                }
                if (products[i].PriceWithoutVat.Contains(','))
                {

                    products[i].PriceWithoutVat = products[i].PriceWithoutVat.Replace(",", "");
                }
                var currentPriceTableElement = new XElement("price-table", new XAttribute("product-id", products[i].ProductId),
                                                            new XElement("amount", products[i].PriceWithoutVat, new XAttribute("quantity", 1))
                    );
                recordsElement.Add(currentPriceTableElement);
            }
            document.Save("RobotPriceListOK.xml");
        }
    }
}
