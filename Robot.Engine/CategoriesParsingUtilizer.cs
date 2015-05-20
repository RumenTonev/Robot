using Data;
using HtmlAgilityPack;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using Data.Migrations;
using System.Data.Entity;
using System.IO;
using System.Xml.Linq;
using Robot.Models;
using Robot.Data;

namespace Robot.Engine
{
    public static class CategoriesParsingUtilizer
    {
        const string currentDomain = "http://www.Robot.com";
        public static void FirstLevelCategoriesIterator(RobotContext db)
        {
            WebClient wb = new WebClient();
            string currentMainWindowURI = currentDomain + "/shops/Robot/Default.aspx";
            HtmlDocument docu = LoadCurrentHtmlDocument(wb, currentMainWindowURI);
            var mainContainer = docu.DocumentNode.SelectSingleNode("//ul[@id='menu']");
            //code repeatition because first level is  with different DOM structure
            var lis = mainContainer.ChildNodes.Where(x => x.Name == "li").ToList();
            foreach (var item in lis)
            {
                var category = new Category()
                {
                    Name = item.ChildNodes.FirstOrDefault(x => x.Name == "a").InnerText.Trim(),
                    IsLeaf = false,
                    Parent = db.Categories.FirstOrDefault(x => x.CategoryXmlId == "root"),
                    ParentId = db.Categories.FirstOrDefault(x => x.CategoryXmlId == "root").CategoryId,
                };
                CreateCategoryId(category, db);
                db.Categories.Add(category);
                db.SaveChanges();
                TraverseCategoryTree(wb, item, category, db);
            }
        }

        public static void LeafCategoriesIterator(RobotContext db)
        {
            //all leaf categories except these,which have different URISegment aka different DOM values
            var standartLeafs = db.Categories.Where(y => y.IsLeaf == true && y.URISegment.Contains("Range")).ToList();
            for (int i = 0; i < standartLeafs.Count; i++)
            {
                GetAllProductsInCategoryLeafRange(standartLeafs[i], db);
            }
        }
        private static void GetAllProductsInCategoryLeafRange(Category category, RobotContext db)
        {
            WebClient wb = new WebClient();
            string currentMatchURI = category.URISegment + "?pagesize=45&page=1";
            HtmlDocument document = LoadCurrentHtmlDocument(wb, currentMatchURI);
            HtmlNode mainContainer = document.GetElementbyId("maincontent");
            //vsi4ki menuta-vseki ima podmenuata
            var productDivs = mainContainer.Descendants().Where(x => x.Attributes["class"] != null
                && x.GetAttributeValue("id", "000").Contains("_rangeContainer")).ToList();
            foreach (var item in productDivs)
            {
                GetFirstValuesRangePage(item, category, db);
            }
        }

        //first values-product id & product name
        private static void GetFirstValuesRangePage(HtmlNode divContainer, Category category, RobotContext db)
        {
            var anchorHolder = divContainer.Descendants()
                .FirstOrDefault(x => x.Name == "a"
                    && x.Attributes["id"] != null && x.GetAttributeValue("id", "000").Contains("_ProductLink"));
            var hrefLinkHolder = anchorHolder.GetAttributeValue("href", "000");
            string pid = GetProductIdValue(hrefLinkHolder);
            string productName = anchorHolder.InnerText.Trim();
            var productIsIn = db.Products.Include(x => x.Categories).FirstOrDefault(x => x.ProductId == pid);
            //check for already entered product
            if (productIsIn != null)
            {
                if (!productIsIn.Categories.Contains(category))
                {
                    productIsIn.Categories.Add(category);
                    db.SaveChanges();
                }
                return;
            }
            var product = new Product()
            {
                ProductId = pid,
                ProductName = productName,
            };
            SetInternalProductValues(product, hrefLinkHolder, db);
            db.Products.Add(product);
            db.SaveChanges();
            product.Categories.Add(category);
            db.SaveChanges();
        }
        //set product values from product-details page
        private static void SetInternalProductValues(Product product, string hrefLinkHolder, RobotContext db)
        {
            WebClient wb = new WebClient();
            HtmlDocument docu = LoadCurrentHtmlDocument(wb, hrefLinkHolder);
            var mainContainer = docu.DocumentNode.SelectSingleNode("//div[@id='maincontent']");
            var picturesHolder = mainContainer.Descendants().FirstOrDefault(x => x.Name == "div"
                    && x.Attributes["id"] != null && x.GetAttributeValue("id", "000") == "product_image");
            ManagePicturesAndPicturesPath(picturesHolder, product, db);
            ManageDescriptions(mainContainer, product);
            ManagePrices(mainContainer, product);
        }

        private static void ManagePrices(HtmlNode mainContainer, Product product)
        {
            string[] result;
            var priceWithVatHolder = mainContainer.Descendants().FirstOrDefault(x => x.Attributes["id"] != null
                && x.GetAttributeValue("id", "000") == "product_price_inc_vat");
            if (priceWithVatHolder == null)
            {
                product.PriceWithVat = null;
            }
            else
            {
                var priceWithVatHolderString = mainContainer.Descendants().FirstOrDefault(x => x.Attributes["id"] != null
                    && x.GetAttributeValue("id", "000") == "product_price_inc_vat").InnerText;
                string[] stringSeparators = new string[] { " ", "inc" };
                result = priceWithVatHolderString.Split(stringSeparators, StringSplitOptions.RemoveEmptyEntries);
                string priceWithVatStr = result.FirstOrDefault(x => x.Contains("£")).Trim();
                product.PriceWithVat = priceWithVatStr.Substring(1);
            }
            var priceWithoutVatHolder = mainContainer.Descendants().FirstOrDefault(x => x.Attributes["id"] != null
               && x.GetAttributeValue("id", "000") == "product_price");
            if (priceWithoutVatHolder == null)
            {
                product.PriceWithoutVat = null;
            }
            else
            {
                string[] stringexSeparators = new string[] { " ", "ex" };
                var priceWithoutVatHolderString = priceWithoutVatHolder.InnerText.Trim();
                string[] result2;
                result2 = priceWithoutVatHolderString.Split(stringexSeparators, StringSplitOptions.RemoveEmptyEntries);
                string priceWithoutVatStr = result2.FirstOrDefault(x => x.Contains("£")).Trim();
                product.PriceWithoutVat = priceWithoutVatStr.Substring(1);
            }
        }

        private static void ManageDescriptions(HtmlNode mainContainer, Product product)
        {
            if (mainContainer.Descendants().FirstOrDefault(x => x.Attributes["id"] != null
               && x.GetAttributeValue("id", "000") == "product_strapline") == null)
            {
                Console.WriteLine("No DescriptionHolder pid {0} ", product.ProductId);
                return;
            }
            var shs = mainContainer.Descendants().FirstOrDefault(x => x.Attributes["id"] != null
               && x.GetAttributeValue("id", "000") == "product_strapline").ChildNodes[0];
            var longSh = mainContainer.Descendants().FirstOrDefault(x => x.Attributes["id"] != null
               && x.GetAttributeValue("id", "000") == "product_overview");
            var shortDescString = shs.InnerText.Trim();
            var longDescString = mainContainer.Descendants().FirstOrDefault(x => x.Attributes["id"] != null
               && x.GetAttributeValue("id", "000") == "product_overview").InnerText.Trim();
            string[] stringSeparators = new string[] {  "<!","-->"
                };
            string[] result;
            result = longDescString.Split(stringSeparators, StringSplitOptions.RemoveEmptyEntries);
            product.ShortDesc = shortDescString;
            product.LongDesc = result[2].Trim();
        }

        private static void ExtractPictureProductFromCarousel(HtmlNode itemSource, Product product, bool isCarousel, int index, RobotContext db)
        {
            //img tag element
            var imgSource = itemSource.GetAttributeValue("src", "000");
            if (!imgSource.EndsWith(".jpg")) { return; }
            if (!imgSource.StartsWith("/_")) { return; }
            var smallPath = @"C:\Users\Rumen\Desktop\RobotPics\small\" + product.ProductId + "_small.jpg";
            var mediumPath = @"C:\Users\Rumen\Desktop\RobotPics\medium\" + product.ProductId + ".jpg";
            var largePath = @"C:\Users\Rumen\Desktop\RobotPics\large\" + product.ProductId + "_large.jpg";
            if (isCarousel)
            {
                var removal = imgSource.Substring(imgSource.LastIndexOf('_'), 6);
                if (removal != "_small")
                {
                    Console.WriteLine(product.ProductId);
                    return;
                }
                imgSource = imgSource.Remove(imgSource.LastIndexOf('_'), 6);

                if (index != 0)
                {
                    smallPath = @"C:\Users\Rumen\Desktop\RobotPics\small\" + product.ProductId + "_" + index + "_small.jpg";
                    mediumPath = @"C:\Users\Rumen\Desktop\RobotPics\medium\" + product.ProductId + "_" + index + ".jpg";
                    largePath = @"C:\Users\Rumen\Desktop\RobotPics\large\" + product.ProductId + "_" + index + "_large.jpg";
                }
            }
            string imageSmallSource = imgSource.Insert(imgSource.LastIndexOf('.'), "_small");
            string imageLargeSource = imgSource.Insert(imgSource.LastIndexOf('.'), "_large");
            //Exceptions for non-stanrd named products
            try
            {
                DownloadPictureAndAddPicturePath(product, db, smallPath, imageSmallSource);
                DownloadPictureAndAddPicturePath(product, db, mediumPath, imgSource);
                DownloadPictureAndAddPicturePath(product, db, largePath, imageLargeSource);
            }
            catch (WebException ex)
            {
            }
        }

        private static void DownloadPictureAndAddPicturePath(Product product, RobotContext db, string pathSource, string imageSourcePath)
        {
            string localPath = new Uri(pathSource).LocalPath;
            using (WebClient wc = new WebClient())
                wc.DownloadFile(currentDomain + imageSourcePath, localPath);
            var path = new PicturePath()
            {
                Product = product,
                ProductId = product.ProductId,
                Content = SetPicturePath(pathSource),
            };
            db.PicturePaths.Add(path);
        }
        private static string SetPicturePath(string localPath)
        {
            string[] stringSeparators = new string[] { @"C:\Users\Rumen\Desktop\RobotPics\" };
            string[] result;
            result = localPath.Split(stringSeparators, StringSplitOptions.RemoveEmptyEntries);
            result[0] = result[0].Replace("\\", "/");
            return result[0];
        }

        private static void ManagePicturesAndPicturesPath(HtmlNode picturesHolder, Product product, RobotContext db)
        {
            if (picturesHolder == null)
            {
                //for check up-preventing parsing of non-standard HTML
                Console.WriteLine("No PicturesHolder pid {0} ", product.ProductId);
                return;
            }
            var corouselContainer = picturesHolder.Descendants().FirstOrDefault(x => x.Attributes["class"] != null && x.GetAttributeValue("class", "000") == "carousel_container");
            //only 1 picture-medium
            if (corouselContainer == null)
            {
                var mainImageHolder = picturesHolder.Descendants()
                    .FirstOrDefault(x => x.Attributes["id"] != null
                        && x.GetAttributeValue("id", "000") == "main_product_image");
                var imageTag = mainImageHolder.Descendants().FirstOrDefault(x => x.Name == "img" && x.Attributes["class"] != null && x.GetAttributeValue("class", "000") == "main_product_image");
                ExtractPictureProductFromCarousel(imageTag, product, false, 0, db);
            }
            else
            {
                var allimages = corouselContainer.Descendants()
                    .Where(x => x.Attributes["class"] != null
                        && x.Name == "img" && x.GetAttributeValue("class", "000") == "carousel_image").ToList();
                for (int i = 0; i < allimages.Count; i++)
                {
                    ExtractPictureProductFromCarousel(allimages[i], product, true, i, db);
                }
            }
        }

        private static string GetProductIdValue(string hrefLinkHolder)
        {
            string[] stringSeparators = new string[] { "/", "//" };
            string[] result;
            result = hrefLinkHolder.Split(stringSeparators, StringSplitOptions.RemoveEmptyEntries);
            var pid = result.FirstOrDefault(x => x.StartsWith("PD"));
            return pid;
        }
        //split over all special symbols join with - with parent categor Id-DW standart for xml
        private static void CreateCategoryId(Category catObj, RobotContext db)
        {
            string[] stringSeparators = new string[] { ".", ",", "/", ":", "-", "&", " ", "  ","\\","|", "\"", "!", "?",
                "@", "#", "$", "%", "^", "*", "(",")", "_", "+","=",";","{", "}","[","]" };
            string[] result;
            result = catObj.Name.Split(stringSeparators, StringSplitOptions.RemoveEmptyEntries);
            for (int i = 0; i < result.Length; i++)
            {
                result[i] = result[i].Replace("'", "");
                result[i] = result[i].Trim();
                result[i] = result[i].ToLower();
            }
            catObj.CategoryXmlId = String.Join("-", result);
            if (catObj.Parent.CategoryXmlId != "root")
            {
                catObj.CategoryXmlId = catObj.CategoryXmlId + "-" + catObj.Parent.CategoryXmlId;
            }
        }
        private static void TraverseCategoryTree(WebClient wb, HtmlNode subCategoryDivHolder, Category parentCategory, RobotContext db)
        {

            string innerMatchURI = subCategoryDivHolder.ChildNodes.FirstOrDefault(x => x.Name == "a").GetAttributeValue("href", "000");
            HtmlDocument innerdocu = LoadCurrentHtmlDocument(wb, innerMatchURI);
            var subcategories = innerdocu.DocumentNode.Descendants().Where(x => x.Attributes["class"] != null && x.GetAttributeValue("class", "000") == "subcategory").ToList();
            if (subcategories.Count == 0)
            {
                parentCategory.IsLeaf = true;
                return;
            }
            //divs for subcategory on parent category page
            foreach (var subCategoryDOMElement in subcategories)
            {
                var category = new Category()
                {
                    Name = subCategoryDOMElement.InnerText.Trim(),
                    IsLeaf = false,
                    Parent = parentCategory,
                    ParentId = parentCategory.CategoryId,
                    URISegment = subCategoryDOMElement.Descendants().FirstOrDefault(x => x.Name == "a").GetAttributeValue("href", "000").Trim()

                };
                CreateCategoryId(category, db);
                ExtractPicture(subCategoryDOMElement, category.CategoryXmlId + "-" + category.ParentId);
                db.Categories.Add(category);
                db.SaveChanges();
                TraverseCategoryTree(wb, subCategoryDOMElement, category, db);
            }
        }
        private static void ExtractPicture(HtmlNode subCategoryDivHolder, string categoryId)
        {
            var imgSource = subCategoryDivHolder.Descendants().FirstOrDefault(x => x.Name == "img").GetAttributeValue("src", "000");
            if (!imgSource.EndsWith(".jpg")) { return; }
            if (!imgSource.StartsWith("/_")) { return; }
            var path = @"C:\Users\Rumen\Desktop\RobotPics\category\" + categoryId + ".jpg";
            string localPath = new Uri(path).LocalPath;
            using (WebClient wc = new WebClient())
                wc.DownloadFile(currentDomain + imgSource, localPath);
        }

        //return HtmlDocument object with UTF-8 encodins
        public static HtmlDocument LoadCurrentHtmlDocument(WebClient wc, string currentMatchURI)
        {
            var responseData = wc.DownloadData(currentMatchURI);
            String source = Encoding.GetEncoding("utf-8").GetString(responseData, 0, responseData.Length - 1);
            source = WebUtility.HtmlDecode(source);
            HtmlDocument documentResult = new HtmlDocument();
            documentResult.LoadHtml(source);
            return documentResult;
        }
    }
}
 
