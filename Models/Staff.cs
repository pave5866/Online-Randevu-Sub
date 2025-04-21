using System.ComponentModel.DataAnnotations;
using System.Data;

namespace Randevu.Models
{
    public class Staff
    {
        [Key]
        public int StaffID { get; set; }
        public string FirstName { get; set; }
        public string LastName { get; set; }
        public string Position { get; set; }
        public string? Phone { get; set; }
        public string? Email { get; set; }
        public string PasswordHash { get; set; }
        public int RoleID { get; set; }

        public Role Role { get; set; }
        public bool status { get; set; }
		public virtual ICollection<WeeklyAvailability> WeeklyAvailabilities { get; set; } = new List<WeeklyAvailability>();
        public virtual ICollection<SpecialDay> SpecialDays { get; set; } = new List<SpecialDay>();


    }
}
