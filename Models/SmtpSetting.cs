namespace Randevu.Models
{
	public class SmtpSetting
	{
		public string Host { get; set; }
		public int Port { get; set; }
		public bool EnableSsl { get; set; }
		public string Username { get; set; }
		public string Password { get; set; }
		public string FromAddress { get; set; }
	}
}
