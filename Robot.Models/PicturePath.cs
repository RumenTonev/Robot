using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Robot.Models
{
    public class PicturePath
    {
        public int PicturePathId { get; set; }
        public string Content { get; set; }
        public string ProductId { get; set; }
        public Product Product { get; set; }
    }
}
