using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;
using System.Text;
using System.Threading.Tasks;
using HtmlAgilityPack;
using System.Net;
using System.IO;
using System.Data.Entity;
using Data.Migrations;
using System.Drawing;
using System.Drawing.Imaging;
using Data;
using Robot.Data;

namespace Robot.Engine
{
    public class Program
    {       
        public static void Main(string[] args)
        {
            RobotContext db = new RobotContext();
            var products = db.Products.ToList();
            ProductParsingUtilizer.CreateCatalog(db, products);
           //init DB may use different migration config
            Database.SetInitializer(new MigrateDatabaseToLatestVersion<RobotContext, Configuration>());
            ProductParsingUtilizer.CreatePriceBook(products);
            ProductParsingUtilizer.CreateInventoryList(products);
            ProductParsingUtilizer.CreateCatalog(db, products);
            ProductParsingUtilizer.ResizePictures();
        }  
    }
}
