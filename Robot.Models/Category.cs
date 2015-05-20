using System.Collections.Generic;

namespace Robot.Models
{
    public class Category
    {
        public Category()
        {
            this.Products = new HashSet<Product>();
        }
        public int CategoryId { get; set; }
        public string CategoryXmlId { get; set; }
        public string URISegment { get; set; }
        public string Name { get; set; }
        public int ParentId { get; set; }
        public Category Parent { get; set; }
        public bool IsLeaf { get; set; }
        public virtual ICollection<Product> Products { get; set; }
    }
}
