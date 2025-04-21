using System.ComponentModel.DataAnnotations;

namespace Randevu.Models
{
	public class ActionTrack
	{
		[Key]
		public int ActionID { get; set; }
		public string ActionIP { get; set; }
		public string ActionName { get; set; }
		public DateTime Date { get; set; }
	}
}
