using System.ComponentModel.DataAnnotations;

namespace Randevu.Models
{
	public class WeeklyAvailability
	{
		[Key]
		public int WeeklyAvailabilityID { get; set; }
		public int StaffID { get; set; }
		public DayOfWeek DayOfWeek { get; set; }  // Haftanın hangi günü müsait olduğu
		public TimeSpan StartTime { get; set; }  // Gün için çalışma başlangıç saati
		public TimeSpan EndTime { get; set; }    // Gün için çalışma bitiş saati

		public virtual Staff Staff { get; set; }
	}
}
