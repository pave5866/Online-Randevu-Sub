using System.ComponentModel.DataAnnotations;

namespace Randevu.Models
{
    public class Appointment
    {
        [Key]
        public int AppointmentID { get; set; }
        public int? CustomerID { get; set; }
        public int? ServiceID { get; set; }

        // StaffID nullable olarak tanımlanıyor.
        public int StaffID { get; set; }

        public DateTime Date { get; set; }
        public TimeSpan Time { get; set; }
        public TimeSpan? EndTime { get; set; }

		public string Status { get; set; }

        // Email ve Telefon bilgileri ekleniyor, her ikisi de nullable.
        public string? Name { get; set; }
        public string? Email { get; set; }
        public string? Telefon { get; set; }
        public string? comment { get; set; }
        public int? Price { get; set; }
		public DateTime? PaymentDueDate { get; set; } // Ödeme son tarihi
        public bool IsApproved { get; set; } = false;

		// İlişkili varlıklar
		public Customer Customer { get; set; }
        public Staff Staff { get; set; }
    }
}
