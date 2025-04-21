using System.ComponentModel.DataAnnotations;

namespace Randevu.Models
{
	public class GelirGider
	{
		[Key]
		public int ID { get; set; }
		public DateTime Tarih { get; set; }  // Ay ve Year yerine
		public int Gelir { get; set; }
		public int Gider { get; set; }
	}
}
