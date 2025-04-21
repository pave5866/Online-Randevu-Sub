using System.ComponentModel.DataAnnotations;

namespace Randevu.Models
{
    public class Service
    {
        [Key]
        public int ServiceID { get; set; }
        public string Description { get; set; }
        public decimal Price { get; set; }
    }
}
