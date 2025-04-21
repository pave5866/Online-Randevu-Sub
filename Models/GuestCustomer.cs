using System.ComponentModel.DataAnnotations;

namespace Randevu.Models
{
    public class GuestCustomer
    {
        [Key]
        public int GuestID { get; set; }
        public string FirstName { get; set; }
        public string LastName { get; set; }
        public string? Phone { get; set; }
        public string? Email { get; set; }
    }
}
