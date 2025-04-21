using System.ComponentModel.DataAnnotations;

namespace Randevu.Models
{
    public class Customer
    {
        [Key]
        public int CustomerID { get; set; }
        public string FirstName { get; set; }
        public string LastName { get; set; }
        public Guid? ActivationCode { get; set; }
        public string? Phone { get; set; }
        public string Email { get; set; }
        public string PasswordHash { get; set; }
        public bool Active { get; set; }
        public DateTime ActivationExpiry { get; set; }
		public string? ResetPasswordCode { get; set; }
		public DateTime ResetPasswordCodeExpiry { get; set; }
    }
}
