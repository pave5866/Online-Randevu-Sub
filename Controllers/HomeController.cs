using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Randevu.Models;
using Randevu.Models.ViewModels;
using System.Diagnostics;
using System.IO;
using System.Xml.Linq;

namespace Randevu.Controllers
{
	public class HomeController : Controller
	{
		private readonly ILogger<HomeController> _logger;
		private readonly ApplicationDbContext _context;
		private readonly IWebHostEnvironment _hostingEnvironment;
		private readonly EmailService _emailService;
		private readonly SmsService _smsService;

		public HomeController(ILogger<HomeController> logger, EmailService emailService, SmsService smsService, ApplicationDbContext context, IWebHostEnvironment hostingEnvironment)
		{
			_logger = logger;
			_context = context;
			_emailService = emailService;
			_smsService = smsService;
			_hostingEnvironment = hostingEnvironment;
		}
		private string LoadSiteTitleFromXml()
		{
			var settingsPath = Path.Combine(_hostingEnvironment.WebRootPath, "settings.xml");
			if (System.IO.File.Exists(settingsPath))
			{
				var settings = XElement.Load(settingsPath);
				return settings.Element("Title")?.Value;
			}
			return "Default Title";  // E�er ayar dosyas� yoksa veya ba�l�k bulunamazsa varsay�lan de�er
		}

		public IActionResult Index()
		{
			ViewBag.UserName = HttpContext.Session.GetString("UserName");

			// Fetch the last four recent blog posts
			var recentPosts = _context.BlogPosts
									  .OrderByDescending(p => p.PublishDate)
									  .Take(4)
									  .ToList();

			// Fetch recommended individuals from XML
			ViewBag.RecommendedIndividuals = LoadRecommendationsFromXml();

			var allStaff = _context.Staffs
								   .Where(s => s.status == true && s.StaffID != 1)
								   .ToList();  // Veritaban�ndan �ekim yap�l�yor

			// Resmi olanlar� filtrele
			var staffWithImages = allStaff.Where(s => ImageExists(s.StaffID)).ToList();

			// Resmi olanlar aras�ndan rastgele 3 tanesini se�
			var randomStaff = staffWithImages.OrderBy(r => Guid.NewGuid())
											 .Take(3)
											 .ToList();

			// Fetch contact information from XML
			ViewBag.ContactInfo = LoadContactInfo();

			// Transfer data to the view using ViewBag
			ViewBag.RecentPosts = recentPosts;
			ViewBag.SiteTitle = LoadSiteTitleFromXml();
			ViewBag.RandomStaff = randomStaff;

			return View();
		}

		private dynamic LoadRecommendationsFromXml()
		{
			string filePath = Path.Combine(_hostingEnvironment.WebRootPath, "settings3.xml");
			var recommendations = new List<dynamic>();
			if (System.IO.File.Exists(filePath))
			{
				var doc = XDocument.Load(filePath);
				recommendations = doc.Element("Settings").Element("Tavsiyeler")?.Elements("Tavsiye")
									  .Select(x => new
									  {
										  Name = x.Element("Name")?.Value,
										  Comment = x.Element("Comment")?.Value,
										  ImagePath = x.Element("Image")?.Value
									  }).ToList<dynamic>();
			}
			return recommendations.Any() ? recommendations : new List<dynamic>(); // E�er liste bo�sa, bo� bir liste d�n
		}


		private dynamic LoadContactInfo()
		{
			string filePath = Path.Combine(_hostingEnvironment.WebRootPath, "settings4.xml");
			if (System.IO.File.Exists(filePath))
			{
				var doc = XDocument.Load(filePath);
				var contactSettings = doc.Element("ContactSettings");
				return new
				{
					Phone = contactSettings.Element("Phone")?.Value,
					Address = contactSettings.Element("Address")?.Value,
					Whatsapp = contactSettings.Element("Whatsapp")?.Value,
					Email = contactSettings.Element("Email")?.Value,
					Instagram = contactSettings.Element("Instagram")?.Value,
					Twitter = contactSettings.Element("Twitter")?.Value,
					Facebook = contactSettings.Element("Facebook")?.Value,
					Youtube = contactSettings.Element("Youtube")?.Value,
					Linkedin = contactSettings.Element("Linkedin")?.Value // Make sure this line is correctly pulling data
				};
			}
			return null; // Return null if no contact info is found or the file doesn't exist
		}



		[ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
		public IActionResult Error()
		{
			return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
		}
		public IActionResult Blog(int id = 1)  // Varsay�lan olarak 1. sayfay� g�ster
		{
			int pageSize = 3;  // Her sayfada g�sterilecek blog say�s�
			ViewBag.UserName = HttpContext.Session.GetString("UserName");

			// Toplam blog say�s�n� al
			int totalPosts = _context.BlogPosts.Count();

			// Sayfalama i�in gerekli bloglar� al
			var blogs = _context.BlogPosts
								.OrderByDescending(b => b.PublishDate)  // Yay�n tarihine g�re tersten s�rala
								.Skip((id - 1) * pageSize)  // �nceki sayfalardaki bloglar� atla
								.Take(pageSize)  // Bu sayfada g�sterilecek blog say�s�n� al
								.ToList();

			ViewBag.ContactInfo = LoadContactInfo();
			ViewBag.SiteTitle = LoadSiteTitleFromXml();

			// Sayfalama bilgisini View'e ta��mak i�in ViewBag kullanabilirsiniz
			ViewBag.CurrentPage = id;
			ViewBag.TotalPages = (int)Math.Ceiling((double)totalPosts / pageSize);

			return View(blogs);  // Blog listesini view'e g�nder
		}

		public async Task<IActionResult> BlogDetail(int id)
		{
			ViewBag.ContactInfo = LoadContactInfo();
			ViewBag.SiteTitle = LoadSiteTitleFromXml();
			var blogPost = await _context.BlogPosts.FirstOrDefaultAsync(b => b.BlogPostID == id);
			if (blogPost == null)
			{
				// Blog post bulunamad�ysa, uygun bir hata mesaj� ile kullan�c�y� bilgilendirin
				return NotFound("�zg�n�z, arad���n�z blog postu bulunamad�.");
			}

			ViewBag.UserName = HttpContext.Session.GetString("UserName");
			return View(blogPost);  // Blog post modelini View'a g�nder
		}

		public IActionResult About()
		{
			ViewBag.UserName = HttpContext.Session.GetString("UserName");
			var allStaff = _context.Staffs
					   .Where(s => s.status == true && s.StaffID != 1)
					   .ToList();
			// Resmi olanlar� filtrele
			var staffWithImages = allStaff.Where(s => ImageExists(s.StaffID)).ToList();

			// Resmi olanlar aras�ndan rastgele 3 tanesini se�
			var randomStaffIds = staffWithImages.OrderBy(s => Guid.NewGuid())
												.Take(3)
												.Select(s => s.StaffID)
												.ToList();

			ViewBag.RandomStaffIds = randomStaffIds;
			ViewBag.ContactInfo = LoadContactInfo();
			ViewBag.SiteTitle = LoadSiteTitleFromXml();

			// Load additional data from XML
			ViewBag.AboutText = LoadAboutFromXml();
			ViewBag.FAQs = LoadFAQsFromXml();
			ViewBag.Recommendations = LoadRecommendationsFromXml();

			return View();
		}
		private IEnumerable<dynamic> LoadFAQsFromXml()
		{
			string filePath = Path.Combine(_hostingEnvironment.WebRootPath, "settings2.xml");
			var faqs = new List<dynamic>();
			if (System.IO.File.Exists(filePath))
			{
				var doc = XDocument.Load(filePath);
				faqs = doc.Element("Settings").Element("FAQs")?.Elements("FAQ")
						  .Select(x => new
						  {
							  Question = x.Element("Question")?.Value,
							  Answer = x.Element("Answer")?.Value
						  }).ToList<dynamic>();
			}
			return faqs;
		}
		private string LoadAboutFromXml()
		{
			string filePath = Path.Combine(_hostingEnvironment.WebRootPath, "settings2.xml");
			if (System.IO.File.Exists(filePath))
			{
				var doc = XDocument.Load(filePath);
				return doc.Element("Settings").Element("About")?.Value;
			}
			return "No about us information available.";
		}
		public IActionResult Team()
		{
			ViewBag.UserName = HttpContext.Session.GetString("UserName");
			var allStaff = _context.Staffs
									.Where(s => s.status == true && s.StaffID != 1) // 'status == true' ve 'StaffID != 1'
									.ToList();

			// Resmi olan �al��anlar� filtrele
			var staffList = allStaff.Where(s => ImageExists(s.StaffID)).ToList();

			ViewBag.ContactInfo = LoadContactInfo();
			ViewBag.SiteTitle = LoadSiteTitleFromXml();
			return View(staffList);
		}

		private bool ImageExists(int staffId)
		{
			var path = Path.Combine(_hostingEnvironment.WebRootPath, "img", "staff", $"{staffId}.png");
			return System.IO.File.Exists(path);
		}
		public IActionResult Services()
		{
			ViewBag.UserName = HttpContext.Session.GetString("UserName");
			var services = _context.Services.ToList();

			ViewBag.ContactInfo = LoadContactInfo();
			ViewBag.SiteTitle = LoadSiteTitleFromXml();
			return View(services);
		}
		public IActionResult Privacy()
		{
			ViewBag.UserName = HttpContext.Session.GetString("UserName");
			ViewBag.ContactInfo = LoadContactInfo();
			ViewBag.SiteTitle = LoadSiteTitleFromXml();

			string xmlFilePath = Path.Combine(_hostingEnvironment.WebRootPath, "settings6.xml");
			if (System.IO.File.Exists(xmlFilePath))
			{
				XElement settings = XElement.Load(xmlFilePath);
				ViewBag.Privacy = settings.Element("Privacy")?.Value; // Load privacy text
			}

			return View();
		}

		public IActionResult Contact()
		{
			ViewBag.UserName = HttpContext.Session.GetString("UserName");
			ViewBag.ContactInfo = LoadContactInfo();
			ViewBag.SiteTitle = LoadSiteTitleFromXml();
			return View();
		}
		public IActionResult Randevu()
		{
			string siteName = HttpContext.Session.GetString("SiteName");
			ViewBag.UserName = HttpContext.Session.GetString("UserName");
			ViewBag.UserRole = HttpContext.Session.GetString("UserRole");
			ViewBag.ContactInfo = LoadContactInfo();
			ViewBag.SiteTitle = LoadSiteTitleFromXml();
			var today = DateTime.Today;

			Dictionary<DayOfWeek, string> dayNames = new Dictionary<DayOfWeek, string>
	{
		{ DayOfWeek.Sunday, "Paz" },
		{ DayOfWeek.Monday, "Pzt" },
		{ DayOfWeek.Tuesday, "Sal" },
		{ DayOfWeek.Wednesday, "Car" },
		{ DayOfWeek.Thursday, "Per" },
		{ DayOfWeek.Friday, "Cum" },
		{ DayOfWeek.Saturday, "Cmt" }
	};

			var staffList = _context.Staffs
				.Where(s => s.status && s.StaffID != 1) // Only active staff
				.Select(s => new
				{
					StaffID = s.StaffID,
					FullName = $"{s.FirstName} {s.LastName}",
					Availabilities = s.WeeklyAvailabilities
						.Select(w => new
						{
							DayOfWeek = w.DayOfWeek,
							IsAvailable = true // Initial check for availability
						}).ToList()
				}).ToList();

			var availableStaff = new List<object>();

			foreach (var staff in staffList)
			{
				var hasAvailableSlot = false;

				for (var date = today; date < today.AddDays(15); date = date.AddDays(1))
				{
					var isSpecialDay = _context.SpecialDays
						.Any(sd => sd.StaffID == staff.StaffID && sd.Date == date);

					if (isSpecialDay)
						continue;

					var availability = staff.Availabilities
						.FirstOrDefault(a => a.DayOfWeek == date.DayOfWeek);

					if (availability != null)
					{
						hasAvailableSlot = true;
						break;
					}
				}

				if (hasAvailableSlot)
				{
					availableStaff.Add(new
					{
						StaffID = staff.StaffID,
						FullName = staff.FullName,
						Availabilities = staff.Availabilities
							.Select(a => new
							{
								DayOfWeek = dayNames[a.DayOfWeek],
								a.IsAvailable
							}).ToList()
					});
				}
			}

			ViewBag.StaffAvailability = availableStaff;
			return View();
		}

		[HttpPost]
		public IActionResult ProcessRandevu(int staffId)
		{
			// Perform operations such as validation, booking preparations, etc.

			// Store necessary data in TempData or Session if needed across redirects
			TempData["SelectedStaffId"] = staffId;

			// Redirect to a clean URL action
			return RedirectToAction("RandevuAdim2");
		}
		public IActionResult RandevuAdim2()
		{
			int staffId = TempData["SelectedStaffId"] != null ? (int)TempData["SelectedStaffId"] : 0;
			if (staffId == 0)
			{
				return RedirectToAction("Index", "Home");
			}

			// Load necessary data
			ViewBag.UserName = HttpContext.Session.GetString("UserName");
			ViewBag.ContactInfo = LoadContactInfo();
			ViewBag.SiteTitle = LoadSiteTitleFromXml();
			ViewBag.SelectedStaffId = staffId;

			// Date calculation: from today to the next 15 days
			var startDate = DateTime.Today;
			var endDate = startDate.AddDays(15);

			var unavailableDates = _context.SpecialDays
				.Where(x => x.StaffID == staffId && x.Date >= startDate && x.Date <= endDate)
				.Select(x => x.Date)
				.ToList();

			var appointments = _context.Appointments
				.Where(x => x.StaffID == staffId && x.Date >= startDate && x.Date <= endDate)
				.ToList();

			var weeklyAvailability = _context.WeeklyAvailabilities
				.Where(x => x.StaffID == staffId)
				.ToList();

			List<DateTime> availableDays = new List<DateTime>();
			Dictionary<DateTime, string> dayNames = new Dictionary<DateTime, string>();
			for (DateTime date = startDate; date <= endDate; date = date.AddDays(1))
			{
				if (!unavailableDates.Contains(date.Date) &&
					weeklyAvailability.Any(w => w.DayOfWeek == date.DayOfWeek))
				{
					availableDays.Add(date);
					dayNames.Add(date, GetTurkishDayName(date.DayOfWeek)); // T�rk�e g�n ad�n� dictionary'e ekleyin
				}
			}

			var timeSlots = GenerateTimeSlots(availableDays, weeklyAvailability, appointments);

			ViewBag.AvailableTimeSlots = timeSlots;
			ViewBag.DayNames = dayNames; // T�rk�e g�n adlar�n� ViewBag ile ge�irin

			return View();
		}

		private string GetTurkishDayName(DayOfWeek dayOfWeek)
		{
			// T�rk�e g�n adlar�
			switch (dayOfWeek)
			{
				case DayOfWeek.Monday: return "Pazartesi";
				case DayOfWeek.Tuesday: return "Sali";
				case DayOfWeek.Wednesday: return "Carsamba";
				case DayOfWeek.Thursday: return "Persembe";
				case DayOfWeek.Friday: return "Cuma";
				case DayOfWeek.Saturday: return "Cumartesi";
				case DayOfWeek.Sunday: return "Pazar";
				default: return null;
			}
		}

		public class TimeSlot
		{
			public TimeSpan StartTime { get; set; }
			public TimeSpan? EndTime { get; set; }
			public bool IsAvailable { get; set; }
		}
		private Dictionary<DateTime, List<TimeSlot>> GenerateTimeSlots(List<DateTime> availableDays, List<WeeklyAvailability> weeklyAvailability, List<Appointment> appointments)
		{
			var timeSlots = new Dictionary<DateTime, List<TimeSlot>>();
			var currentTime = DateTime.Now.TimeOfDay; // Mevcut saat
			var todayDate = DateTime.Today;

			foreach (var day in availableDays)
			{
				var dayAvailability = weeklyAvailability.FirstOrDefault(w => w.DayOfWeek == day.DayOfWeek);
				if (dayAvailability != null)
				{
					var startTime = dayAvailability.StartTime;
					var endTime = dayAvailability.EndTime;

					// E�er g�n bug�n ise ve mevcut saat �al��ma saatinden sonra ise, bu g�n� atla
					if (day.Date == todayDate && currentTime >= endTime)
					{
						continue; // Bu g�n i�in zaman dilimleri olu�turma
					}

					//E�er g�n bug�n ise ve mevcut saat �al��ma saatinden �nce ise, mevcut saati baz al
					if (day.Date == todayDate && currentTime > startTime)
					{
						startTime = new TimeSpan(currentTime.Hours, ((currentTime.Minutes / 30) + 1) * 30, 0); // Mevcut saati en yak�n yar�m saate yuvarla
					}

					var slots = new List<TimeSlot>();
					while (startTime < endTime)
					{
						bool isAvailable;
						
						isAvailable = !appointments.Any(a => a.Date.Date == day && startTime >= a.Time && startTime <= a.EndTime);

						slots.Add(new TimeSlot { StartTime = startTime, IsAvailable = isAvailable });

						startTime = startTime.Add(TimeSpan.FromMinutes(30)); // Her 30 dakikada bir zaman dilimi
					}
					if (slots.Any())
					{
						timeSlots.Add(day, slots);
					}
				}
			}
			return timeSlots;
		}
		public IActionResult SelectTime(int staffId, DateTime date, TimeSpan time)
		{
			TempData["StaffId"] = staffId;
			TempData["AppointmentDate"] = date;
			// TimeSpan nesnesini string olarak kaydet
			TempData["AppointmentTime"] = time.ToString();
			return RedirectToAction("RandevuAdim3");
		}
		public IActionResult RandevuAdim3()
		{
			int? customerId = HttpContext.Session.GetInt32("UserId");
			ViewBag.UserName = HttpContext.Session.GetString("UserName");
			ViewBag.UserRole = HttpContext.Session.GetString("UserRole");
			ViewBag.ContactInfo = LoadContactInfo();
			ViewBag.SiteTitle = LoadSiteTitleFromXml();

			if (TempData["StaffId"] is int staffId &&
				TempData["AppointmentDate"] is DateTime date &&
				TempData["AppointmentTime"] is string timeString && TimeSpan.TryParse(timeString, out TimeSpan time))
			{
				var staff = _context.Staffs.FirstOrDefault(s => s.StaffID == staffId);
				ViewBag.StaffName = staff?.FirstName + " " + staff?.LastName;
				ViewBag.StaffId = staffId;
				ViewBag.AppointmentDate = date;
				ViewBag.AppointmentTime = time;

				// Kullan�c� giri� yapm��sa, ilgili kullan�c� bilgilerini �ek
				if (customerId.HasValue)
				{
					var customer = _context.Customers.FirstOrDefault(c => c.CustomerID == customerId.Value);
					if (customer != null)
					{
						ViewBag.CustomerName = customer.FirstName + " " + customer.LastName;
						ViewBag.CustomerPhone = customer.Phone;
						ViewBag.CustomerEmail = customer.Email;
					}
				}
			}
			else
			{
				return RedirectToAction("RandevuAdim2"); // Gerekli bilgiler eksikse geri y�nlendir
			}

			var hizmetler = _context.Services.ToList();
			ServiceListVM serviceListVM = new ServiceListVM();
			serviceListVM.Services = hizmetler;
			return View(serviceListVM);
		}
		[HttpPost]
		public IActionResult ConfirmAppointment(int staffId, DateTime date, TimeSpan time, string name, string phone, string email, string comment, int? ServiceId)
		{
			string userIP = HttpContext.Connection.RemoteIpAddress?.ToString();

			// Kullan�c� IP adresine g�re randevu talep s�n�rlamas�n� kontrol et
			if (!CanRequestAppointment(userIP))
			{
				TempData["Error"] = "Bug�n i�in maksimum randevu talep limitine ula�t�n�z.";
				return RedirectToAction("Error");
			}

			// �ye olmayan kullan�c�lar i�in email ve telefon bilgisinin daha �nce kullan�l�p kullan�lmad���n� kontrol et
			bool emailExists = _context.Customers.Any(c => c.Email == email) || _context.Staffs.Any(s => s.Email == email);
			bool phoneExists = _context.Customers.Any(c => c.Phone == phone) || _context.Staffs.Any(s => s.Phone == phone);

			if (emailExists || phoneExists)
			{
				TempData["Error"] = "Belirtilen email veya telefon numaras� zaten kullan�mda.";
				return RedirectToAction("Error");
			}

			int? customerId = HttpContext.Session.GetInt32("UserId");
			Customer customer = null;

			// E�er kullan�c� giri� yapm��sa, mevcut m��teri bilgilerini kullan
			if (customerId.HasValue)
			{
				customer = _context.Customers.FirstOrDefault(c => c.CustomerID == customerId.Value);
				name = customer?.FirstName + " " + customer?.LastName;
				phone = customer?.Phone;
				email = customer?.Email;
			}

			// Randevu bilgilerini haz�rla
			var appointment = new Appointment
			{
				CustomerID = customerId, // E�er �ye de�ilse, CustomerID null olarak atanabilir
				StaffID = staffId,
				Date = date,
				Time = time,
				EndTime = time.Add(TimeSpan.FromMinutes(30)),
				Status = "Onay Bekleniyor",
				Name = name,
				Telefon = phone,
				IsApproved = false,
				Email = email,
				comment = comment, // Set the comment
				ServiceID = ServiceId,
			};

			// Randevuyu veritaban�na ekle
			_context.Appointments.Add(appointment);
			_context.SaveChanges();

			// Randevu kay�t i�lemi sonras�nda, randevu onay email'i g�nder
			var staff = _context.Staffs.FirstOrDefault(s => s.StaffID == staffId);
			string staffName = staff?.FirstName + " " + staff?.LastName;
			string reviewLink = Url.Action("MyAppointments", "AdmPanel", null, protocol: HttpContext.Request.Scheme);

			_emailService.SendActivationAppointmentEmail(email, name, date, time, staffName);
			_smsService.SendActivationAppointmentSms(phone, name, date, time, staffName);

			// Personel i�in randevu inceleme davetiyesi e-postas� g�nder
			_emailService.SendActivationStaffAppointmentEmail(staff.Email, name, date, time, phone, email, comment, reviewLink);
			_smsService.SendActivationStaffAppointmentSms(staff.Phone, reviewLink);

			// IP ve talep kayd�n� ActionTrack'e ekle
			_context.ActionTracks.Add(new ActionTrack
			{
				ActionIP = userIP,
				ActionName = "Randevu Talebi",
				Date = DateTime.Now
			});
			_context.SaveChanges();

			// Ba�ar�l� randevu olu�turma sayfas�na y�nlendir
			return RedirectToAction("AppointmentConfirmed");
		}

		private bool CanRequestAppointment(string ip)
		{
			var today = DateTime.Today;
			var count = _context.ActionTracks
						.Where(at => at.ActionIP == ip && at.Date.Date == today)
						.Count();

			return count < 222;
		}

		public IActionResult AppointmentConfirmed()
		{
			return View();
		}

	}
}
