using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Robot.Models
{
    public class Product
    {
        public Product()
        {
            this.PicturesPaths = new HashSet<PicturePath>();
            this.Categories = new HashSet<Category>();

        }
        public string ProductId { get; set; }
        public string ProductName { get; set; }
        public string ShortDesc { get; set; }
        public string LongDesc { get; set; }
        public string PriceWithVat { get; set; }
        public string PriceWithoutVat { get; set; }
        public virtual ICollection<PicturePath> PicturesPaths { get; set; }
        public virtual ICollection<Category> Categories { get; set; }
    }
}
