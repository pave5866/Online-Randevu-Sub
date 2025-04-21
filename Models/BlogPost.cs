using System.ComponentModel.DataAnnotations;

namespace Randevu.Models
{
    public class BlogPost
    {
        [Key]
        public int BlogPostID { get; set; }
        public string Title { get; set; }
        public string Content { get; set; }
        public DateTime PublishDate { get; set; }
    }
}
