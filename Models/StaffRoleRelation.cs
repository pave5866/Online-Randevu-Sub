using System.ComponentModel.DataAnnotations;

namespace Randevu.Models
{
    public class StaffRoleRelation
    {
        public int StaffID { get; set; }
        public int RoleID { get; set; }

        public Staff Staff { get; set; }
        public Role Role { get; set; }
    }
}
