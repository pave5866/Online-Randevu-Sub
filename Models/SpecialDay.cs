using System.ComponentModel.DataAnnotations;

namespace Randevu.Models
{
	public class SpecialDay
	{
        [Key]
        public int SpecialDayID { get; set; }
        public int StaffID { get; set; }
        public DateTime Date { get; set; } // Özel günün tarihi
        public string Description { get; set; } // Günün açıklaması, örneğin "Ulusal Tatil" veya "Kişisel İzin"

        public virtual Staff Staff { get; set; }
    }
}
