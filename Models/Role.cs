using System.ComponentModel.DataAnnotations;

namespace Randevu.Models
{
    public class Role
    {
        [Key]
        public int RoleID { get; set; }
        public string RoleName { get; set; }
    }
}
