using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Randevu.Models;
using System.Diagnostics;
using System.Globalization;
using System.Net;
using System.Net.Mail;
using System.Text;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.Formats.Png;
using System.Xml.Linq;
using System.Drawing;
using System.Drawing.Imaging;
using Microsoft.Extensions.Hosting.Internal;
using SixLabors.ImageSharp.Formats;
using System.Runtime.InteropServices;
using Microsoft.Extensions.Hosting;
using System.Collections.Specialized;
using Newtonsoft.Json;
using System.Security.Cryptography;
using Newtonsoft.Json.Linq;
using System.Buffers;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Authorization;
using Nancy.Json;

namespace Randevu.Controllers
{
	public class AdmPanelController : Controller
	{
		private readonly ApplicationDbContext _context;
		private readonly EmailService _emailService;
		private readonly SmsService _smsService;
		private readonly IWebHostEnvironment _hostingEnvironment;
		public AdmPanelController(ApplicationDbContext context, EmailService emailService, SmsService smsService, IWebHostEnvironment hostingEnvironment)
		{
			_context = context;
			_emailService = emailService;
			_smsService = smsService;
			_hostingEnvironment = hostingEnvironment;
		}

		private readonly string merchant_id = "472857";
		private readonly string merchant_key = "aRG532rytsx4eUrA";
		private readonly string merchant_salt = "fryhRk22hCdrR7S9";

		public IActionResult Index()
		{
			int? userId = HttpContext.Session.GetInt32("UserId");
			string userRole = HttpContext.Session.GetString("UserRole");
			string siteName = HttpContext.Session.GetString("SiteName");
			if (siteName != LoadSiteTitleFromXml())
			{
				return RedirectToAction("Index", "Home");
			}
			if (userRole == "Customer")
			{
				// Eğer kullanıcı role Customer ise, direkt ana sayfaya yönlendir.
				return RedirectToAction("Index", "Home");
			}
			// Kullanıcı bilgilerini veritabanından çekme
			var user = _context.Staffs.FirstOrDefault(u => u.StaffID == userId.Value);
			if (user == null)
			{
				// Kullanıcı bilgisi bulunamazsa, oturumu sonlandır ve login sayfasına yönlendir
				HttpContext.Session.Clear();
				return RedirectToAction("Login", "AdmPanel");
			}

			// Staff isminin ilk iki harfini al ve ViewBag ile taşı
			if (!string.IsNullOrEmpty(user.FirstName) && user.FirstName.Length >= 2)
			{
				ViewBag.UserInitials = user.FirstName.Substring(0, 2); // İlk iki harfi al
			}
			else if (!string.IsNullOrEmpty(user.FirstName))
			{
				ViewBag.UserInitials = user.FirstName; // İsim iki harften kısa ise tüm ismi kullan
			}
			else
			{
				ViewBag.UserInitials = ""; // İsim boş veya null ise boş bir string ata
			}
			ViewBag.UserName = user.FirstName + " " + user.LastName;
			ViewBag.UserPosition = user.Position;
			ViewBag.UserRole = userRole;
			ViewBag.UserID = userId;
			var currentYear = DateTime.Now.Year;
			var gelirler = new int[12];
			var giderler = new int[12];

			for (int i = 0; i < 12; i++)
			{
				var month = i + 1;
				var gelirGider = _context.GelirGiders
					.Where(g => g.Tarih.Year == currentYear && g.Tarih.Month == month)
					.FirstOrDefault();

				if (gelirGider != null)
				{
					gelirler[i] = gelirGider.Gelir;
					giderler[i] = gelirGider.Gider;
				}
				else
				{
					gelirler[i] = 0;
					giderler[i] = 0;
				}
			}
			var onaylanmisRandevular = new int[12];

			for (int i = 0; i < 12; i++)
			{
				var month = i + 1;
				onaylanmisRandevular[i] = _context.Appointments
					.Where(a => a.Date.Year == currentYear && a.Date.Month == month && a.Status == "Onaylandı")
					.Count();
			}

			ViewBag.Gelirler = gelirler;
			ViewBag.Giderler = giderler;

			string filePath = Path.Combine(_hostingEnvironment.WebRootPath, "panel", "vendor", "morris", "custom", "barColors.js");
			var data = new List<string>
	{
		"// Morris Bar Colors",
		"Morris.Bar({",
		"    element: 'barColors',",
		"    data: ["
	};

			string[] months = { "Ocak", "Şubat", "Mart", "Nisan", "Mayıs", "Haziran", "Temmuz", "Ağustos", "Eylül", "Ekim", "Kasım", "Aralık" };
			for (int i = 0; i < 12; i++)
			{
				data.Add($"        {{ x: '{months[i]}', Randevu: {onaylanmisRandevular[i]} }},");
			}
			data.AddRange(new[]
			{
		"    ],",
		"    xkey: 'x',",
		"    ykeys: ['Randevu'],",
		"    labels: ['Randevu'],",
		"    resize: true,",
		"    gridLineColor: '#e1e5f1',",
		"    hideHover: 'auto',",
		"    barColors: ['#1a8e5f', '#262b31', '#434950', '#63686f', '#868a90']",
		"});"
	});
			ViewBag.MemberCount = _context.Customers.Count();
			ViewBag.PendingAppointments = _context.Appointments.Count(a => a.Status == "Onay Bekleniyor" || a.Status == "Ödeme Bekleniyor");
			ViewBag.StaffCount = _context.Staffs.Count();
			ViewBag.CompletedAppointments = _context.Appointments.Count(a => a.Status == "Onaylandı");

			System.IO.File.WriteAllLines(filePath, data);
			string xmlFilePath = Path.Combine(_hostingEnvironment.WebRootPath, "settings.xml");
			if (System.IO.File.Exists(xmlFilePath))
			{
				XElement settings = XElement.Load(xmlFilePath);
				ViewBag.SiteTitle = settings.Element("Title")?.Value; // Site ismini ViewBag ile taşı
			}
			switch (userRole)
			{
				case "Head Admin":
				case "Admin":
				case "Staff":
					return View(); // Tüm admin ve staff için aynı view gösteriliyor.
				case "Customer":
					return RedirectToAction("Index", "Home"); // Customer ana sayfasına yönlendir
				default:
					return RedirectToAction("Login", "AdmPanel"); // Rol tanımlanmamışsa login sayfasına geri dön
			}
		}

		#region USER LOGİN LOGOUT REGİSTER RESET AND FORGOT PASSWORD

		public IActionResult Login()
		{
			string xmlFilePath = Path.Combine(_hostingEnvironment.WebRootPath, "settings.xml");
			if (System.IO.File.Exists(xmlFilePath))
			{
				XElement settings = XElement.Load(xmlFilePath);
				ViewBag.SiteTitle = settings.Element("Title")?.Value; // Site ismini ViewBag ile taşı
			}
			// Kullanıcı ID'si session'da kontrol edilir.
			int? userId = HttpContext.Session.GetInt32("UserId");
			string userRole = HttpContext.Session.GetString("UserRole");
			switch (userRole)
			{
				case "Head Admin":
					return RedirectToAction("Index", "AdmPanel"); // Staff ana sayfasına yönlendir
				case "Admin":
					return RedirectToAction("Index", "AdmPanel"); // Staff ana sayfasına yönlendir
				case "Staff":
					return RedirectToAction("Index", "AdmPanel"); // Staff ana sayfasına yönlendir
				case "Customer":
					return RedirectToAction("Index", "Home"); // Customer ana sayfasına yönlendir
				default:
					return View(); // Rol tanımlanmamışsa login sayfasına geri dön
			}

		}

		[HttpPost]
		public IActionResult Login(string username, string password)
		{
			string userIP = DetectIPAddress(Request);
			string actionName = "login";

			var recentAttempts = _context.ActionTracks
				.Where(at => at.ActionIP == userIP && at.Date >= DateTime.Now.AddMinutes(-10) && at.ActionName == "login-failed")
				.Count();
			string xmlFilePath = Path.Combine(_hostingEnvironment.WebRootPath, "settings.xml");
			if (System.IO.File.Exists(xmlFilePath))
			{
				XElement settings = XElement.Load(xmlFilePath);
				ViewBag.SiteTitle = settings.Element("Title")?.Value; // Site ismini ViewBag ile taşı
			}
			if (recentAttempts >= 3)
			{
				LogAction(userIP, actionName, "Login failed", false);
				ViewBag.Hata = "Çok fazla giriş denemesi. Lütfen daha sonra tekrar deneyiniz.";
				return View();
			}

			string passwordHash = Convert.ToBase64String(Encoding.UTF8.GetBytes(password));
			var staff = _context.Staffs.Include(s => s.Role).FirstOrDefault(s => s.Email == username && s.PasswordHash == passwordHash && s.status);
			var customer = _context.Customers.FirstOrDefault(c => c.Email == username && c.PasswordHash == passwordHash && c.Active);

			if (staff != null)
			{
				HttpContext.Session.SetInt32("UserId", staff.StaffID);
				HttpContext.Session.SetString("UserName", staff.FirstName + " " + staff.LastName);
				HttpContext.Session.SetString("UserRole", staff.Role.RoleName);
				HttpContext.Session.SetString("SiteName", LoadSiteTitleFromXml());
				RemovePreviousFailedAttempts(userIP, actionName);
			}
			else if (customer != null)
			{
				HttpContext.Session.SetInt32("UserId", customer.CustomerID);
				HttpContext.Session.SetString("UserRole", "Customer");
				HttpContext.Session.SetString("SiteName", LoadSiteTitleFromXml());
				HttpContext.Session.SetString("UserName", customer.FirstName + " " + customer.LastName);
				RemovePreviousFailedAttempts(userIP, actionName);
			}
			else
			{
				LogAction(userIP, actionName, "Login failed", false);
				ViewBag.Hata = "Kullanıcı adı ya da şifre yanlış.";
				return View();
			}

			return RedirectToAction("Index", "AdmPanel");
		}

		public IActionResult Logout()
		{
			// Session bilgilerini temizle
			HttpContext.Session.Clear();

			// Kullanıcıyı Login sayfasına yönlendir
			return RedirectToAction("Login", "AdmPanel");
		}

		public IActionResult Register()
		{
			string xmlFilePath = Path.Combine(_hostingEnvironment.WebRootPath, "settings.xml");
			if (System.IO.File.Exists(xmlFilePath))
			{
				XElement settings = XElement.Load(xmlFilePath);
				ViewBag.SiteTitle = settings.Element("Title")?.Value; // Site ismini ViewBag ile taşı
			}
			// Kullanıcı ID'si session'da kontrol edilir.
			int? userId = HttpContext.Session.GetInt32("UserId");
			string userRole = HttpContext.Session.GetString("UserRole");

			switch (userRole)
			{
				case "Head Admin":
					return RedirectToAction("Index", "AdmPanel"); // Staff ana sayfasına yönlendir
				case "Admin":
					return RedirectToAction("Index", "AdmPanel"); // Staff ana sayfasına yönlendir
				case "Staff":
					return RedirectToAction("Index", "AdmPanel"); // Staff ana sayfasına yönlendir
				case "Customer":
					return RedirectToAction("Index", "Home"); // Customer ana sayfasına yönlendir
				default:
					return View(); // Rol tanımlanmamışsa login sayfasına geri dön
			}
		}

		[HttpPost]
		public ActionResult Register(string mail, string firstName, string lastName, string phone, string password1, string password2)
		{
			string userIP = DetectIPAddress(Request);
			string actionName = "register";

			if (password1 != password2)
			{
				ViewBag.Hata = "Şifreler uyuşmuyor.";
				LogAction(userIP, actionName, "Password mismatch", false);
				return View();
			}

			var existingCustomer = _context.Customers.SingleOrDefault(c => c.Email == mail);
			if (existingCustomer != null)
			{
				ViewBag.Hata = "Bu e-posta adresi zaten kullanılıyor.";
				LogAction(userIP, actionName, "Email already in use", false);
				return View();
			}

			var recentAttempts = _context.ActionTracks
				.Where(at => at.ActionIP == userIP && at.Date >= DateTime.Now.AddMinutes(-10) && at.ActionName == "register-failed")
				.Count();

			if (recentAttempts >= 3)
			{
				LogAction(userIP, actionName, "Too many attempts", false);
				ViewBag.Hata = "Çok fazla kayıt denemesi. Lütfen daha sonra tekrar deneyiniz.";
				return View();
			}

			using (var transaction = _context.Database.BeginTransaction())
			{
				try
				{
					var activationCode = Guid.NewGuid();
					var customer = new Customer
					{
						Email = mail,
						FirstName = firstName,
						LastName = lastName,
						Phone = phone,
						PasswordHash = Convert.ToBase64String(Encoding.UTF8.GetBytes(password1)),
						Active = false,
						ActivationCode = activationCode,
						ActivationExpiry = DateTime.UtcNow.AddHours(24) // Aktivasyon süresi 24 saat
					};

					_context.Customers.Add(customer);
					_context.SaveChanges();

					SendActivationEmail(customer);

					RemovePreviousFailedAttempts(userIP, actionName);
					transaction.Commit();

					ViewBag.Hata = "Kayıt başarılı! Aktivasyon maili gönderildi. Lütfen mailinizi kontrol edin.";
				}
				catch (Exception ex)
				{
					transaction.Rollback();
					LogAction(userIP, actionName, $"Registration failed: {ex.Message}", false);
					ViewBag.Hata = $"Kayıt sırasında bir hata oluştu: {ex.Message}";
				}
			}

			return View();
		}

		[HttpGet]
		public ActionResult Activate(Guid code)
		{
			var customer = _context.Customers.SingleOrDefault(c => c.ActivationCode == code);

			if (customer != null && !customer.Active && customer.ActivationExpiry > DateTime.UtcNow)
			{
				customer.Active = true;
				_context.SaveChanges();
				ViewBag.Success = "Hesabınız başarıyla aktif edildi!";
			}
			else
			{
				ViewBag.Hata = "Aktivasyon kodu geçersiz, süresi dolmuş veya hesap zaten aktif.";
			}

			return View();
		}

		[HttpGet]
		public IActionResult ResetPassword(string code)
		{
			var user = _context.Customers.FirstOrDefault(a => a.ResetPasswordCode == code);
			if (user == null || user.ResetPasswordCodeExpiry < DateTime.Now || !user.Active)
			{
				ViewBag.Error = "Şifre sıfırlama kodu geçersiz, süresi dolmuş veya hesap aktif değil.";
				return View("Error");
			}
			ViewBag.Code = code;  // Passing the code to the view through ViewBag
			return View();
		}

		[HttpPost]
		public IActionResult ResetPassword(string code, string newPassword)
		{
			string xmlFilePath = Path.Combine(_hostingEnvironment.WebRootPath, "settings.xml");
			if (System.IO.File.Exists(xmlFilePath))
			{
				XElement settings = XElement.Load(xmlFilePath);
				ViewBag.SiteTitle = settings.Element("Title")?.Value; // Site ismini ViewBag ile taşı
			}
			var user = _context.Customers.FirstOrDefault(a => a.ResetPasswordCode == code);
			if (user == null || user.ResetPasswordCodeExpiry < DateTime.Now || !user.Active)
			{
				ViewBag.Error = "Şifre sıfırlama kodu geçersiz, süresi dolmuş veya hesap aktif değil.";
				return View("Error");
			}

			user.PasswordHash = Convert.ToBase64String(Encoding.UTF8.GetBytes(newPassword));
			user.ResetPasswordCode = null; // Reset code'nu sıfırla
			_context.SaveChanges();

			ViewBag.Message = "Şifreniz başarıyla sıfırlandı.";
			return RedirectToAction("Login");
		}

		public IActionResult ForgetPassword()
		{
			return View();
		}

		[HttpPost]
		public ActionResult ForgetPassword(string email)
		{
			var user = _context.Customers.FirstOrDefault(a => a.Email == email);
			if (user == null || !user.Active)
			{
				ViewBag.Error = "Böyle bir kullanıcı bulunamadı veya hesap aktif değil.";
				return View();
			}
			// Şifre sıfırlama sıklığını kontrol et
			var lastReset = _context.ActionTracks
				.Where(a => a.ActionIP == email && a.ActionName == "PasswordReset")
				.OrderByDescending(a => a.Date)
				.FirstOrDefault();

			if (lastReset != null && (DateTime.Now - lastReset.Date).TotalDays < 10)
			{
				ViewBag.Error = "Şifre sıfırlama işlemi sadece 10 günde bir yapılabilir.";
				return View();
			}

			try
			{
				user.ResetPasswordCode = Guid.NewGuid().ToString();
				user.ResetPasswordCodeExpiry = DateTime.Now.AddMinutes(60); // Kodun geçerlilik süresi 60 dakika
				_context.SaveChanges();

				var link = Url.Action("ResetPassword", "AdmPanel", new { code = user.ResetPasswordCode }, protocol: HttpContext.Request.Scheme);
				_emailService.SendPasswordResetEmail(user.Email, link);
				_smsService.SendPasswordResetSms(user.Phone, link);

				_context.ActionTracks.Add(new ActionTrack
				{
					ActionIP = email, // E-posta adresini ActionIP olarak kullanıyoruz
					ActionName = "PasswordReset",
					Date = DateTime.Now
				});
				_context.SaveChanges();

				ViewBag.Message = "Şifre sıfırlama linki e-posta adresinize gönderildi. Lütfen mailinizi inceleyiniz.";
			}
			catch (Exception ex)
			{
				// Log the exception details here to analyze what went wrong
				ViewBag.Error = "E-posta gönderilirken bir hata oluştu. Lütfen işlemi tekrar deneyiniz.";
			}

			return View();
		}

		[HttpPost]
		public ActionResult ForgotPassword(string email)
		{
			var user = _context.Customers.FirstOrDefault(a => a.Email == email);
			if (user == null || !user.Active)
			{
				ViewBag.Error = "Böyle bir kullanıcı bulunamadı veya hesap aktif değil.";
				return View();
			}

			user.ResetPasswordCode = Guid.NewGuid().ToString();
			user.ResetPasswordCodeExpiry = DateTime.Now.AddMinutes(60); // Kodun geçerlilik süresi 60 dakika
			_context.SaveChanges();

			var link = Url.Action("ResetPassword", "AdmPanel", new { code = user.ResetPasswordCode }, protocol: HttpContext.Request.Scheme);
			_emailService.SendPasswordResetEmail(user.Email, link);
			_smsService.SendPasswordResetSms(user.Phone, link);

			ViewBag.Message = "Şifre sıfırlama linki e-posta adresinize gönderildi.";
			return View();
		}

		#endregion

		public IActionResult UserSettings()
		{

			int? userId = HttpContext.Session.GetInt32("UserId");
			string userRole = HttpContext.Session.GetString("UserRole");
			string siteName = HttpContext.Session.GetString("SiteName");
			if (siteName != LoadSiteTitleFromXml())
			{
				return RedirectToAction("Home", "AdmPanel");
			}
			if (!userId.HasValue || string.IsNullOrEmpty(userRole))
			{
				// Eğer kullanıcı ID veya rol belirtilmemişse, kullanıcıyı login sayfasına yönlendir
				return RedirectToAction("Login", "AdmPanel");
			}
			string xmlFilePath = Path.Combine(_hostingEnvironment.WebRootPath, "settings.xml");
			if (System.IO.File.Exists(xmlFilePath))
			{
				XElement settings = XElement.Load(xmlFilePath);
				ViewBag.SiteTitle = settings.Element("Title")?.Value; // Site ismini ViewBag ile taşı
			}
			switch (userRole)
			{
				case "Head Admin":
				case "Admin":
				case "Staff":
					var user = _context.Staffs.FirstOrDefault(u => u.StaffID == userId.Value);
					if (user == null)
					{
						// Kullanıcı bulunamadıysa oturumu sonlandır ve login sayfasına yönlendir
						HttpContext.Session.Clear();
						return RedirectToAction("Login", "AdmPanel");
					}
					if (!string.IsNullOrEmpty(user.FirstName) && user.FirstName.Length >= 2)
					{
						ViewBag.UserInitials = user.FirstName.Substring(0, 2); // İlk iki harfi al
					}
					else if (!string.IsNullOrEmpty(user.FirstName))
					{
						ViewBag.UserInitials = user.FirstName; // İsim iki harften kısa ise tüm ismi kullan
					}
					else
					{
						ViewBag.UserInitials = ""; // İsim boş veya null ise boş bir string ata
					}
					ViewBag.UserID = userId;
					ViewBag.UserMail = user.Email;
					ViewBag.UserPosition = user.Position;
					ViewBag.UserRole = userRole;
					ViewBag.UserName = user.FirstName + " " + user.LastName;
					return View(user); // Staff profili için View döndür
				case "Customer":
					// Customer için farklı bir yönlendirme yapılabilir veya burada hata yönetimi
					return RedirectToAction("Index", "Home"); // Customer ana sayfasına yönlendir
				default:
					// Rol tanımlanmamışsa veya tanımlı bir role uymuyorsa login sayfasına yönlendir
					return RedirectToAction("Login", "AdmPanel");
			}
		}

		[HttpPost]
		public async Task<IActionResult> UpdateUserSettings(Staff updatedUser, IFormFile photo)
		{
			var user = _context.Staffs.Find(updatedUser.StaffID);
			if (user == null)
			{
				TempData["ErrorMessage"] = "Kullanıcı bulunamadı.";
				return RedirectToAction("ErrorPage");
			}

			user.FirstName = updatedUser.FirstName;
			user.LastName = updatedUser.LastName;
			user.Email = updatedUser.Email;
			user.Phone = updatedUser.Phone;

			if (photo != null && photo.ContentType.ToLower().Contains("image/png"))
			{
				var fileName = $"{user.StaffID}.png";
				var filePath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "img", "staff", fileName);

				using (var memoryStream = new MemoryStream())
				{
					await photo.CopyToAsync(memoryStream);
					using (var img = System.Drawing.Image.FromStream(memoryStream))
					{
						// Orjinal görseli belirtilen boyutlara uygun olarak yeniden boyutlandır
						var resizedImg = new Bitmap(img, new System.Drawing.Size(370, 310));

						// Yeniden boyutlandırılmış görseli dosyaya kaydet
						using (var stream = new FileStream(filePath, FileMode.Create))
						{
							resizedImg.Save(stream, ImageFormat.Png);
						}
					}
				}
			}

			_context.Update(user);
			await _context.SaveChangesAsync();

			return RedirectToAction("UserSettings", "AdmPanel");
		}

		public IActionResult MyProfile()
		{
			int? userId = HttpContext.Session.GetInt32("UserId");
			string userRole = HttpContext.Session.GetString("UserRole");
			string siteName = HttpContext.Session.GetString("SiteName");
			if (siteName != LoadSiteTitleFromXml())
			{
				return RedirectToAction("Home", "AdmPanel");
			}
			if (!userId.HasValue || string.IsNullOrEmpty(userRole))
			{
				// Eğer kullanıcı ID veya rol belirtilmemişse, kullanıcıyı login sayfasına yönlendir
				return RedirectToAction("Login", "AdmPanel");
			}
			string xmlFilePath = Path.Combine(_hostingEnvironment.WebRootPath, "settings.xml");
			if (System.IO.File.Exists(xmlFilePath))
			{
				XElement settings = XElement.Load(xmlFilePath);
				ViewBag.SiteTitle = settings.Element("Title")?.Value; // Site ismini ViewBag ile taşı
			}
			switch (userRole)
			{
				case "Head Admin":
				case "Admin":
				case "Staff":
					var user = _context.Staffs.FirstOrDefault(u => u.StaffID == userId.Value);
					if (user == null)
					{
						// Kullanıcı bulunamadıysa oturumu sonlandır ve login sayfasına yönlendir
						HttpContext.Session.Clear();
						return RedirectToAction("Login", "AdmPanel");
					}
					if (!string.IsNullOrEmpty(user.FirstName) && user.FirstName.Length >= 2)
					{
						ViewBag.UserInitials = user.FirstName.Substring(0, 2); // İlk iki harfi al
					}
					else if (!string.IsNullOrEmpty(user.FirstName))
					{
						ViewBag.UserInitials = user.FirstName; // İsim iki harften kısa ise tüm ismi kullan
					}
					else
					{
						ViewBag.UserInitials = ""; // İsim boş veya null ise boş bir string ata
					}
					ViewBag.UserID = userId;
					ViewBag.UserMail = user.Email;
					ViewBag.UserPosition = user.Position;
					ViewBag.UserRole = userRole;
					ViewBag.UserName = user.FirstName + " " + user.LastName;
					return View(user); // Staff profili için View döndür
				case "Customer":
					// Customer için farklı bir yönlendirme yapılabilir veya burada hata yönetimi
					return RedirectToAction("Index", "Home"); // Customer ana sayfasına yönlendir
				default:
					// Rol tanımlanmamışsa veya tanımlı bir role uymuyorsa login sayfasına yönlendir
					return RedirectToAction("Login", "AdmPanel");
			}
		}

		public IActionResult WorkDays(int id)
		{
			if (id == 1)
			{
				// Eğer id 1 ise kullanıcıya erişimi engelle
				return RedirectToAction("Index", "AdmPanel");
			}
			int? userId = HttpContext.Session.GetInt32("UserId");
			string userRole = HttpContext.Session.GetString("UserRole");
			string siteName = HttpContext.Session.GetString("SiteName");
			if (siteName != LoadSiteTitleFromXml())
			{
				return RedirectToAction("Index", "AdmPanel");
			}
			if (!userId.HasValue || string.IsNullOrEmpty(userRole))
			{
				// Eğer kullanıcı ID veya rol belirtilmemişse, kullanıcıyı login sayfasına yönlendir
				return RedirectToAction("Login", "AdmPanel");
			}
			string xmlFilePath = Path.Combine(_hostingEnvironment.WebRootPath, "settings.xml");
			if (System.IO.File.Exists(xmlFilePath))
			{
				XElement settings = XElement.Load(xmlFilePath);
				ViewBag.SiteTitle = settings.Element("Title")?.Value; // Site ismini ViewBag ile taşı
			}
			ViewBag.UserID = userId;
			switch (userRole)
			{
				case "Head Admin":
				case "Admin":
				case "Staff":
					var user = _context.Staffs.FirstOrDefault(u => u.StaffID == userId.Value);
					var staff = _context.Staffs.Include(s => s.WeeklyAvailabilities).FirstOrDefault(u => u.StaffID == id);
					if (user == null || staff == null)
					{
						// Kullanıcı bulunamadıysa oturumu sonlandır ve login sayfasına yönlendir
						HttpContext.Session.Clear();
						return RedirectToAction("Login", "AdmPanel");
					}
					if (!string.IsNullOrEmpty(user.FirstName) && user.FirstName.Length >= 2)
					{
						ViewBag.UserInitials = user.FirstName.Substring(0, 2); // İlk iki harfi al
					}
					else if (!string.IsNullOrEmpty(user.FirstName))
					{
						ViewBag.UserInitials = user.FirstName; // İsim iki harften kısa ise tüm ismi kullan
					}
					else
					{
						ViewBag.UserInitials = ""; // İsim boş veya null ise boş bir string ata
					}
					ViewBag.UserID = userId;
					ViewBag.UserRole = userRole;
					ViewBag.UserMail = staff.Email;
					ViewBag.UserPosition = staff.Position;
					ViewBag.UserName = staff.FirstName + " " + staff.LastName;
					ViewBag.WorkDays = staff.WeeklyAvailabilities.Select(w => new
					{
						DayOfWeek = w.DayOfWeek,
						StartTime = (int)w.StartTime.TotalMinutes,  // Dakika cinsinden
						EndTime = (int)w.EndTime.TotalMinutes       // Dakika cinsinden
					}).ToList();

					return View(staff);
				case "Customer":
					return RedirectToAction("Index", "Home"); // Customer ana sayfasına yönlendir
				default:
					return RedirectToAction("Login", "AdmPanel");
			}
		}

		[HttpPost]
		public IActionResult WorkDays(int id, List<WeeklyAvailability> availabilities)
		{
			int? userId = HttpContext.Session.GetInt32("UserId");
			if (!userId.HasValue)
			{
				return RedirectToAction("Login");
			}
			string siteName = HttpContext.Session.GetString("SiteName");
			if (siteName != LoadSiteTitleFromXml())
			{
				return RedirectToAction("Home", "AdmPanel");
			}
			var staff = _context.Staffs.Include(s => s.WeeklyAvailabilities).FirstOrDefault(u => u.StaffID == id);
			if (staff == null)
			{
				return NotFound("Personel bulunamadı.");
			}

			_context.WeeklyAvailabilities.RemoveRange(staff.WeeklyAvailabilities);

			foreach (var availability in availabilities)
			{
				availability.StaffID = staff.StaffID;

				// TimeSpan string'ini işle ve doğru dakika değerini elde et
				int minutesStart = ConvertTimeSpanStringToMinutes(availability.StartTime.ToString());
				int minutesEnd = ConvertTimeSpanStringToMinutes(availability.EndTime.ToString());

				// Dakika değerlerini TimeSpan'a çevir
				availability.StartTime = TimeSpan.FromMinutes(minutesStart);
				availability.EndTime = TimeSpan.FromMinutes(minutesEnd);

				_context.WeeklyAvailabilities.Add(availability);
			}

			try
			{
				_context.SaveChanges();
				return RedirectToAction("WorkDays");
			}
			catch (Exception ex)
			{
				// Hata yönetimi
				return View("Error", new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
			}
		}

		public IActionResult GelirGider()
		{
			int? userId = HttpContext.Session.GetInt32("UserId");
			string userRole = HttpContext.Session.GetString("UserRole");
			string siteName = HttpContext.Session.GetString("SiteName");
			if (siteName != LoadSiteTitleFromXml())
			{
				return RedirectToAction("Home", "AdmPanel");
			}
			if (!userId.HasValue || string.IsNullOrEmpty(userRole))
			{
				// Eğer kullanıcı ID veya rol belirtilmemişse, kullanıcıyı login sayfasına yönlendir
				return RedirectToAction("Login", "AdmPanel");
			}
			var user = _context.Staffs.FirstOrDefault(u => u.StaffID == userId.Value);
			if (user == null)
			{
				// Kullanıcı bulunamadıysa oturumu sonlandır ve login sayfasına yönlendir
				HttpContext.Session.Clear();
				return RedirectToAction("Login", "AdmPanel");
			}
			string xmlFilePath = Path.Combine(_hostingEnvironment.WebRootPath, "settings.xml");
			if (System.IO.File.Exists(xmlFilePath))
			{
				XElement settings = XElement.Load(xmlFilePath);
				ViewBag.SiteTitle = settings.Element("Title")?.Value; // Site ismini ViewBag ile taşı
			}
			if (!string.IsNullOrEmpty(user.FirstName) && user.FirstName.Length >= 2)
			{
				ViewBag.UserInitials = user.FirstName.Substring(0, 2); // İlk iki harfi al
			}
			else if (!string.IsNullOrEmpty(user.FirstName))
			{
				ViewBag.UserInitials = user.FirstName; // İsim iki harften kısa ise tüm ismi kullan
			}
			else
			{
				ViewBag.UserInitials = ""; // İsim boş veya null ise boş bir string ata
			}
			ViewBag.UserID = userId;
			ViewBag.UserRole = userRole;
			ViewBag.UserName = user.FirstName + " " + user.LastName;
			ViewBag.UserPosition = user.Position;
			switch (userRole)
			{
				case "Head Admin":
				case "Admin":
					var gelirGiderList = _context.GelirGiders.ToList();
					return View(gelirGiderList);
				case "Staff":
					return RedirectToAction("Login", "AdmPanel"); // Staff profili için View döndür
				case "Customer":
					// Customer için farklı bir yönlendirme yapılabilir veya burada hata yönetimi
					return RedirectToAction("Index", "Home"); // Customer ana sayfasına yönlendir
				default:
					// Rol tanımlanmamışsa veya tanımlı bir role uymuyorsa login sayfasına yönlendir
					return RedirectToAction("Login", "AdmPanel");
			}
		}

		[HttpPost]
		public async Task<IActionResult> GelirGider(int Month, int Year, int Gelir, int Gider)
		{
			int? userId = HttpContext.Session.GetInt32("UserId");
			string userRole = HttpContext.Session.GetString("UserRole");
			string siteName = HttpContext.Session.GetString("SiteName");
			if (siteName != LoadSiteTitleFromXml())
			{
				return RedirectToAction("Home", "AdmPanel");
			}
			if (!userId.HasValue || string.IsNullOrEmpty(userRole))
			{
				return RedirectToAction("Login", "AdmPanel");
			}
			var user = await _context.Staffs.FirstOrDefaultAsync(u => u.StaffID == userId.Value);
			if (user == null)
			{
				// Kullanıcı bulunamadıysa oturumu sonlandır ve login sayfasına yönlendir
				HttpContext.Session.Clear();
				return RedirectToAction("Login", "AdmPanel");
			}
			switch (userRole)
			{
				case "Head Admin":
				case "Admin":
					try
					{
						// Create date from year and month
						var entryDate = new DateTime(Year, Month, 1);

						// Check if an entry already exists
						var existingEntry = await _context.GelirGiders.FirstOrDefaultAsync(g => g.Tarih == entryDate);
						if (existingEntry != null)
						{
							return RedirectToAction("GelirGider", "AdmPanel");
						}

						// Create new entry
						var newEntry = new GelirGider
						{
							Tarih = entryDate,
							Gelir = Gelir,
							Gider = Gider
						};

						// Add to database
						_context.GelirGiders.Add(newEntry);
						await _context.SaveChangesAsync();

						return RedirectToAction("GelirGider", "AdmPanel");
					}
					catch (Exception ex)
					{
						return RedirectToAction("GelirGider", "AdmPanel");
					}
				case "Staff":
					return RedirectToAction("Login", "AdmPanel"); // Staff profili için View döndür
				case "Customer":
					// Customer için farklı bir yönlendirme yapılabilir veya burada hata yönetimi
					return RedirectToAction("Index", "Home"); // Customer ana sayfasına yönlendir
				default:
					// Rol tanımlanmamışsa veya tanımlı bir role uymuyorsa login sayfasına yönlendir
					return RedirectToAction("Login", "AdmPanel");
			}
		}

		public IActionResult GelirGiderEdit(int id)
		{
			int? userId = HttpContext.Session.GetInt32("UserId");
			string userRole = HttpContext.Session.GetString("UserRole");
			string siteName = HttpContext.Session.GetString("SiteName");
			if (siteName != LoadSiteTitleFromXml())
			{
				return RedirectToAction("Home", "AdmPanel");
			}
			if (!userId.HasValue || string.IsNullOrEmpty(userRole))
			{
				// Eğer kullanıcı ID veya rol belirtilmemişse, kullanıcıyı login sayfasına yönlendir
				return RedirectToAction("Login", "AdmPanel");
			}
			string xmlFilePath = Path.Combine(_hostingEnvironment.WebRootPath, "settings.xml");
			if (System.IO.File.Exists(xmlFilePath))
			{
				XElement settings = XElement.Load(xmlFilePath);
				ViewBag.SiteTitle = settings.Element("Title")?.Value; // Site ismini ViewBag ile taşı
			}
			ViewBag.UserID = userId;
			switch (userRole)
			{
				case "Head Admin":
				case "Admin":
					var user = _context.Staffs.FirstOrDefault(u => u.StaffID == userId.Value);
					if (user == null)
					{
						// Kullanıcı bulunamadıysa oturumu sonlandır ve login sayfasına yönlendir
						HttpContext.Session.Clear();
						return RedirectToAction("Login", "AdmPanel");
					}
					if (!string.IsNullOrEmpty(user.FirstName) && user.FirstName.Length >= 2)
					{
						ViewBag.UserInitials = user.FirstName.Substring(0, 2); // İlk iki harfi al
					}
					else if (!string.IsNullOrEmpty(user.FirstName))
					{
						ViewBag.UserInitials = user.FirstName; // İsim iki harften kısa ise tüm ismi kullan
					}
					else
					{
						ViewBag.UserInitials = ""; // İsim boş veya null ise boş bir string ata
					}
					ViewBag.UserPosition = user.Position;
					ViewBag.UserName = user.FirstName + " " + user.LastName;
					var gelirGider = _context.GelirGiders.FirstOrDefault(g => g.ID == id);
					if (gelirGider == null)
					{
						return NotFound();
					}
					return View(gelirGider);
				case "Staff":
					return RedirectToAction("Login", "AdmPanel"); // Staff profili için View döndür
				case "Customer":
					// Customer için farklı bir yönlendirme yapılabilir veya burada hata yönetimi
					return RedirectToAction("Index", "Home"); // Customer ana sayfasına yönlendir
				default:
					// Rol tanımlanmamışsa veya tanımlı bir role uymuyorsa login sayfasına yönlendir
					return RedirectToAction("Login", "AdmPanel");
			}
		}

		[HttpPost]
		public IActionResult GelirGiderEdit(GelirGider model)
		{
			int? userId = HttpContext.Session.GetInt32("UserId");
			string userRole = HttpContext.Session.GetString("UserRole");
			string siteName = HttpContext.Session.GetString("SiteName");
			if (siteName != LoadSiteTitleFromXml())
			{
				return RedirectToAction("Home", "AdmPanel");
			}
			if (!userId.HasValue || string.IsNullOrEmpty(userRole))
			{
				// Eğer kullanıcı ID veya rol belirtilmemişse, kullanıcıyı login sayfasına yönlendir
				return RedirectToAction("Login", "AdmPanel");
			}
			var user = _context.Staffs.FirstOrDefault(u => u.StaffID == userId.Value);
			if (user == null)
			{
				// Kullanıcı bulunamadıysa oturumu sonlandır ve login sayfasına yönlendir
				HttpContext.Session.Clear();
				return RedirectToAction("Login", "AdmPanel");
			}
			if (!string.IsNullOrEmpty(user.FirstName) && user.FirstName.Length >= 2)
			{
				ViewBag.UserInitials = user.FirstName.Substring(0, 2); // İlk iki harfi al
			}
			else if (!string.IsNullOrEmpty(user.FirstName))
			{
				ViewBag.UserInitials = user.FirstName; // İsim iki harften kısa ise tüm ismi kullan
			}
			else
			{
				ViewBag.UserInitials = ""; // İsim boş veya null ise boş bir string ata
			}
			ViewBag.UserRole = userRole;
			ViewBag.UserName = user.FirstName + " " + user.LastName;
			ViewBag.UserPosition = user.Position;
			switch (userRole)
			{
				case "Head Admin":
					if (ModelState.IsValid)
					{
						var existingEntry = _context.GelirGiders.FirstOrDefault(g => g.ID == model.ID);
						if (existingEntry != null)
						{
							// Aynı yıl ve aya ait başka bir veri olup olmadığını kontrol et
							var duplicateEntry = _context.GelirGiders
								.FirstOrDefault(g => g.ID != model.ID && g.Tarih.Year == model.Tarih.Year && g.Tarih.Month == model.Tarih.Month);

							if (duplicateEntry != null)
							{
								ViewBag.Hata = "Bu ayda bir kayıt zaten bulunuyor !";
								return View(model);
							}

							existingEntry.Tarih = model.Tarih; // Tarihi güncelle
							existingEntry.Gelir = model.Gelir;
							existingEntry.Gider = model.Gider;
							_context.SaveChanges();
							return RedirectToAction("GelirGider", "AdmPanel");
						}
						return NotFound();
					}
					return View(model);
				case "Admin":
					return RedirectToAction("Login", "AdmPanel");
				case "Staff":
					return RedirectToAction("Login", "AdmPanel"); // Staff profili için View döndür
				default:
					// Rol tanımlanmamışsa veya tanımlı bir role uymuyorsa login sayfasına yönlendir
					return RedirectToAction("Login", "AdmPanel");
			}

		}

		public IActionResult Staff()
		{
			int? userId = HttpContext.Session.GetInt32("UserId");
			string userRole = HttpContext.Session.GetString("UserRole");
			string siteName = HttpContext.Session.GetString("SiteName");
			if (siteName != LoadSiteTitleFromXml())
			{
				return RedirectToAction("Home", "AdmPanel");
			}
			if (!userId.HasValue || string.IsNullOrEmpty(userRole))
			{
				// Eğer kullanıcı ID veya rol belirtilmemişse, kullanıcıyı login sayfasına yönlendir
				return RedirectToAction("Login", "AdmPanel");
			}
			var user = _context.Staffs.FirstOrDefault(u => u.StaffID == userId.Value);
			if (user == null)
			{
				// Kullanıcı bulunamadıysa oturumu sonlandır ve login sayfasına yönlendir
				HttpContext.Session.Clear();
				return RedirectToAction("Login", "AdmPanel");
			}
			if (!string.IsNullOrEmpty(user.FirstName) && user.FirstName.Length >= 2)
			{
				ViewBag.UserInitials = user.FirstName.Substring(0, 2); // İlk iki harfi al
			}
			else if (!string.IsNullOrEmpty(user.FirstName))
			{
				ViewBag.UserInitials = user.FirstName; // İsim iki harften kısa ise tüm ismi kullan
			}
			else
			{
				ViewBag.UserInitials = ""; // İsim boş veya null ise boş bir string ata
			}
			string xmlFilePath = Path.Combine(_hostingEnvironment.WebRootPath, "settings.xml");
			if (System.IO.File.Exists(xmlFilePath))
			{
				XElement settings = XElement.Load(xmlFilePath);
				ViewBag.SiteTitle = settings.Element("Title")?.Value; // Site ismini ViewBag ile taşı
			}
			ViewBag.UserRole = userRole;
			ViewBag.UserID = userId;
			ViewBag.UserName = user.FirstName + " " + user.LastName;
			ViewBag.UserPosition = user.Position;
			switch (userRole)
			{
				case "Head Admin":
				case "Admin":
					var staff = _context.Staffs
						.Include(s => s.Role)
						.Where(s => s.status == true && s.StaffID != 1)
						.ToList();
					return View(staff);

				case "Staff":
					return RedirectToAction("Login", "AdmPanel"); // Staff profili için View döndür
				default:
					// Rol tanımlanmamışsa veya tanımlı bir role uymuyorsa login sayfasına yönlendir
					return RedirectToAction("Login", "AdmPanel");
			}
		}

		public IActionResult Services()
		{
			int? userId = HttpContext.Session.GetInt32("UserId");
			string userRole = HttpContext.Session.GetString("UserRole");
			string siteName = HttpContext.Session.GetString("SiteName");
			if (siteName != LoadSiteTitleFromXml())
			{
				return RedirectToAction("Home", "AdmPanel");
			}
			if (!userId.HasValue || string.IsNullOrEmpty(userRole))
			{
				// Eğer kullanıcı ID veya rol belirtilmemişse, kullanıcıyı login sayfasına yönlendir
				return RedirectToAction("Login", "AdmPanel");
			}
			var user = _context.Staffs.FirstOrDefault(u => u.StaffID == userId.Value);
			if (user == null)
			{
				// Kullanıcı bulunamadıysa oturumu sonlandır ve login sayfasına yönlendir
				HttpContext.Session.Clear();
				return RedirectToAction("Login", "AdmPanel");
			}
			if (!string.IsNullOrEmpty(user.FirstName) && user.FirstName.Length >= 2)
			{
				ViewBag.UserInitials = user.FirstName.Substring(0, 2); // İlk iki harfi al
			}
			else if (!string.IsNullOrEmpty(user.FirstName))
			{
				ViewBag.UserInitials = user.FirstName; // İsim iki harften kısa ise tüm ismi kullan
			}
			else
			{
				ViewBag.UserInitials = ""; // İsim boş veya null ise boş bir string ata
			}
			string xmlFilePath = Path.Combine(_hostingEnvironment.WebRootPath, "settings.xml");
			if (System.IO.File.Exists(xmlFilePath))
			{
				XElement settings = XElement.Load(xmlFilePath);
				ViewBag.SiteTitle = settings.Element("Title")?.Value; // Site ismini ViewBag ile taşı
			}
			ViewBag.UserRole = userRole;
			ViewBag.UserID = userId;
			ViewBag.UserName = user.FirstName + " " + user.LastName;
			ViewBag.UserPosition = user.Position;
			switch (userRole)
			{
				case "Head Admin":
				case "Admin":
					var services = _context.Services.ToList();
					return View(services);

				case "Staff":
					return RedirectToAction("Login", "AdmPanel");
				default:
					// Rol tanımlanmamışsa veya tanımlı bir role uymuyorsa login sayfasına yönlendir
					return RedirectToAction("Login", "AdmPanel");
			}
		}

		[HttpPost]
		public async Task<IActionResult> AddService(Service model, IFormFile photo)
		{
			if (ModelState.IsValid)
			{
				_context.Services.Add(model);
				await _context.SaveChangesAsync(); // Veritabanına kaydet

				if (photo != null && photo.Length > 0 && photo.ContentType == "image/png")
				{
					var directoryPath = Path.Combine(_hostingEnvironment.WebRootPath, "img", "services");
					var fileName = $"{model.ServiceID}.png"; // ID'ye dayalı dosya adı
					var filePath = Path.Combine(directoryPath, fileName);

					using (var image = SixLabors.ImageSharp.Image.Load(photo.OpenReadStream()))
					{
						image.Mutate(x => x.Resize(370, 310)); // Resmi 370x310 boyutlarına ayarla

						// Dosyayı açarken FileShare.None ile kilitle
						using (var fs = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.None))
						{
							await image.SaveAsPngAsync(fs); // PNG formatında kaydet
						}
					}
				}

				return RedirectToAction("Services", "AdmPanel"); // Yönlendirme
			}

			return RedirectToAction("Services", "AdmPanel");
		}

		public IActionResult ServiceEdit(int id)
		{
			string xmlFilePath = Path.Combine(_hostingEnvironment.WebRootPath, "settings.xml");
			if (System.IO.File.Exists(xmlFilePath))
			{
				XElement settings = XElement.Load(xmlFilePath);
				ViewBag.SiteTitle = settings.Element("Title")?.Value; // Site ismini ViewBag ile taşı
			}
			int? userId = HttpContext.Session.GetInt32("UserId");
			string userRole = HttpContext.Session.GetString("UserRole");
			string siteName = HttpContext.Session.GetString("SiteName");
			if (siteName != LoadSiteTitleFromXml())
			{
				return RedirectToAction("Home", "AdmPanel");
			}
			if (!userId.HasValue || string.IsNullOrEmpty(userRole))
			{
				// Eğer kullanıcı ID veya rol belirtilmemişse, kullanıcıyı login sayfasına yönlendir
				return RedirectToAction("Login", "AdmPanel");
			}


			ViewBag.UserID = userId;
			switch (userRole)
			{
				case "Head Admin":
				case "Admin":
					var user = _context.Staffs.FirstOrDefault(u => u.StaffID == userId.Value);
					if (user == null)
					{
						// Kullanıcı bulunamadıysa oturumu sonlandır ve login sayfasına yönlendir
						HttpContext.Session.Clear();
						return RedirectToAction("Login", "AdmPanel");
					}
					if (!string.IsNullOrEmpty(user.FirstName) && user.FirstName.Length >= 2)
					{
						ViewBag.UserInitials = user.FirstName.Substring(0, 2); // İlk iki harfi al
					}
					else if (!string.IsNullOrEmpty(user.FirstName))
					{
						ViewBag.UserInitials = user.FirstName; // İsim iki harften kısa ise tüm ismi kullan
					}
					else
					{
						ViewBag.UserInitials = ""; // İsim boş veya null ise boş bir string ata
					}
					ViewBag.UserPosition = user.Position;
					ViewBag.UserName = user.FirstName + " " + user.LastName;
					ViewBag.UserRole = userRole;
					var service = _context.Services.FirstOrDefault(g => g.ServiceID == id);
					return View(service);
				case "Staff":
					return RedirectToAction("Login", "AdmPanel");
				case "Customer":
					// Customer için farklı bir yönlendirme yapılabilir veya burada hata yönetimi
					return RedirectToAction("Index", "Home"); // Customer ana sayfasına yönlendir
				default:
					// Rol tanımlanmamışsa veya tanımlı bir role uymuyorsa login sayfasına yönlendir
					return RedirectToAction("Login", "AdmPanel");
			}
		}

		[HttpGet]
		public async Task<IActionResult> ServiceDelete(int id)
		{
			var service = await _context.Services.FindAsync(id);
			if (service == null)
			{
				return NotFound();
			}

			// İlgili görseli sil
			var filePath = Path.Combine(_hostingEnvironment.WebRootPath, "img", "services", $"{service.ServiceID}.png");
			if (System.IO.File.Exists(filePath))
			{
				System.IO.File.Delete(filePath);
			}

			_context.Services.Remove(service);
			await _context.SaveChangesAsync();

			return RedirectToAction("Services", "AdmPanel");
		}

		[HttpPost]
		public async Task<IActionResult> ServiceEdit(int id, Service updatedService, IFormFile ImageFile)
		{
			ModelState.Remove("ImageFile");
			if (!ModelState.IsValid)
			{
				return View(updatedService); // Eğer model geçersizse, formu tekrar göster
			}

			var existingService = await _context.Services.FindAsync(id);
			if (existingService == null)
			{
				return NotFound(); // Hizmet bulunamazsa, hata döndür
			}

			// Güncellemeleri yap
			existingService.Description = updatedService.Description;
			existingService.Price = updatedService.Price;

			// Dosya işleme
			if (ImageFile != null && ImageFile.Length > 0 && ImageFile.ContentType == "image/png")
			{
				var directoryPath = Path.Combine(_hostingEnvironment.WebRootPath, "img", "services");
				var filePath = Path.Combine(directoryPath, $"{existingService.ServiceID}.png");

				if (!Directory.Exists(directoryPath))
				{
					Directory.CreateDirectory(directoryPath); // Eğer klasör yoksa, oluştur
				}

				using (var image = SixLabors.ImageSharp.Image.Load(ImageFile.OpenReadStream()))
				{
					image.Mutate(x => x.Resize(370, 310)); // Resmi istenen boyutlara ayarla
					await image.SaveAsPngAsync(filePath); // PNG formatında kaydet
				}
			}

			_context.Update(existingService);
			await _context.SaveChangesAsync();

			return RedirectToAction("Services", "AdmPanel"); // İşlem tamamlandıktan sonra hizmet listesi sayfasına yönlendir
		}

		public IActionResult Users()
		{
			int? userId = HttpContext.Session.GetInt32("UserId");
			string userRole = HttpContext.Session.GetString("UserRole");
			string siteName = HttpContext.Session.GetString("SiteName");
			if (siteName != LoadSiteTitleFromXml())
			{
				return RedirectToAction("Home", "AdmPanel");
			}
			if (!userId.HasValue || string.IsNullOrEmpty(userRole))
			{
				// Eğer kullanıcı ID veya rol belirtilmemişse, kullanıcıyı login sayfasına yönlendir
				return RedirectToAction("Login", "AdmPanel");
			}
			var user = _context.Staffs.FirstOrDefault(u => u.StaffID == userId.Value);
			if (user == null)
			{
				// Kullanıcı bulunamadıysa oturumu sonlandır ve login sayfasına yönlendir
				HttpContext.Session.Clear();
				return RedirectToAction("Login", "AdmPanel");
			}
			if (!string.IsNullOrEmpty(user.FirstName) && user.FirstName.Length >= 2)
			{
				ViewBag.UserInitials = user.FirstName.Substring(0, 2); // İlk iki harfi al
			}
			else if (!string.IsNullOrEmpty(user.FirstName))
			{
				ViewBag.UserInitials = user.FirstName; // İsim iki harften kısa ise tüm ismi kullan
			}
			else
			{
				ViewBag.UserInitials = ""; // İsim boş veya null ise boş bir string ata
			}
			string xmlFilePath = Path.Combine(_hostingEnvironment.WebRootPath, "settings.xml");
			if (System.IO.File.Exists(xmlFilePath))
			{
				XElement settings = XElement.Load(xmlFilePath);
				ViewBag.SiteTitle = settings.Element("Title")?.Value; // Site ismini ViewBag ile taşı
			}
			ViewBag.UserRole = userRole;
			ViewBag.UserID = userId;
			ViewBag.UserName = user.FirstName + " " + user.LastName;
			ViewBag.UserPosition = user.Position;
			switch (userRole)
			{
				case "Head Admin":
				case "Admin":
					var users = _context.Customers.Where(s => s.Active == true).ToList();
					return View(users);
				case "Staff":
					return RedirectToAction("Login", "AdmPanel"); // Staff profili için View döndür
				default:
					// Rol tanımlanmamışsa veya tanımlı bir role uymuyorsa login sayfasına yönlendir
					return RedirectToAction("Login", "AdmPanel");
			}
		}

		public IActionResult DeactiveUsers()
		{
			int? userId = HttpContext.Session.GetInt32("UserId");
			string userRole = HttpContext.Session.GetString("UserRole");
			string siteName = HttpContext.Session.GetString("SiteName");
			if (siteName != LoadSiteTitleFromXml())
			{
				return RedirectToAction("Home", "AdmPanel");
			}
			if (!userId.HasValue || string.IsNullOrEmpty(userRole))
			{
				// Eğer kullanıcı ID veya rol belirtilmemişse, kullanıcıyı login sayfasına yönlendir
				return RedirectToAction("Login", "AdmPanel");
			}
			var user = _context.Staffs.FirstOrDefault(u => u.StaffID == userId.Value);
			if (user == null)
			{
				// Kullanıcı bulunamadıysa oturumu sonlandır ve login sayfasına yönlendir
				HttpContext.Session.Clear();
				return RedirectToAction("Login", "AdmPanel");
			}
			if (!string.IsNullOrEmpty(user.FirstName) && user.FirstName.Length >= 2)
			{
				ViewBag.UserInitials = user.FirstName.Substring(0, 2); // İlk iki harfi al
			}
			else if (!string.IsNullOrEmpty(user.FirstName))
			{
				ViewBag.UserInitials = user.FirstName; // İsim iki harften kısa ise tüm ismi kullan
			}
			else
			{
				ViewBag.UserInitials = ""; // İsim boş veya null ise boş bir string ata
			}
			string xmlFilePath = Path.Combine(_hostingEnvironment.WebRootPath, "settings.xml");
			if (System.IO.File.Exists(xmlFilePath))
			{
				XElement settings = XElement.Load(xmlFilePath);
				ViewBag.SiteTitle = settings.Element("Title")?.Value; // Site ismini ViewBag ile taşı
			}
			ViewBag.UserRole = userRole;
			ViewBag.UserID = userId;
			ViewBag.UserName = user.FirstName + " " + user.LastName;
			ViewBag.UserPosition = user.Position;
			switch (userRole)
			{
				case "Head Admin":
				case "Admin":
					var users = _context.Customers.Where(s => s.Active == false).ToList();
					return View(users);
				case "Staff":
					return RedirectToAction("Login", "AdmPanel"); // Staff profili için View döndür
				default:
					// Rol tanımlanmamışsa veya tanımlı bir role uymuyorsa login sayfasına yönlendir
					return RedirectToAction("Login", "AdmPanel");
			}
		}

		[HttpPost]
		public IActionResult ActiveCustomer(int id)
		{
			int? userId = HttpContext.Session.GetInt32("UserId");
			string userRole = HttpContext.Session.GetString("UserRole");

			// Kullanıcı giriş yapmamışsa veya rol bilgisi yoksa login sayfasına yönlendir
			if (!userId.HasValue || string.IsNullOrEmpty(userRole))
			{
				return RedirectToAction("Login", "AdmPanel");
			}

			// Kullanıcı bilgilerini kontrol et
			var user = _context.Staffs.FirstOrDefault(u => u.StaffID == userId.Value);
			if (user == null)
			{
				HttpContext.Session.Clear();
				return RedirectToAction("Login", "AdmPanel");
			}

			// Sadece Head Admin ve Admin kullanıcılarının işlem yapmasına izin ver
			if (!(userRole.Equals("Head Admin") || userRole.Equals("Admin")))
			{
				return RedirectToAction("Login", "AdmPanel");
			}

			// Customer'ı bul ve Active özelliğini false yap
			var customer = _context.Customers.FirstOrDefault(c => c.CustomerID == id);
			if (customer != null)
			{
				customer.Active = true;
				_context.SaveChanges();
			}

			// İşlem sonrası Users sayfasına yönlendir
			return RedirectToAction("Users");
		}

		[HttpPost]
		public IActionResult DeactivateCustomer(int id)
		{
			int? userId = HttpContext.Session.GetInt32("UserId");
			string userRole = HttpContext.Session.GetString("UserRole");

			// Kullanıcı giriş yapmamışsa veya rol bilgisi yoksa login sayfasına yönlendir
			if (!userId.HasValue || string.IsNullOrEmpty(userRole))
			{
				return RedirectToAction("Login", "AdmPanel");
			}

			// Kullanıcı bilgilerini kontrol et
			var user = _context.Staffs.FirstOrDefault(u => u.StaffID == userId.Value);
			if (user == null)
			{
				HttpContext.Session.Clear();
				return RedirectToAction("Login", "AdmPanel");
			}

			// Sadece Head Admin ve Admin kullanıcılarının işlem yapmasına izin ver
			if (!(userRole.Equals("Head Admin") || userRole.Equals("Admin")))
			{
				return RedirectToAction("Login", "AdmPanel");
			}

			// Customer'ı bul ve Active özelliğini false yap
			var customer = _context.Customers.FirstOrDefault(c => c.CustomerID == id);
			if (customer != null)
			{
				customer.Active = false;
				_context.SaveChanges();
			}

			// İşlem sonrası Users sayfasına yönlendir
			return RedirectToAction("Users");
		}

		[HttpPost]
		public IActionResult Staff(int RoleID, string FirstName, string LastName, string Position, string Phone, string Email, string Password)
		{
			int? userId = HttpContext.Session.GetInt32("UserId");
			string userRole = HttpContext.Session.GetString("UserRole");
			string siteName = HttpContext.Session.GetString("SiteName");
			if (siteName != LoadSiteTitleFromXml())
			{
				return RedirectToAction("Home", "AdmPanel");
			}
			if (!userId.HasValue || string.IsNullOrEmpty(userRole))
			{
				// If user ID or role is not specified, redirect to login page
				return RedirectToAction("Login", "AdmPanel");
			}

			var user = _context.Staffs.FirstOrDefault(u => u.StaffID == userId.Value);
			if (user == null)
			{
				// If user is not found, clear the session and redirect to login page
				HttpContext.Session.Clear();
				return RedirectToAction("Login", "AdmPanel");
			}

			if (!string.IsNullOrEmpty(user.FirstName) && user.FirstName.Length >= 2)
			{
				ViewBag.UserInitials = user.FirstName.Substring(0, 2); // Take the first two characters
			}
			else if (!string.IsNullOrEmpty(user.FirstName))
			{
				ViewBag.UserInitials = user.FirstName; // Use the full name if it is shorter than two characters
			}
			else
			{
				ViewBag.UserInitials = ""; // Assign an empty string if the name is empty or null
			}

			ViewBag.UserRole = userRole;
			ViewBag.UserName = user.FirstName + " " + user.LastName;
			ViewBag.UserPosition = user.Position;

			switch (userRole)
			{
				case "Head Admin":
				case "Admin":
					if (!ModelState.IsValid)
					{
						// Print ModelState errors for debugging
						var errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage).ToList();
						foreach (var error in errors)
						{
							Console.WriteLine(error);
						}
						return View(); // Return the view with the model to display validation errors
					}

					// Check if the email already exists
					if (_context.Staffs.Any(s => s.Email == Email))
					{
						ModelState.AddModelError("Email", "The email address is already in use.");
						return RedirectToAction("Staff", "AdmPanel"); // Return the view with an error message
					}

					// Ensure "Admin" cannot add "Head Admin"
					if (userRole == "Admin" && RoleID == 1)
					{
						ModelState.AddModelError("Role", "Admin users cannot add a Head Admin role.");
						return RedirectToAction("Staff", "AdmPanel"); // Return the view with an error message
					}

					// Prefix phone numbers with "+90"
					if (!Phone.StartsWith("+90"))
					{
						Phone = "+90" + Phone.TrimStart('0'); // Ensure no leading zero after "+90"
					}

					var newStaff = new Staff
					{
						FirstName = FirstName,
						LastName = LastName,
						Position = Position,
						Phone = Phone,
						Email = Email,
						PasswordHash = EncodeBase64(Password), // Encode the password in Base64
						RoleID = RoleID,
						status = true
					};

					_context.Staffs.Add(newStaff);
					_context.SaveChanges();
					return RedirectToAction("Staff", "AdmPanel"); // Redirect to a suitable page after saving

				case "Staff":
					return RedirectToAction("Login", "AdmPanel"); // Redirect to login for staff profile

				default:
					// Redirect to login if role is undefined or not matched
					return RedirectToAction("Login", "AdmPanel");
			}
		}

		public IActionResult StaffEdit(int id)
		{
			if (id == 1)
			{
				return RedirectToAction("Staff", "AdmPanel");
			}
			string xmlFilePath = Path.Combine(_hostingEnvironment.WebRootPath, "settings.xml");
			if (System.IO.File.Exists(xmlFilePath))
			{
				XElement settings = XElement.Load(xmlFilePath);
				ViewBag.SiteTitle = settings.Element("Title")?.Value; // Site ismini ViewBag ile taşı
			}
			int? userId = HttpContext.Session.GetInt32("UserId");
			string userRole = HttpContext.Session.GetString("UserRole");
			string siteName = HttpContext.Session.GetString("SiteName");
			if (siteName != LoadSiteTitleFromXml())
			{
				return RedirectToAction("Home", "AdmPanel");
			}
			if (!userId.HasValue || string.IsNullOrEmpty(userRole))
			{
				// Eğer kullanıcı ID veya rol belirtilmemişse, kullanıcıyı login sayfasına yönlendir
				return RedirectToAction("Login", "AdmPanel");
			}

			ViewBag.UserID = userId;
			switch (userRole)
			{
				case "Head Admin":
				case "Admin":
					var user = _context.Staffs.FirstOrDefault(u => u.StaffID == userId.Value);
					if (user == null)
					{
						// Kullanıcı bulunamadıysa oturumu sonlandır ve login sayfasına yönlendir
						HttpContext.Session.Clear();
						return RedirectToAction("Login", "AdmPanel");
					}
					if (!string.IsNullOrEmpty(user.FirstName) && user.FirstName.Length >= 2)
					{
						ViewBag.UserInitials = user.FirstName.Substring(0, 2); // İlk iki harfi al
					}
					else if (!string.IsNullOrEmpty(user.FirstName))
					{
						ViewBag.UserInitials = user.FirstName; // İsim iki harften kısa ise tüm ismi kullan
					}
					else
					{
						ViewBag.UserInitials = ""; // İsim boş veya null ise boş bir string ata
					}
					ViewBag.UserPosition = user.Position;
					ViewBag.UserName = user.FirstName + " " + user.LastName;
					var staff = _context.Staffs.FirstOrDefault(g => g.StaffID == id);
					if ((userRole == "Admin" && staff.RoleID == 1) || (userRole == "Staff" && staff.RoleID == 1))
					{
						ModelState.AddModelError("Role", "Admin users cannot add a Head Admin role.");
						return RedirectToAction("Staff", "AdmPanel"); // Return the view with an error message
					}
					if (staff == null)
					{
						return NotFound();
					}
					ViewBag.DecodedPassword = DecodeBase64(staff.PasswordHash); // Decode the password
					ViewBag.PhoneNumberWithoutPrefix = StripCountryCode(staff.Phone); // Strip "+90" prefix
					return View(staff);
				case "Staff":
					return RedirectToAction("Login", "AdmPanel"); // Staff profili için View döndür
				case "Customer":
					// Customer için farklı bir yönlendirme yapılabilir veya burada hata yönetimi
					return RedirectToAction("Index", "Home"); // Customer ana sayfasına yönlendir
				default:
					// Rol tanımlanmamışsa veya tanımlı bir role uymuyorsa login sayfasına yönlendir
					return RedirectToAction("Login", "AdmPanel");
			}
		}

		[HttpPost]
		public IActionResult StaffEdit(int id, int RoleID, string FirstName, string LastName, string Position, string Phone, string Email, string Password)
		{
			var staff = _context.Staffs.FirstOrDefault(g => g.StaffID == id);
			if (staff == null)
			{
				return NotFound();
			}

			staff.FirstName = FirstName;
			staff.LastName = LastName;
			staff.Position = Position;

			if (!Phone.StartsWith("+90"))
			{
				Phone = "+90" + Phone.TrimStart('0');
			}
			staff.Phone = Phone;

			if (staff.Email != Email && _context.Staffs.Any(s => s.Email == Email))
			{
				ModelState.AddModelError("Email", "The email address is already in use.");
				return View(staff); // Return the view with an error message
			}
			staff.Email = Email;

			staff.RoleID = RoleID;

			if (!string.IsNullOrEmpty(Password))
			{
				staff.PasswordHash = EncodeBase64(Password); // Encode the new password in Base64
			}

			_context.SaveChanges();
			return RedirectToAction("Staff", "AdmPanel"); // Redirect to a suitable page after saving
		}

		public IActionResult OffStaff()
		{
			string xmlFilePath = Path.Combine(_hostingEnvironment.WebRootPath, "settings.xml");
			if (System.IO.File.Exists(xmlFilePath))
			{
				XElement settings = XElement.Load(xmlFilePath);
				ViewBag.SiteTitle = settings.Element("Title")?.Value; // Site ismini ViewBag ile taşı
			}
			int? userId = HttpContext.Session.GetInt32("UserId");
			string userRole = HttpContext.Session.GetString("UserRole");
			string siteName = HttpContext.Session.GetString("SiteName");
			if (siteName != LoadSiteTitleFromXml())
			{
				return RedirectToAction("Home", "AdmPanel");
			}
			if (!userId.HasValue || string.IsNullOrEmpty(userRole))
			{
				// Eğer kullanıcı ID veya rol belirtilmemişse, kullanıcıyı login sayfasına yönlendir
				return RedirectToAction("Login", "AdmPanel");
			}
			var user = _context.Staffs.FirstOrDefault(u => u.StaffID == userId.Value);
			if (user == null)
			{
				// Kullanıcı bulunamadıysa oturumu sonlandır ve login sayfasına yönlendir
				HttpContext.Session.Clear();
				return RedirectToAction("Login", "AdmPanel");
			}
			if (!string.IsNullOrEmpty(user.FirstName) && user.FirstName.Length >= 2)
			{
				ViewBag.UserInitials = user.FirstName.Substring(0, 2); // İlk iki harfi al
			}
			else if (!string.IsNullOrEmpty(user.FirstName))
			{
				ViewBag.UserInitials = user.FirstName; // İsim iki harften kısa ise tüm ismi kullan
			}
			else
			{
				ViewBag.UserInitials = ""; // İsim boş veya null ise boş bir string ata
			}
			ViewBag.UserID = userId;
			ViewBag.UserRole = userRole;
			ViewBag.UserName = user.FirstName + " " + user.LastName;
			ViewBag.UserPosition = user.Position;
			switch (userRole)
			{
				case "Head Admin":
				case "Admin":
					var staff = _context.Staffs.Include(s => s.Role).Where(s => s.status == false).ToList();
					return View(staff);
				case "Staff":
					return RedirectToAction("Login", "AdmPanel"); // Staff profili için View döndür
				default:
					// Rol tanımlanmamışsa veya tanımlı bir role uymuyorsa login sayfasına yönlendir
					return RedirectToAction("Login", "AdmPanel");
			}
		}

		[HttpPost]
		public IActionResult OffStaffON(int id)
		{
			int? userId = HttpContext.Session.GetInt32("UserId");
			string userRole = HttpContext.Session.GetString("UserRole");
			string siteName = HttpContext.Session.GetString("SiteName");
			if (siteName != LoadSiteTitleFromXml())
			{
				return RedirectToAction("Home", "AdmPanel");
			}
			if (!userId.HasValue || string.IsNullOrEmpty(userRole))
			{
				// Eğer kullanıcı ID veya rol belirtilmemişse, kullanıcıyı login sayfasına yönlendir
				return RedirectToAction("Login", "AdmPanel");
			}

			ViewBag.UserID = userId;
			switch (userRole)
			{
				case "Head Admin":
					var staff = _context.Staffs.FirstOrDefault(s => s.StaffID == id);
					if (staff == null)
					{
						return NotFound();
					}

					staff.status = true;
					_context.SaveChanges();

					return RedirectToAction("Staff", "AdmPanel"); // Redirect back to the staff listing page
				case "Admin":
				case "Staff":
					return RedirectToAction("Login", "AdmPanel"); // Staff profili için View döndür
				case "Customer":
					// Customer için farklı bir yönlendirme yapılabilir veya burada hata yönetimi
					return RedirectToAction("Index", "Home"); // Customer ana sayfasına yönlendir
				default:
					// Rol tanımlanmamışsa veya tanımlı bir role uymuyorsa login sayfasına yönlendir
					return RedirectToAction("Login", "AdmPanel");
			}
		}

		[HttpPost]
		public IActionResult OffStaffOff(int id)
		{
			int? userId = HttpContext.Session.GetInt32("UserId");
			string userRole = HttpContext.Session.GetString("UserRole");
			string siteName = HttpContext.Session.GetString("SiteName");
			if (siteName != LoadSiteTitleFromXml())
			{
				return RedirectToAction("Home", "AdmPanel");
			}
			if (!userId.HasValue || string.IsNullOrEmpty(userRole))
			{
				// Eğer kullanıcı ID veya rol belirtilmemişse, kullanıcıyı login sayfasına yönlendir
				return RedirectToAction("Login", "AdmPanel");
			}

			ViewBag.UserID = userId;
			switch (userRole)
			{
				case "Head Admin":
					var staff = _context.Staffs.FirstOrDefault(s => s.StaffID == id);
					if (staff == null)
					{
						return NotFound();
					}

					staff.status = false;
					_context.SaveChanges();

					return RedirectToAction("Staff", "AdmPanel"); // Redirect back to the staff listing page
				case "Admin":
				case "Staff":
					return RedirectToAction("Login", "AdmPanel"); // Staff profili için View döndür
				case "Customer":
					// Customer için farklı bir yönlendirme yapılabilir veya burada hata yönetimi
					return RedirectToAction("Index", "Home"); // Customer ana sayfasına yönlendir
				default:
					// Rol tanımlanmamışsa veya tanımlı bir role uymuyorsa login sayfasına yönlendir
					return RedirectToAction("Login", "AdmPanel");
			}
		}

		public IActionResult SpecialDays(int id)
		{
			if (id == 1)
			{
				return RedirectToAction("Index", "AdmPanel");
			}
			string xmlFilePath = Path.Combine(_hostingEnvironment.WebRootPath, "settings.xml");
			if (System.IO.File.Exists(xmlFilePath))
			{
				XElement settings = XElement.Load(xmlFilePath);
				ViewBag.SiteTitle = settings.Element("Title")?.Value; // Site ismini ViewBag ile taşı
			}
			int? userId = HttpContext.Session.GetInt32("UserId");
			string userRole = HttpContext.Session.GetString("UserRole");
			string siteName = HttpContext.Session.GetString("SiteName");
			if (siteName != LoadSiteTitleFromXml())
			{
				return RedirectToAction("Home", "AdmPanel");
			}
			if (!userId.HasValue || string.IsNullOrEmpty(userRole))
			{
				// Eğer kullanıcı ID veya rol belirtilmemişse, kullanıcıyı login sayfasına yönlendir
				return RedirectToAction("Login", "AdmPanel");
			}
			var user = _context.Staffs.FirstOrDefault(u => u.StaffID == userId.Value);
			if (user == null)
			{
				// Kullanıcı bulunamadıysa oturumu sonlandır ve login sayfasına yönlendir
				HttpContext.Session.Clear();
				return RedirectToAction("Login", "AdmPanel");
			}
			if (!string.IsNullOrEmpty(user.FirstName) && user.FirstName.Length >= 2)
			{
				ViewBag.UserInitials = user.FirstName.Substring(0, 2); // İlk iki harfi al
			}
			else if (!string.IsNullOrEmpty(user.FirstName))
			{
				ViewBag.UserInitials = user.FirstName; // İsim iki harften kısa ise tüm ismi kullan
			}
			else
			{
				ViewBag.UserInitials = ""; // İsim boş veya null ise boş bir string ata
			}
			ViewBag.UserID = userId;
			ViewBag.UserRole = userRole;
			ViewBag.UserName = user.FirstName + " " + user.LastName;
			ViewBag.UserPosition = user.Position;
			ViewBag.StaffID = id;
			switch (userRole)
			{
				case "Head Admin":
				case "Admin":
				case "Staff":
					var specialDays = _context.SpecialDays
						  .Where(s => s.StaffID == id)
						  .Select(s => new
						  {
							  s.SpecialDayID,
							  s.StaffID,
							  Date = s.Date.ToString("dd MMMM yyyy", new CultureInfo("tr-TR")),
							  s.Description
						  })
						  .ToList();
					return View((object)specialDays); // Staff profili için View döndür
				default:
					// Rol tanımlanmamışsa veya tanımlı bir role uymuyorsa login sayfasına yönlendir
					return RedirectToAction("Login", "AdmPanel");
			}
		}

		public IActionResult SpecialDayDelete(int id)
		{
			int? userId = HttpContext.Session.GetInt32("UserId");
			string userRole = HttpContext.Session.GetString("UserRole");
			string siteName = HttpContext.Session.GetString("SiteName");
			if (siteName != LoadSiteTitleFromXml())
			{
				return RedirectToAction("Home", "AdmPanel");
			}
			if (!userId.HasValue || string.IsNullOrEmpty(userRole))
			{
				return RedirectToAction("Login", "AdmPanel");
			}

			var specialDay = _context.SpecialDays.FirstOrDefault(s => s.SpecialDayID == id);
			if (specialDay == null)
			{
				return NotFound("Özel gün bulunamadı.");
			}

			_context.SpecialDays.Remove(specialDay);
			_context.SaveChanges();

			return RedirectToAction("SpecialDays", new { id = specialDay.StaffID });
		}

		[HttpPost]
		public IActionResult AddSpecialDay(int id, string dateRange, string description)
		{
			var dates = dateRange.Split(" - ");
			if (dates.Length != 2)
			{
				return BadRequest("Geçersiz tarih aralığı.");
			}

			DateTime startDate = DateTime.Parse(dates[0]);
			DateTime endDate = DateTime.Parse(dates[1]);

			if (endDate < startDate)
			{
				return BadRequest("Bitiş tarihi başlangıç tarihinden önce olamaz.");
			}

			TimeSpan dateRangeSpan = endDate - startDate;
			if (dateRangeSpan.Days > 30)
			{
				return BadRequest("Tarih aralığı 30 günden fazla olamaz.");
			}

			for (DateTime date = startDate; date <= endDate; date = date.AddDays(1))
			{
				var specialDay = new SpecialDay
				{
					StaffID = id,
					Date = date,
					Description = description
				};
				_context.SpecialDays.Add(specialDay);
			}

			_context.SaveChanges();
			return RedirectToAction("SpecialDays", new { id = id });
		}

		public IActionResult Blog()
		{
			string xmlFilePath = Path.Combine(_hostingEnvironment.WebRootPath, "settings.xml");
			if (System.IO.File.Exists(xmlFilePath))
			{
				XElement settings = XElement.Load(xmlFilePath);
				ViewBag.SiteTitle = settings.Element("Title")?.Value; // Site ismini ViewBag ile taşı
			}
			int? userId = HttpContext.Session.GetInt32("UserId");
			string userRole = HttpContext.Session.GetString("UserRole");
			string siteName = HttpContext.Session.GetString("SiteName");
			if (siteName != LoadSiteTitleFromXml())
			{
				return RedirectToAction("Home", "AdmPanel");
			}
			if (!userId.HasValue || string.IsNullOrEmpty(userRole))
			{
				// Eğer kullanıcı ID veya rol belirtilmemişse, kullanıcıyı login sayfasına yönlendir
				return RedirectToAction("Login", "AdmPanel");
			}
			var user = _context.Staffs.FirstOrDefault(u => u.StaffID == userId.Value);
			if (user == null)
			{
				// Kullanıcı bulunamadıysa oturumu sonlandır ve login sayfasına yönlendir
				HttpContext.Session.Clear();
				return RedirectToAction("Login", "AdmPanel");
			}
			if (!string.IsNullOrEmpty(user.FirstName) && user.FirstName.Length >= 2)
			{
				ViewBag.UserInitials = user.FirstName.Substring(0, 2); // İlk iki harfi al
			}
			else if (!string.IsNullOrEmpty(user.FirstName))
			{
				ViewBag.UserInitials = user.FirstName; // İsim iki harften kısa ise tüm ismi kullan
			}
			else
			{
				ViewBag.UserInitials = ""; // İsim boş veya null ise boş bir string ata
			}
			ViewBag.UserID = userId;
			ViewBag.UserRole = userRole;
			ViewBag.UserName = user.FirstName + " " + user.LastName;
			ViewBag.UserPosition = user.Position;
			switch (userRole)
			{
				case "Head Admin":
				case "Admin":
				case "Staff":
					var blogs = _context.BlogPosts.ToList();
					return View(blogs); // Staff profili için View döndür
				default:
					// Rol tanımlanmamışsa veya tanımlı bir role uymuyorsa login sayfasına yönlendir
					return RedirectToAction("Login", "AdmPanel");
			}
		}

		public IActionResult BlogDelete(int id)
		{
			string xmlFilePath = Path.Combine(_hostingEnvironment.WebRootPath, "settings.xml");
			if (System.IO.File.Exists(xmlFilePath))
			{
				XElement settings = XElement.Load(xmlFilePath);
				ViewBag.SiteTitle = settings.Element("Title")?.Value; // Site ismini ViewBag ile taşı
			}
			int? userId = HttpContext.Session.GetInt32("UserId");
			string userRole = HttpContext.Session.GetString("UserRole");
			string siteName = HttpContext.Session.GetString("SiteName");
			if (siteName != LoadSiteTitleFromXml())
			{
				return RedirectToAction("Home", "AdmPanel");
			}
			if (!userId.HasValue || string.IsNullOrEmpty(userRole))
			{
				return RedirectToAction("Login", "AdmPanel");
			}

			var blog = _context.BlogPosts.FirstOrDefault(s => s.BlogPostID == id);
			if (blog == null)
			{
				return NotFound("Özel gün bulunamadı.");
			}

			// Dosya yolu oluşturma
			string path = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "index", "images", "blog", $"{blog.BlogPostID}.png");

			// Dosyanın var olup olmadığını kontrol etme
			if (System.IO.File.Exists(path))
			{
				System.IO.File.Delete(path);  // Dosyayı silme
			}

			_context.BlogPosts.Remove(blog);
			_context.SaveChanges();

			return RedirectToAction("Blog", "AdmPanel");
		}

		[HttpPost]
		public async Task<IActionResult> Blog(BlogPost blogPost, IFormFile ImageFile)
		{
			if (ModelState.IsValid)
			{
				blogPost.PublishDate = DateTime.Now; // Yayın tarihi olarak şimdiki zamanı atayın

				_context.BlogPosts.Add(blogPost);
				await _context.SaveChangesAsync(); // Veritabanına kaydet

				if (ImageFile != null && ImageFile.Length > 0 && ImageFile.ContentType == "image/png")
				{
					var path = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "index", "images", "blog", $"{blogPost.BlogPostID}.png");

					using (var image = SixLabors.ImageSharp.Image.Load(ImageFile.OpenReadStream()))
					{
						image.Mutate(x => x.Resize(770, 330));

						// Dosyayı açarken FileShare.None ile kilitleyin
						using (var fs = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None))
						{
							await image.SaveAsPngAsync(fs);
						}
					}
				}

				return RedirectToAction("Blog", "AdmPanel"); // Yönlendirme
			}

			return RedirectToAction("Blog", "AdmPanel");
		}

		public IActionResult BlogEdit(int id)
		{
			string xmlFilePath = Path.Combine(_hostingEnvironment.WebRootPath, "settings.xml");
			if (System.IO.File.Exists(xmlFilePath))
			{
				XElement settings = XElement.Load(xmlFilePath);
				ViewBag.SiteTitle = settings.Element("Title")?.Value; // Site ismini ViewBag ile taşı
			}
			int? userId = HttpContext.Session.GetInt32("UserId");
			string userRole = HttpContext.Session.GetString("UserRole");
			string siteName = HttpContext.Session.GetString("SiteName");
			if (siteName != LoadSiteTitleFromXml())
			{
				return RedirectToAction("Home", "AdmPanel");
			}
			if (!userId.HasValue || string.IsNullOrEmpty(userRole))
			{
				// Eğer kullanıcı ID veya rol belirtilmemişse, kullanıcıyı login sayfasına yönlendir
				return RedirectToAction("Login", "AdmPanel");
			}


			ViewBag.UserID = userId;
			switch (userRole)
			{
				case "Head Admin":
				case "Admin":
				case "Staff":
					var user = _context.Staffs.FirstOrDefault(u => u.StaffID == userId.Value);
					if (user == null)
					{
						// Kullanıcı bulunamadıysa oturumu sonlandır ve login sayfasına yönlendir
						HttpContext.Session.Clear();
						return RedirectToAction("Login", "AdmPanel");
					}
					if (!string.IsNullOrEmpty(user.FirstName) && user.FirstName.Length >= 2)
					{
						ViewBag.UserInitials = user.FirstName.Substring(0, 2); // İlk iki harfi al
					}
					else if (!string.IsNullOrEmpty(user.FirstName))
					{
						ViewBag.UserInitials = user.FirstName; // İsim iki harften kısa ise tüm ismi kullan
					}
					else
					{
						ViewBag.UserInitials = ""; // İsim boş veya null ise boş bir string ata
					}
					ViewBag.UserPosition = user.Position;
					ViewBag.UserName = user.FirstName + " " + user.LastName;
					ViewBag.UserRole = userRole;
					var blog = _context.BlogPosts.FirstOrDefault(g => g.BlogPostID == id);
					ViewBag.Title = blog.Title;
					ViewBag.Content = blog.Content;
					return View(blog);
				case "Customer":
					// Customer için farklı bir yönlendirme yapılabilir veya burada hata yönetimi
					return RedirectToAction("Index", "Home"); // Customer ana sayfasına yönlendir
				default:
					// Rol tanımlanmamışsa veya tanımlı bir role uymuyorsa login sayfasına yönlendir
					return RedirectToAction("Login", "AdmPanel");
			}
		}

		[HttpPost]
		public async Task<IActionResult> BlogEdit(int id, BlogPost blogPost, IFormFile ImageFile)
		{
			ModelState.Remove("ImageFile");
			if (!ModelState.IsValid)
			{
				return View(blogPost); // Eğer model geçersizse, formu tekrar göster
			}

			var existingPost = await _context.BlogPosts.FindAsync(id);
			if (existingPost == null)
			{
				return NotFound(); // Blog post bulunamazsa, hata döndür
			}

			// Güncellemeleri yap
			existingPost.Title = blogPost.Title;
			existingPost.Content = blogPost.Content;

			// Dosya işleme
			if (ImageFile != null && ImageFile.Length > 0 && ImageFile.ContentType == "image/png")
			{
				var path = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "index", "images", "blog", $"{existingPost.BlogPostID}.png");

				using (var image = SixLabors.ImageSharp.Image.Load(ImageFile.OpenReadStream()))
				{
					image.Mutate(x => x.Resize(770, 330));
					await image.SaveAsPngAsync(path);
				}
			}

			_context.Update(existingPost);
			await _context.SaveChangesAsync();

			return RedirectToAction("Blog", "AdmPanel"); // İşlem tamamlandıktan sonra ana blog sayfasına yönlendir
		}

		public IActionResult AdmSettings()
		{
			string xmlFilePath = Path.Combine(_hostingEnvironment.WebRootPath, "settings.xml");
			if (System.IO.File.Exists(xmlFilePath))
			{
				XElement settings = XElement.Load(xmlFilePath);
				ViewBag.SiteTitle = settings.Element("Title")?.Value; // Site ismini ViewBag ile taşı
			}
			int? userId = HttpContext.Session.GetInt32("UserId");
			string userRole = HttpContext.Session.GetString("UserRole");
			string siteName = HttpContext.Session.GetString("SiteName");
			if (siteName != LoadSiteTitleFromXml())
			{
				return RedirectToAction("Home", "AdmPanel");
			}
			if (!userId.HasValue || string.IsNullOrEmpty(userRole))
			{
				// Eğer kullanıcı ID veya rol belirtilmemişse, kullanıcıyı login sayfasına yönlendir
				return RedirectToAction("Login", "AdmPanel");
			}
			var user = _context.Staffs.FirstOrDefault(u => u.StaffID == userId.Value);
			if (user == null)
			{
				// Kullanıcı bulunamadıysa oturumu sonlandır ve login sayfasına yönlendir
				HttpContext.Session.Clear();
				return RedirectToAction("Login", "AdmPanel");
			}
			if (!string.IsNullOrEmpty(user.FirstName) && user.FirstName.Length >= 2)
			{
				ViewBag.UserInitials = user.FirstName.Substring(0, 2); // İlk iki harfi al
			}
			else if (!string.IsNullOrEmpty(user.FirstName))
			{
				ViewBag.UserInitials = user.FirstName; // İsim iki harften kısa ise tüm ismi kullan
			}
			else
			{
				ViewBag.UserInitials = ""; // İsim boş veya null ise boş bir string ata
			}
			ViewBag.UserRole = userRole;
			ViewBag.UserID = userId;
			ViewBag.UserName = user.FirstName + " " + user.LastName;
			ViewBag.UserPosition = user.Position;

			// Online ödeme durumunu kontrol et
			string filePath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "settings5.xml");
			bool isOnlinePaymentEnabled = false;
			if (System.IO.File.Exists(filePath))
			{
				var doc = XDocument.Load(filePath);
				var settings = doc.Element("Settings");
				var onlinePaymentElement = settings.Element("OnlinePayment");
				if (onlinePaymentElement != null)
				{
					isOnlinePaymentEnabled = bool.Parse(onlinePaymentElement.Value);
				}
			}
			ViewBag.OnlinePaymentEnabled = isOnlinePaymentEnabled;

			switch (userRole)
			{
				case "Head Admin":
				case "Admin":
					return View();
				default:
					// Rol tanımlanmamışsa veya tanımlı bir role uymuyorsa login sayfasına yönlendir
					return RedirectToAction("Login", "AdmPanel");
			}
		}

		public IActionResult OnlinePaymentToggle()
		{
			// XML dosyasının yolunu belirle
			string filePath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "settings5.xml");

			// XML dosyasını oku veya oluştur
			bool isOnlinePaymentEnabled = false;
			if (System.IO.File.Exists(filePath))
			{
				var doc = XDocument.Load(filePath);
				var settings = doc.Element("Settings");
				var onlinePaymentElement = settings.Element("OnlinePayment");
				if (onlinePaymentElement != null)
				{
					isOnlinePaymentEnabled = bool.Parse(onlinePaymentElement.Value);
				}
			}

			// Durumu tersine çevir (aktif/pasif)
			isOnlinePaymentEnabled = !isOnlinePaymentEnabled;

			// XML dosyasını güncelle veya oluştur
			var newDoc = new XDocument(
				new XElement("Settings",
					new XElement("OnlinePayment", isOnlinePaymentEnabled.ToString())
				)
			);
			newDoc.Save(filePath);

			// Aynı sayfaya yönlendir
			return RedirectToAction("AdmSettings");
		}

		public IActionResult PrivacySettings()
		{
			int? userId = HttpContext.Session.GetInt32("UserId");
			string userRole = HttpContext.Session.GetString("UserRole");
			string siteName = HttpContext.Session.GetString("SiteName");
			if (siteName != LoadSiteTitleFromXml())
			{
				return RedirectToAction("Home", "AdmPanel");
			}
			if (!userId.HasValue || string.IsNullOrEmpty(userRole))
			{
				// Eğer kullanıcı ID veya rol belirtilmemişse, kullanıcıyı login sayfasına yönlendir
				return RedirectToAction("Login", "AdmPanel");
			}
			var user = _context.Staffs.FirstOrDefault(u => u.StaffID == userId.Value);
			if (user == null)
			{
				// Kullanıcı bulunamadıysa oturumu sonlandır ve login sayfasına yönlendir
				HttpContext.Session.Clear();
				return RedirectToAction("Login", "AdmPanel");
			}
			if (!string.IsNullOrEmpty(user.FirstName) && user.FirstName.Length >= 2)
			{
				ViewBag.UserInitials = user.FirstName.Substring(0, 2); // İlk iki harfi al
			}
			else if (!string.IsNullOrEmpty(user.FirstName))
			{
				ViewBag.UserInitials = user.FirstName; // İsim iki harften kısa ise tüm ismi kullan
			}
			else
			{
				ViewBag.UserInitials = ""; // İsim boş veya null ise boş bir string ata
			}
			ViewBag.UserID = userId;
			ViewBag.UserRole = userRole;
			ViewBag.UserName = user.FirstName + " " + user.LastName;
			ViewBag.UserPosition = user.Position;

			string xmlFilePath = Path.Combine(_hostingEnvironment.WebRootPath, "settings6.xml");
			if (System.IO.File.Exists(xmlFilePath))
			{
				XElement settings = XElement.Load(xmlFilePath);
				ViewBag.Privacy = settings.Element("Privacy")?.Value; // Privacy bilgilerini ViewBag ile taşı
			}

			switch (userRole)
			{
				case "Head Admin":
				case "Admin":
					return View();
				default:
					// Rol tanımlanmamışsa veya tanımlı bir role uymuyorsa login sayfasına yönlendir
					return RedirectToAction("Login", "AdmPanel");
			}
		}

		[HttpPost]
		public async Task<IActionResult> PrivacySettings(string Privacy)
		{
			var settingsPath = Path.Combine(_hostingEnvironment.WebRootPath, "settings6.xml");
			// Metni <br> etiketi kullanarak HTML satırlarına dönüştürme
			var formattedPrivacy = Privacy.Replace("\n", "<br>");

			var settings = new XElement("Settings",
				new XElement("Privacy", formattedPrivacy)
			);

			// Settings XML dosyasını kaydet
			settings.Save(settingsPath);

			return RedirectToAction("PrivacySettings");
		}

		public IActionResult MainSettings()
		{

			int? userId = HttpContext.Session.GetInt32("UserId");
			string userRole = HttpContext.Session.GetString("UserRole");
			string siteName = HttpContext.Session.GetString("SiteName");
			if (siteName != LoadSiteTitleFromXml())
			{
				return RedirectToAction("Home", "AdmPanel");
			}
			if (!userId.HasValue || string.IsNullOrEmpty(userRole))
			{
				// Eğer kullanıcı ID veya rol belirtilmemişse, kullanıcıyı login sayfasına yönlendir
				return RedirectToAction("Login", "AdmPanel");
			}
			var user = _context.Staffs.FirstOrDefault(u => u.StaffID == userId.Value);
			if (user == null)
			{
				// Kullanıcı bulunamadıysa oturumu sonlandır ve login sayfasına yönlendir
				HttpContext.Session.Clear();
				return RedirectToAction("Login", "AdmPanel");
			}
			if (!string.IsNullOrEmpty(user.FirstName) && user.FirstName.Length >= 2)
			{
				ViewBag.UserInitials = user.FirstName.Substring(0, 2); // İlk iki harfi al
			}
			else if (!string.IsNullOrEmpty(user.FirstName))
			{
				ViewBag.UserInitials = user.FirstName; // İsim iki harften kısa ise tüm ismi kullan
			}
			else
			{
				ViewBag.UserInitials = ""; // İsim boş veya null ise boş bir string ata
			}
			ViewBag.UserID = userId;
			ViewBag.UserRole = userRole;
			ViewBag.UserName = user.FirstName + " " + user.LastName;
			ViewBag.UserPosition = user.Position;

			string xmlFilePath = Path.Combine(_hostingEnvironment.WebRootPath, "settings.xml");
			if (System.IO.File.Exists(xmlFilePath))
			{
				XElement settings = XElement.Load(xmlFilePath);
				ViewBag.SiteTitle = settings.Element("Title")?.Value; // Site ismini ViewBag ile taşı
			}
			switch (userRole)
			{
				case "Head Admin":
				case "Admin":
					return View();
				default:
					// Rol tanımlanmamışsa veya tanımlı bir role uymuyorsa login sayfasına yönlendir
					return RedirectToAction("Login", "AdmPanel");
			}
		}

		[HttpPost]
		public async Task<IActionResult> MainSettings(string Title, IFormFile ImageFile, IFormFile ImageFile2, IFormFile ImageFile3)
		{
			var settingsPath = Path.Combine(_hostingEnvironment.WebRootPath, "settings.xml");
			var settings = new XElement("Settings",
				new XElement("Title", Title)
			);

			// Settings XML dosyasını kaydet
			settings.Save(settingsPath);

			// Logo ve arkaplan resimlerini kaydet
			if (ImageFile != null && ImageFile.Length > 0)
			{
				string logoPath = Path.Combine(_hostingEnvironment.WebRootPath, "img", "logo.png");
				await SaveFileAsync(ImageFile, logoPath);

				// Icona çevirme
				await ConvertToIconAsync(logoPath, Path.Combine(_hostingEnvironment.WebRootPath, "panel", "favicon.ico"));
			}

			if (ImageFile2 != null && ImageFile2.Length > 0)
			{
				string homeBgPath = Path.Combine(_hostingEnvironment.WebRootPath, "index", "images", "homebg.png");
				await SaveFileAsync(ImageFile2, homeBgPath);
			}

			if (ImageFile3 != null && ImageFile3.Length > 0)
			{
				string bgImagePath = Path.Combine(_hostingEnvironment.WebRootPath, "index", "images", "bg-image-1.png");
				await SaveFileAsync(ImageFile3, bgImagePath);
			}

			return RedirectToAction("MainSettings");
		}

		public IActionResult RecommendedSettings()
		{
			string xmlFilePath = Path.Combine(_hostingEnvironment.WebRootPath, "settings.xml");
			if (System.IO.File.Exists(xmlFilePath))
			{
				XElement settings = XElement.Load(xmlFilePath);
				ViewBag.SiteTitle = settings.Element("Title")?.Value; // Site ismini ViewBag ile taşı
			}
			int? userId = HttpContext.Session.GetInt32("UserId");
			string userRole = HttpContext.Session.GetString("UserRole");
			string siteName = HttpContext.Session.GetString("SiteName");
			if (siteName != LoadSiteTitleFromXml())
			{
				return RedirectToAction("Home", "AdmPanel");
			}
			if (!userId.HasValue || string.IsNullOrEmpty(userRole))
			{
				return RedirectToAction("Login", "AdmPanel");
			}

			var user = _context.Staffs.FirstOrDefault(u => u.StaffID == userId.Value);
			if (user == null)
			{
				HttpContext.Session.Clear();
				return RedirectToAction("Login", "AdmPanel");
			}

			if (!string.IsNullOrEmpty(user.FirstName) && user.FirstName.Length >= 2)
			{
				ViewBag.UserInitials = user.FirstName.Substring(0, 2);
			}
			else if (!string.IsNullOrEmpty(user.FirstName))
			{
				ViewBag.UserInitials = user.FirstName;
			}
			else
			{
				ViewBag.UserInitials = "";
			}
			ViewBag.UserID = userId;
			ViewBag.UserRole = userRole;
			ViewBag.UserName = user.FirstName + " " + user.LastName;
			ViewBag.UserPosition = user.Position;

			string filePath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "settings2.xml");
			if (System.IO.File.Exists(filePath))
			{
				var doc = XDocument.Load(filePath);
				ViewBag.About = doc.Root.Element("About")?.Value ?? string.Empty;
				ViewBag.FAQs = doc.Root.Element("FAQs")?.Elements("FAQ")
					.Select(x => (x.Element("Question").Value, x.Element("Answer").Value))
					.ToList() ?? new List<(string Question, string Answer)>();
			}
			else
			{
				ViewBag.About = string.Empty;
				ViewBag.FAQs = new List<(string Question, string Answer)>
		{
			("", ""),
			("", ""),
			("", "")
		};
			}

			switch (userRole)
			{
				case "Head Admin":
				case "Admin":
					return View();
				default:
					return RedirectToAction("Login", "AdmPanel");
			}
		}

		[HttpPost]
		public IActionResult AboutEdit(string about)
		{
			string filePath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "settings2.xml");
			List<(string Question, string Answer)> faqs = new List<(string Question, string Answer)>();
			string siteName = HttpContext.Session.GetString("SiteName");
			if (siteName != LoadSiteTitleFromXml())
			{
				return RedirectToAction("Home", "AdmPanel");
			}
			if (System.IO.File.Exists(filePath))
			{
				var doc = XDocument.Load(filePath);
				faqs = doc.Root.Element("FAQs")?.Elements("FAQ")
					.Select(x => (x.Element("Question").Value, x.Element("Answer").Value))
					.ToList() ?? new List<(string Question, string Answer)>
					{
				("", ""),
				("", ""),
				("", "")
					};
			}

			var newDoc = new XDocument(
				new XElement("Settings",
					new XElement("About", about),
					new XElement("FAQs",
						faqs.Select(f => new XElement("FAQ",
							new XElement("Question", f.Question),
							new XElement("Answer", f.Answer)
						))
					)
				)
			);

			newDoc.Save(filePath);
			return RedirectToAction("RecommendedSettings");
		}

		[HttpPost]
		public IActionResult FaqSettings(string question1, string question1answer, string question2, string question2answer, string question3, string question3answer)
		{
			string filePath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "settings2.xml");
			string about = string.Empty;

			if (System.IO.File.Exists(filePath))
			{
				var doc = XDocument.Load(filePath);
				about = doc.Root.Element("About")?.Value ?? string.Empty;
			}

			var faqs = new List<(string Question, string Answer)>
	{
		(question1, question1answer),
		(question2, question2answer),
		(question3, question3answer)
	};

			var newDoc = new XDocument(
				new XElement("Settings",
					new XElement("About", about),
					new XElement("FAQs",
						faqs.Select(f => new XElement("FAQ",
							new XElement("Question", f.Question),
							new XElement("Answer", f.Answer)
						))
					)
				)
			);

			newDoc.Save(filePath);
			return RedirectToAction("RecommendedSettings");
		}

		public IActionResult TavSettings()
		{
			string xmlFilePath = Path.Combine(_hostingEnvironment.WebRootPath, "settings.xml");
			if (System.IO.File.Exists(xmlFilePath))
			{
				XElement settings = XElement.Load(xmlFilePath);
				ViewBag.SiteTitle = settings.Element("Title")?.Value; // Site ismini ViewBag ile taşı
			}
			int? userId = HttpContext.Session.GetInt32("UserId");
			string userRole = HttpContext.Session.GetString("UserRole");
			string siteName = HttpContext.Session.GetString("SiteName");
			if (siteName != LoadSiteTitleFromXml())
			{
				return RedirectToAction("Home", "AdmPanel");
			}
			if (!userId.HasValue || string.IsNullOrEmpty(userRole))
			{
				return RedirectToAction("Login", "AdmPanel");
			}

			var user = _context.Staffs.FirstOrDefault(u => u.StaffID == userId.Value);
			if (user == null)
			{
				HttpContext.Session.Clear();
				return RedirectToAction("Login", "AdmPanel");
			}

			if (!string.IsNullOrEmpty(user.FirstName) && user.FirstName.Length >= 2)
			{
				ViewBag.UserInitials = user.FirstName.Substring(0, 2);
			}
			else if (!string.IsNullOrEmpty(user.FirstName))
			{
				ViewBag.UserInitials = user.FirstName;
			}
			else
			{
				ViewBag.UserInitials = "";
			}
			ViewBag.UserID = userId;
			ViewBag.UserRole = userRole;
			ViewBag.UserName = user.FirstName + " " + user.LastName;
			ViewBag.UserPosition = user.Position;

			string filePath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "settings3.xml");
			List<dynamic> tavs = new List<dynamic>();  // Boş liste ile başlat

			if (System.IO.File.Exists(filePath))
			{
				var doc = XDocument.Load(filePath);
				tavs = doc.Root.Element("Tavsiyeler")?.Elements("Tavsiye")
					.Select(x => new
					{
						Name = x.Element("Name")?.Value ?? "",
						Comment = x.Element("Comment")?.Value ?? "",
						Image = x.Element("Image")?.Value ?? ""
					})
					.ToList<dynamic>() ?? new List<dynamic>();
			}

			while (tavs.Count < 3)  // Burada artık hata almayacağız çünkü tavs null değil
			{
				tavs.Add(new { Name = "", Comment = "", Image = "" });
			}

			ViewBag.Tavs = tavs;

			switch (userRole)
			{
				case "Head Admin":
				case "Admin":
					return View();
				default:
					return RedirectToAction("Login", "AdmPanel");
			}
		}

		[HttpPost]
		public IActionResult TavSettings(string[] Name, string[] Tav, IFormFile[] ImageFile3)
		{
			string filePath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "settings3.xml");
			List<dynamic> tavsiyeler = new List<dynamic>();

			for (int i = 0; i < Name.Length; i++)
			{
				string imageFileName = $"{Guid.NewGuid()}.png";
				string imagePath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "img", "tav", imageFileName);

				using (var stream = new FileStream(imagePath, FileMode.Create))
				{
					ImageFile3[i].CopyTo(stream);
				}

				tavsiyeler.Add(new
				{
					Name = Name[i],
					Comment = Tav[i],
					Image = imageFileName
				});
			}

			var doc = new XDocument(
				new XElement("Settings",
					new XElement("Tavsiyeler",
						tavsiyeler.Select(t => new XElement("Tavsiye",
							new XElement("Name", t.Name),
							new XElement("Comment", t.Comment),
							new XElement("Image", t.Image)
						))
					)
				)
			);

			doc.Save(filePath);
			return RedirectToAction("TavSettings");
		}

		public IActionResult ContactSettings()
		{
			string xmlFilePath = Path.Combine(_hostingEnvironment.WebRootPath, "settings.xml");
			if (System.IO.File.Exists(xmlFilePath))
			{
				XElement settings = XElement.Load(xmlFilePath);
				ViewBag.SiteTitle = settings.Element("Title")?.Value; // Site ismini ViewBag ile taşı
			}
			// Kullanıcı ve rol kontrolü
			int? userId = HttpContext.Session.GetInt32("UserId");
			string userRole = HttpContext.Session.GetString("UserRole");
			string siteName = HttpContext.Session.GetString("SiteName");

			if (siteName != LoadSiteTitleFromXml())
			{
				return RedirectToAction("Home", "AdmPanel");
			}

			if (!userId.HasValue || string.IsNullOrEmpty(userRole))
			{
				return RedirectToAction("Login", "AdmPanel");
			}

			var user = _context.Staffs.FirstOrDefault(u => u.StaffID == userId.Value);
			if (user == null)
			{
				HttpContext.Session.Clear();
				return RedirectToAction("Login", "AdmPanel");
			}
			ViewBag.UserID = userId;
			ViewBag.UserRole = userRole;
			ViewBag.UserName = user.FirstName + " " + user.LastName;
			ViewBag.UserPosition = user.Position;
			// Rol kontrolü
			if (!(userRole.Equals("Head Admin") || userRole.Equals("Admin")))
			{
				return RedirectToAction("Login", "AdmPanel");
			}

			// XML dosyası yolu
			string filePath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "settings4.xml");

			// XML dosyasını okuyarak mevcut verileri yükleme
			if (System.IO.File.Exists(filePath))
			{
				var doc = XDocument.Load(filePath);
				var settings = doc.Element("ContactSettings");
				ViewBag.Phone = settings.Element("Phone")?.Value;
				ViewBag.Whatsapp = settings.Element("Whatsapp")?.Value;
				ViewBag.Email = settings.Element("Email")?.Value;
				ViewBag.Instagram = settings.Element("Instagram")?.Value;
				ViewBag.Twitter = settings.Element("Twitter")?.Value;
				ViewBag.Facebook = settings.Element("Facebook")?.Value;
				ViewBag.Youtube = settings.Element("Youtube")?.Value;
				ViewBag.Address = settings.Element("Address")?.Value;
			}
			else
			{
				// XML dosyası mevcut değilse tüm değerleri boş ayarla
				ViewBag.Phone = ViewBag.Whatsapp = ViewBag.Email = ViewBag.Instagram = ViewBag.Twitter = ViewBag.Facebook = ViewBag.Youtube = ViewBag.Address = "";
			}

			return View();
		}

		[HttpPost]
		public IActionResult ContactSettings(string Phone, string Whatsapp, string Email, string Instagram, string Twitter, string Facebook, string Youtube, string Address)
		{
			// Kullanıcı ve rol kontrolü
			int? userId = HttpContext.Session.GetInt32("UserId");
			string userRole = HttpContext.Session.GetString("UserRole");

			if (!userId.HasValue || string.IsNullOrEmpty(userRole))
			{
				return RedirectToAction("Login", "AdmPanel");
			}

			var user = _context.Staffs.FirstOrDefault(u => u.StaffID == userId.Value);
			if (user == null)
			{
				HttpContext.Session.Clear();
				return RedirectToAction("Login", "AdmPanel");
			}
			// Rol kontrolü
			if (!(userRole.Equals("Head Admin") || userRole.Equals("Admin")))
			{
				return RedirectToAction("Login", "AdmPanel");
			}

			// XML dosyası yolu
			string filePath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "settings4.xml");

			// Yeni veya mevcut XML dosyası oluşturma/güncelleme
			var doc = new XDocument(
				new XElement("ContactSettings",
					new XElement("Phone", string.IsNullOrEmpty(Phone) ? null : Phone),
					new XElement("Whatsapp", string.IsNullOrEmpty(Whatsapp) ? null : Whatsapp),
					new XElement("Email", string.IsNullOrEmpty(Email) ? null : Email),
					new XElement("Instagram", string.IsNullOrEmpty(Instagram) ? null : Instagram),
					new XElement("Twitter", string.IsNullOrEmpty(Twitter) ? null : Twitter),
					new XElement("Facebook", string.IsNullOrEmpty(Facebook) ? null : Facebook),
					new XElement("Youtube", string.IsNullOrEmpty(Youtube) ? null : Youtube),
					new XElement("Address", string.IsNullOrEmpty(Address) ? null : Address)
				)
			);

			// Dosya kaydetme
			doc.Save(filePath);

			// Başarıyla kaydedildikten sonra aynı sayfaya yönlendir
			return RedirectToAction("ContactSettings");
		}

		public IActionResult MyAppointments()
		{
			string xmlFilePath = Path.Combine(_hostingEnvironment.WebRootPath, "settings.xml");
			if (System.IO.File.Exists(xmlFilePath))
			{
				XElement settings = XElement.Load(xmlFilePath);
				ViewBag.SiteTitle = settings.Element("Title")?.Value; // Site ismini ViewBag ile taşı
			}
			int? userId = HttpContext.Session.GetInt32("UserId");
			string userRole = HttpContext.Session.GetString("UserRole");

			// Kullanıcı giriş yapmamışsa veya rol bilgisi yoksa login sayfasına yönlendir
			if (!userId.HasValue || string.IsNullOrEmpty(userRole))
			{
				return RedirectToAction("Login", "AdmPanel");
			}
			var user = _context.Staffs.FirstOrDefault(u => u.StaffID == userId.Value);

			if (!string.IsNullOrEmpty(user.FirstName) && user.FirstName.Length >= 2)
			{
				ViewBag.UserInitials = user.FirstName.Substring(0, 2); // İlk iki harfi al
			}
			else if (!string.IsNullOrEmpty(user.FirstName))
			{
				ViewBag.UserInitials = user.FirstName; // İsim iki harften kısa ise tüm ismi kullan
			}
			else
			{
				ViewBag.UserInitials = ""; // İsim boş veya null ise boş bir string ata
			}
			var now = DateTime.Now;

			// Önce gerekli randevuları veritabanından çekiyoruz
			var allAppointments = _context.Appointments
				.Where(a => a.StaffID == userId.Value &&
							(a.Status == "Onay Bekleniyor" || a.Status == "Ödeme Bekleniyor"))
				.ToList();

			// Bellek içinde zaman bazlı karşılaştırma yapıyoruz
			var outdatedAppointments = allAppointments
				.Where(a => a.Date.Add(a.Time) < now)
				.ToList();

			if (outdatedAppointments.Any())
			{
				_context.Appointments.RemoveRange(outdatedAppointments);
				_context.SaveChanges();
			}

			// Kullanıcının ilgili randevularını çek
			var appointments = _context.Appointments
				.Where(a => a.StaffID == userId.Value)
				.Select(a => new
				{
					AppointmentID = a.AppointmentID,
					Name = a.Name,
					Date = a.Date,
					Time = a.Time,
					EndTime = a.EndTime == null ? a.Time : a.EndTime,
					Status = a.Status,
					IsApproved = a.IsApproved,
					Email = a.Email,
					Price = a.Price,
					Comment = a.comment,
					Telefon = a.Telefon
				}).OrderBy(x=>x.IsApproved == false).ThenByDescending(x=>x.Date).ToList();

			// XML dosyasından online ödeme durumu oku
			string filePath = Path.Combine(_hostingEnvironment.WebRootPath, "settings5.xml");
			bool onlinePaymentEnabled = false;

			if (System.IO.File.Exists(filePath))
			{
				var doc = XDocument.Load(filePath);
				var onlinePaymentElement = doc.Element("Settings")?.Element("OnlinePayment");
				if (onlinePaymentElement != null && bool.TryParse(onlinePaymentElement.Value, out bool result))
				{
					onlinePaymentEnabled = result;
				}
			}

			ViewBag.OnlinePaymentEnabled = onlinePaymentEnabled;

			// ViewModel veya ViewBag ile view'a data taşı
			ViewBag.UserID = userId;
			ViewBag.Appointments = appointments;
			ViewBag.UserName = user.FirstName + " " + user.LastName;
			ViewBag.UserRole = userRole;

			// View döndür
			return View("MyAppointments");
		}

		[HttpPost]
		public IActionResult UpdateAppointment(int appointmentId, string firstName, string position, string phone, string email, string rejectionReason, bool isPaid, int? price, string action, TimeSpan endTime)
		{
			var appointment = _context.Appointments.FirstOrDefault(a => a.AppointmentID == appointmentId);
			if (appointment == null)
			{
				TempData["Error"] = "Randevu bulunamadı.";
				return RedirectToAction("Error");
			}

			var formattedDate = appointment.Date.ToString("dd MMMM yyyy");
			var formattedTime = appointment.Time.ToString(@"hh\:mm");
			switch (action.ToLower())
			{
				case "approve":
					if (isPaid && price.HasValue)
					{
						appointment.Status = "Ödeme Bekleniyor";
						appointment.Price = price.Value;
						appointment.PaymentDueDate = DateTime.UtcNow.AddMinutes(20); // 20 dakika ödeme süresi
						string encodedAppointmentId = Convert.ToBase64String(Encoding.UTF8.GetBytes(appointmentId.ToString()));
						string paymentLink = Url.Action("Payment", "AdmPanel", new { id = encodedAppointmentId }, Request.Scheme);
						string paymentMessage = $@"
                    <html>
                        <body>
                            <p>Merhaba {firstName},</p>
                            <p>{formattedDate} tarihinde, saat {formattedTime} için oluşturduğunuz randevu talebiniz {price.Value} TL tutarında ödeme beklemektedir. Ödemenizi tamamlamak için 20 dakikanız bulunmaktadır. Ödemenizi tamamlayarak randevunuzu kesinleştirebilirsiniz.</p>
                            <p><a href='{paymentLink}'>Ödeme yapmak için buraya tıklayınız</a></p>
                            <p>Teşekkürler.</p>
                        </body>
                    </html>";
						_emailService.SendPaymentRequestEmail(email, paymentMessage, price.Value, appointment.Date, appointment.Time, paymentLink);
						_smsService.SendPaymentRequestSms(phone, price.Value, appointment.Date, appointment.Time, paymentLink);
					}
					else
					{
						appointment.Status = "Onaylandı";
						//Iso
						appointment.IsApproved = true;
						appointment.EndTime = endTime;

						string confirmationMessage = $@"
                    <html>
                        <body>
                            <p>Merhaba {firstName},</p>
                            <p>{formattedDate} tarihinde, saat {formattedTime} için randevunuz başarıyla onaylandı. Belirtilen zamanda hizmet almak üzere hazır bulunmanız rica olunur.</p>
                            <p>Sorularınız için bizimle iletişime geçebilirsiniz.</p>
                            <p>İyi günler dileriz.</p>
                            <p>Randevu Sistemi Ekibi</p>
                        </body>
                    </html>";
						_emailService.SendConfirmationEmail(email, firstName, appointment.Date, appointment.Time);
						_smsService.SendConfirmationSms(phone, firstName, appointment.Date, appointment.Time);
					}
					break;

				case "reject":
					_context.Appointments.Remove(appointment); // Randevuyu veritabanından sil
					_context.SaveChanges();
					string rejectionMessage = $@"
                <html>
                    <body>
                        <p>Merhaba {firstName},</p>
                        <p>Maalesef {formattedDate} tarihinde, saat {formattedTime} için olan randevu talebiniz aşağıdaki sebep(ler) nedeniyle reddedilmiştir:</p>
                        <p>{rejectionReason}</p>
                        <p>Lütfen başka bir zaman aralığı seçiniz veya daha fazla bilgi için bizimle iletişime geçiniz.</p>
                        <p>Saygılarımızla,</p>
                        <p>Randevu Sistemi Ekibi</p>
                    </body>
                </html>";
					_emailService.SendRejectionEmail(email, firstName, appointment.Date, appointment.Time, rejectionReason);
					_smsService.SendRejectionSms(phone, firstName, appointment.Date, appointment.Time, rejectionReason);
					break;

				default:
					TempData["Error"] = "Geçersiz işlem.";
					return RedirectToAction("Error");
			}

			_context.SaveChanges(); // Değişiklikleri kaydet
			return RedirectToAction("MyAppointments"); // Randevu listesi sayfasına yönlendir
		}

		public IActionResult Appointments()
		{
			string xmlFilePath = Path.Combine(_hostingEnvironment.WebRootPath, "settings.xml");
			if (System.IO.File.Exists(xmlFilePath))
			{
				XElement settings = XElement.Load(xmlFilePath);
				ViewBag.SiteTitle = settings.Element("Title")?.Value; // Site ismini ViewBag ile taşı
			}
			int? userId = HttpContext.Session.GetInt32("UserId");
			string userRole = HttpContext.Session.GetString("UserRole");

			// Kullanıcı giriş yapmamışsa veya rol bilgisi yoksa login sayfasına yönlendir
			if (!userId.HasValue || string.IsNullOrEmpty(userRole))
			{
				return RedirectToAction("Login", "AdmPanel");
			}
			var user = _context.Staffs.FirstOrDefault(u => u.StaffID == userId.Value);

			if (!string.IsNullOrEmpty(user.FirstName) && user.FirstName.Length >= 2)
			{
				ViewBag.UserInitials = user.FirstName.Substring(0, 2); // İlk iki harfi al
			}
			else if (!string.IsNullOrEmpty(user.FirstName))
			{
				ViewBag.UserInitials = user.FirstName; // İsim iki harften kısa ise tüm ismi kullan
			}
			else
			{
				ViewBag.UserInitials = ""; // İsim boş veya null ise boş bir string ata
			}

			// Geçmişte kalmış ve onay bekleyen randevuları sil
			var outdatedAppointments = _context.Appointments
				.Where(a => a.StaffID == userId.Value && a.Status == "Onay Bekleniyor" && a.Date < DateTime.Today)
				.ToList();

			if (outdatedAppointments.Any())
			{
				_context.Appointments.RemoveRange(outdatedAppointments);
				_context.SaveChanges();
			}

			// Kullanıcının ilgili randevularını çek ve en yakın tarihten başlayarak sırala
			var appointments = _context.Appointments
				.Where(a => a.StaffID == userId.Value && a.Status == "Onaylandı")
				.ToList()
				.OrderBy(a => a.Date.Date.Add(a.Time)) // Bu kısım artık bellek üzerinde çalışacak
				.Select(a => new
				{
					AppointmentID = a.AppointmentID,
					Name = a.Name,
					DateTime = a.Date.Date.Add(a.Time),
					Status = a.Status,
					Email = a.Email,
					Telefon = a.Telefon,
					Price = a.Price,
					IsApproved = a.IsApproved,
					EndTime = a.EndTime,
				}).ToList();

			// ViewModel veya ViewBag ile view'a data taşı
			ViewBag.UserID = userId;
			ViewBag.Appointments = appointments;
			ViewBag.UserName = user.FirstName + " " + user.LastName;
			ViewBag.UserRole = userRole;

			// View döndür
			return View("Appointments"); // Varsayalım ki View'in ismi "Appointments.cshtml"
		}

		public IActionResult UserAppointments(int id)
		{
			string xmlFilePath = Path.Combine(_hostingEnvironment.WebRootPath, "settings.xml");
			if (System.IO.File.Exists(xmlFilePath))
			{
				XElement settings = XElement.Load(xmlFilePath);
				ViewBag.SiteTitle = settings.Element("Title")?.Value; // Site ismini ViewBag ile taşı
			}
			int? userId = HttpContext.Session.GetInt32("UserId");
			string userRole = HttpContext.Session.GetString("UserRole");
			string siteName = HttpContext.Session.GetString("SiteName");
			if (siteName != LoadSiteTitleFromXml())
			{
				return RedirectToAction("Home", "AdmPanel");
			}
			if (!userId.HasValue || string.IsNullOrEmpty(userRole))
			{
				// Eğer kullanıcı ID veya rol belirtilmemişse, kullanıcıyı login sayfasına yönlendir
				return RedirectToAction("Login", "AdmPanel");
			}
			var user = _context.Staffs.FirstOrDefault(u => u.StaffID == userId.Value);
			if (user == null)
			{
				// Kullanıcı bulunamadıysa oturumu sonlandır ve login sayfasına yönlendir
				HttpContext.Session.Clear();
				return RedirectToAction("Login", "AdmPanel");
			}
			if (!string.IsNullOrEmpty(user.FirstName) && user.FirstName.Length >= 2)
			{
				ViewBag.UserInitials = user.FirstName.Substring(0, 2); // İlk iki harfi al
			}
			else if (!string.IsNullOrEmpty(user.FirstName))
			{
				ViewBag.UserInitials = user.FirstName; // İsim iki harften kısa ise tüm ismi kullan
			}
			else
			{
				ViewBag.UserInitials = ""; // İsim boş veya null ise boş bir string ata
			}
			ViewBag.UserID = userId;
			ViewBag.UserRole = userRole;
			ViewBag.UserName = user.FirstName + " " + user.LastName;
			ViewBag.UserPosition = user.Position;

			switch (userRole)
			{
				case "Head Admin":
				case "Admin":
					// Geçmişte kalmış ve onay bekleyen randevuları sil
					var outdatedAppointments = _context.Appointments
						 .Where(a => a.StaffID == userId.Value &&
									 (a.Status == "Onay Bekleniyor" || a.Status == "Ödeme Bekleniyor") &&
									 a.Date < DateTime.Today)
						 .ToList();

					if (outdatedAppointments.Any())
					{
						_context.Appointments.RemoveRange(outdatedAppointments);
						_context.SaveChanges();
					}

					// Kullanıcının ilgili randevularını çek ve en yakın tarihten başlayarak sırala
					var appointments = _context.Appointments
						.Where(a => a.StaffID == id)
						.OrderByDescending(a => a.Status == "Onay Bekleniyor" || a.Status == "Ödeme Bekleniyor")
						.ThenBy(a => a.Date.Date.Add(a.Time))
						.Select(a => new
						{
							AppointmentID = a.AppointmentID,
							Name = a.Name,
							DateTime = a.Date.Date.Add(a.Time),
							Status = a.Status,
							Email = a.Email,
							Telefon = a.Telefon,
							Price = a.Price
						}).ToList();

					// ViewModel veya ViewBag ile view'a data taşı
					ViewBag.Appointments = appointments;
					ViewBag.UserName = user.FirstName + " " + user.LastName;
					ViewBag.UserRole = userRole;

					// View döndür
					return View("Appointments");
				default:
					// Rol tanımlanmamışsa veya tanımlı bir role uymuyorsa login sayfasına yönlendir
					return RedirectToAction("Login", "AdmPanel");
			}
		}


        private string GetUserIpAddress()
        {
            var ipAddress = HttpContext.Connection.RemoteIpAddress.ToString();
            return ipAddress;
        }




        [AllowAnonymous]
        public IActionResult Payment(string id)
        {
            string xmlFilePath = Path.Combine(_hostingEnvironment.WebRootPath, "settings.xml");
            if (System.IO.File.Exists(xmlFilePath))
            {
                XElement settings = XElement.Load(xmlFilePath);
                ViewBag.SiteTitle = settings.Element("Title")?.Value; // Site ismini ViewBag ile taşı
            }

            // if (string.IsNullOrEmpty(id))
            // {
            // 	return RedirectToAction("Error");
            // }

            // int appointmentId;

            // try
            // {
            // 	var decodedBytes = Convert.FromBase64String(id);
            // 	var decodedString = Encoding.UTF8.GetString(decodedBytes);
            // 	appointmentId = int.Parse(decodedString);
            // }
            // catch
            // {
            // 	return RedirectToAction("Error");
            // }

            int appointmentId = 1010;

            var appointment = _context.Appointments.FirstOrDefault(a => a.AppointmentID == appointmentId);

            if (appointment == null)
            {
                return RedirectToAction("Error");
            }

            // Eğer appointment ödeme beklenmiyorsa veya ödeme süresi geçmişse
            if (appointment.PaymentDueDate < DateTime.UtcNow || appointment.Status != "Ödeme Bekleniyor")
            {
                if (appointment.PaymentDueDate < DateTime.UtcNow)
                {
                    _context.Appointments.Remove(appointment);
                    _context.SaveChanges();
                }
                return appointment.Status != "Ödeme Bekleniyor" ? RedirectToAction("Index", "Home") : RedirectToAction("PaymentExpired", "AdmPanel");
            }

            var odemeModul = new 
            {
                basarili_url = "https://verandevum.com/Home/Basarili",
                hatali_url = "https://verandevum.com/Home/Hatali",
                islem_adi = "Randevu İşlemi",
                tutar = appointment.Price,
                creatAt = DateTime.Now,
                durumu = "Beklemede",
                name = appointment.Customer.FirstName,
                surname = appointment.Customer.FirstName,
                address = "Adres Gelmesi gerekiyor",
                mail = appointment.Email,
                phone = appointment.Telefon
            };

            // Redirect işlemi					buradaki sorgu işlemi ve class doldurma işlemleri tamam direkt ödemeye geçecek sadece başarılı olduğunda ilgili domain adresindeki basarili url e yönlendirip ödenmiştir diye buradaki appointment işlemine kayıt atıcam o kadar başka bir durum kalmıyor zaten sunucu tarafı da tamam olursa bitiyor öyle diyim size 
            string redirectUrl = $"https://odeme.verandevum.com/Odeme/Index?&tutar={odemeModul.tutar}&islem_adi={odemeModul.islem_adi}&creatAt={odemeModul.creatAt.ToString("o")}&durumu={odemeModul.durumu}&name={odemeModul.name}&surname={odemeModul.surname}&address={odemeModul.address}&mail={odemeModul.mail}&phone={odemeModul.phone}";

            return Redirect(redirectUrl);
        }

        [HttpPost]
		public IActionResult PaymentResponse([FromForm] PayTRResponse payTRResponse)
		{
			// ####### Bu kısımda herhangi bir değişiklik yapmanıza gerek yoktur. #######
			// 
			// POST değerleri ile hash oluştur.

			string Birlestir = string.Concat(payTRResponse.Merchant_oid, merchant_salt, payTRResponse.Status, payTRResponse.Total_amount);
			HMACSHA256 hmac = new(Encoding.UTF8.GetBytes(merchant_key));
			byte[] b = hmac.ComputeHash(Encoding.UTF8.GetBytes(Birlestir));
			string token = Convert.ToBase64String(b);

			//
			// Oluşturulan hash'i, paytr'dan gelen post içindeki hash ile karşılaştır (isteğin paytr'dan geldiğine ve değişmediğine emin olmak için)
			// Bu işlemi yapmazsanız maddi zarara uğramanız olasıdır.
			if (payTRResponse.Hash.ToString() != token)
			{
				// Response.Write("PAYTR notification failed: bad hash");
				return RedirectToAction("Error");
			}

			string siteTilte = LoadSiteTitleFromXml().Replace(" ", "");
			int appointmentId = Convert.ToInt32(payTRResponse.Merchant_oid.Replace(siteTilte, ""));

			//###########################################################################

			// BURADA YAPILMASI GEREKENLER
			// 1) Siparişin durumunu $post['merchant_oid'] değerini kullanarak veri tabanınızdan sorgulayın.
			// 2) Eğer sipariş zaten daha önceden onaylandıysa veya iptal edildiyse  echo "OK"; exit; yaparak sonlandırın.

			if (payTRResponse.Status == "success")
			{ //Ödeme Onaylandı

				// Bildirimin alındığını PayTR sistemine bildir.  
				// Response.Write("OK");

				// BURADA YAPILMASI GEREKENLER ONAY İŞLEMLERİDİR.
				// 1) Siparişi onaylayın.
				ApprovePayment(appointmentId, true);

				// 2) iframe çağırma adımında merchant_oid ve diğer bilgileri veri tabanınıza kayıp edip bu aşamada karşılaştırarak eğer var ise bilgieri çekebilir ve otomatik sipariş tamamlama işlemleri yaptırabilirsiniz.
				// 2) Eğer müşterinize mesaj / SMS / e-posta gibi bilgilendirme yapacaksanız bu aşamada yapabilirsiniz. Bu işlemide yine iframe çağırma adımında merchant_oid bilgisini kayıt edip bu aşamada sorgulayarak verilere ulaşabilirsiniz.
				// 3) 1. ADIM'da gönderilen payment_amount sipariş tutarı taksitli alışveriş yapılması durumunda
				// değişebilir. Güncel tutarı Request.Form['total_amount'] değerinden alarak muhasebe işlemlerinizde kullanabilirsiniz.

			}
			else
			{ //Ödemeye Onay Verilmedi

				// Bildirimin alındığını PayTR sistemine bildir.  
				// Response.Write("OK");
				DenyPayment();

				// BURADA YAPILMASI GEREKENLER
				// 1) Siparişi iptal edin.
				// 2) Eğer ödemenin onaylanmama sebebini kayıt edecekseniz aşağıdaki değerleri kullanabilirsiniz.
				// $post['failed_reason_code'] - başarısız hata kodu
				// $post['failed_reason_msg'] - başarısız hata mesajı
			}

			return Ok("OK");
		}

		public IActionResult PaymentExpired()
		{
			return View();
		}

		[HttpGet("/PaymentConfirmed/{id}")]
		public IActionResult PaymentConfirmed(int id)
		{
			var appointment = _context.Appointments
									  .Include(a => a.Staff)
									  .FirstOrDefault(a => a.AppointmentID == id);
			if (appointment == null)
			{
				return RedirectToAction("Index", "Home");
			}

			return View(appointment);
		}

		public IActionResult PaymentDenied()
		{
			return View();
		}

		[HttpGet("/AdmPanel/PaymentApproved/{appointmentId}")]
		public IActionResult PaymentApproved(int appointmentId)
		{
			var appointment = _context.Appointments
									  .Include(a => a.Staff)
									  .FirstOrDefault(a => a.AppointmentID == appointmentId);

			return RedirectToAction("PaymentConfirmed", "AdmPanel", new { id = appointmentId });
		}

		#region HELPER METHODS

		private PayTR PreparePayTr(Appointment appointment)
		{
			// ####################### DÜZENLEMESİ ZORUNLU ALANLAR #######################
			//
			// API Entegrasyon Bilgileri - Mağaza paneline giriş yaparak BİLGİ sayfasından alabilirsiniz.
			//
			// Müşterinizin sitenizde kayıtlı veya form vasıtasıyla aldığınız eposta adresi
			string emailstr = appointment.Email;
			//
			// Tahsil edilecek tutar. 9.99 için 9.99 * 100 = 999 gönderilmelidir.
			int payment_amountstr = (int)(appointment.Price * 100);
			//
			// Sipariş numarası: Her işlemde benzersiz olmalıdır!! Bu bilgi bildirim sayfanıza yapılacak bildirimde geri gönderilir.
			string merchant_oid = String.Format("{0} {1}", LoadSiteTitleFromXml(), appointment.AppointmentID.ToString()).Replace(" ", "");
			//
			// Müşterinizin sitenizde kayıtlı veya form aracılığıyla aldığınız ad ve soyad bilgisi
			string user_namestr = appointment.Name;
			//
			// Müşterinizin sitenizde kayıtlı veya form aracılığıyla aldığınız adres bilgisi
			string user_addressstr = "KONYA";
			//
			// Müşterinizin sitenizde kayıtlı veya form aracılığıyla aldığınız telefon bilgisi
			string user_phonestr = appointment.Telefon;
			//
			// Başarılı ödeme sonrası müşterinizin yönlendirileceği sayfa
			// !!! Bu sayfa siparişi onaylayacağınız sayfa değildir! Yalnızca müşterinizi bilgilendireceğiniz sayfadır!
			// !!! Siparişi onaylayacağız sayfa "Bildirim URL" sayfasıdır (Bakınız: 2.ADIM Klasörü).
			string merchant_ok_url = Url.Action("PaymentApproved", "AdmPanel", new { id = appointment.AppointmentID }, protocol: Request.Scheme);
			//
			// Ödeme sürecinde beklenmedik bir hata oluşması durumunda müşterinizin yönlendirileceği sayfa
			// !!! Bu sayfa siparişi iptal edeceğiniz sayfa değildir! Yalnızca müşterinizi bilgilendireceğiniz sayfadır!
			// !!! Siparişi iptal edeceğiniz sayfa "Bildirim URL" sayfasıdır (Bakınız: 2.ADIM Klasörü).
			string merchant_fail_url = Url.ActionLink("PaymentDenied", "AdmPanel", protocol: Request.Scheme);
			//        
			// !!! Eğer bu örnek kodu sunucuda değil local makinanızda çalıştırıyorsanız
			// buraya dış ip adresinizi (https://www.whatismyip.com/) yazmalısınız. Aksi halde geçersiz paytr_token hatası alırsınız.

			string user_ip = DetectIPAddress(Request);

			// string user_ip = Request.ServerVariables["HTTP_X_FORWARDED_FOR"];
			// if (user_ip == "" || user_ip == null)
			// {
			// 	user_ip = Request.ServerVariables["REMOTE_ADDR"];
			// }
			//

			// ÖRNEK user_basket oluşturma - Ürün adedine göre object'leri çoğaltabilirsiniz
			object[][] user_basket = {
			new object[] {"Randevu", payment_amountstr, 1}, // 1. ürün (Ürün Ad - Birim Fiyat - Adet)
            };
			/* ############################################################################################ */

			// İşlem zaman aşımı süresi - dakika cinsinden
			string timeout_limit = "30";
			//
			// Hata mesajlarının ekrana basılması için entegrasyon ve test sürecinde 1 olarak bırakın. Daha sonra 0 yapabilirsiniz.
			string debug_on = "1";
			//
			// Mağaza canlı modda iken test işlem yapmak için 1 olarak gönderilebilir.
			string test_mode = "0";
			//
			// Taksit yapılmasını istemiyorsanız, sadece tek çekim sunacaksanız 1 yapın
			string no_installment = "1";
			//
			// Sayfada görüntülenecek taksit adedini sınırlamak istiyorsanız uygun şekilde değiştirin.
			// Sıfır (0) gönderilmesi durumunda yürürlükteki en fazla izin verilen taksit geçerli olur.
			string max_installment = "0";
			//
			// Para birimi olarak TL, EUR, USD gönderilebilir. USD ve EUR kullanmak için kurumsal@paytr.com 
			// üzerinden bilgi almanız gerekmektedir. Boş gönderilirse TL geçerli olur.
			string currency = "TL";
			//
			// Türkçe için tr veya İngilizce için en gönderilebilir. Boş gönderilirse tr geçerli olur.
			string lang = "tr";

			// Gönderilecek veriler oluşturuluyor
			NameValueCollection data = new NameValueCollection();
			data["merchant_id"] = merchant_id;
			data["user_ip"] = user_ip;
			data["merchant_oid"] = merchant_oid;
			data["email"] = emailstr;
			data["payment_amount"] = payment_amountstr.ToString();
			//
			// Sepet içerği oluşturma fonksiyonu, değiştirilmeden kullanılabilir.
			// JavaScriptSerializer ser = new JavaScriptSerializer();
			string user_basket_json = JsonConvert.SerializeObject(user_basket); // ser.Serialize(user_basket);
			string user_basketstr = Convert.ToBase64String(Encoding.UTF8.GetBytes(user_basket_json));
			data["user_basket"] = user_basketstr;
			//
			// Token oluşturma fonksiyonu, değiştirilmeden kullanılmalıdır.
			string Birlestir = string.Concat(merchant_id, user_ip, merchant_oid, emailstr, payment_amountstr.ToString(), user_basketstr, no_installment, max_installment, currency, test_mode, merchant_salt);
			HMACSHA256 hmac = new HMACSHA256(Encoding.UTF8.GetBytes(merchant_key));
			byte[] b = hmac.ComputeHash(Encoding.UTF8.GetBytes(Birlestir));
			data["paytr_token"] = Convert.ToBase64String(b);
			//
			data["debug_on"] = debug_on;
			data["test_mode"] = test_mode;
			data["no_installment"] = no_installment;
			data["max_installment"] = max_installment;
			data["user_name"] = user_namestr;
			data["user_address"] = user_addressstr;
			data["user_phone"] = user_phonestr;
			data["merchant_ok_url"] = merchant_ok_url;
			data["merchant_fail_url"] = merchant_fail_url;
			data["timeout_limit"] = timeout_limit;
			data["currency"] = currency;
			data["lang"] = lang;

			PayTR payTR = new();

			try
			{
				using (WebClient client = new WebClient())
				{
					client.Headers.Add("Content-Type", "application/x-www-form-urlencoded");
					byte[] result = client.UploadValues("https://www.paytr.com/odeme/api/get-token", "POST", data);
					string ResultAuthTicket = Encoding.UTF8.GetString(result);
					dynamic json = JValue.Parse(ResultAuthTicket);

					payTR = JsonConvert.DeserializeObject<PayTR>(ResultAuthTicket);

					if (payTR.Status == "success")
					{
						payTR.Url = "https://www.paytr.com/odeme/guvenli/" + json.token;
						payTR.Visible = true;
					}
				}
			}
			catch (System.Exception ex)
			{
				throw ex;
			}


			return payTR;
		}

		private void ApprovePayment(int appointmentId, bool status)
		{
			var appointment = _context.Appointments
									  .Include(a => a.Staff)
									  .FirstOrDefault(a => a.AppointmentID == appointmentId);
			if (appointment == null)
			{
				return;
			}

			if (status)
			{
				appointment.Status = "Onaylandı";
				_context.SaveChanges();

				// Randevu onay e-postası gönderme
				_emailService.SendAppointmentConfirmationEmail(
					appointment.Email,
					appointment.Name,
					appointment.Date,
					appointment.Time,
					$"{appointment.Staff.FirstName} {appointment.Staff.LastName}",
					appointment.Price.Value);
				_smsService.SendAppointmentConfirmationSms(
					appointment.Telefon,
					appointment.Name,
					appointment.Date,
					appointment.Time,
					$"{appointment.Staff.FirstName} {appointment.Staff.LastName}",
					appointment.Price.Value);
				return;
			}
			else
			{
				return;
			}
		}

		private void DenyPayment()
		{
			return;
		}

		private async Task SaveFileAsync(IFormFile file, string path)
		{
			using (var stream = new FileStream(path, FileMode.Create))
			{
				await file.CopyToAsync(stream);
			}
		}

		private async Task ConvertToIconAsync(string imagePath, string iconPath, int iconWidth = 32, int iconHeight = 32)
		{
			using (var image = System.Drawing.Image.FromFile(imagePath))
			{
				using (var resized = new Bitmap(image, new System.Drawing.Size(iconWidth, iconHeight)))
				{
					// Bitmap'ten ikon handle'ı elde ediliyor
					IntPtr iconHandle = resized.GetHicon();
					try
					{
						using (var iconStream = new FileStream(iconPath, FileMode.Create))
						{
							// Handle'dan ikon oluşturuluyor ve dosyaya kaydediliyor
							Icon icon = Icon.FromHandle(iconHandle);
							icon.Save(iconStream);
							iconStream.Flush();
						}
					}
					finally
					{
						// Kullanılan handle'ı serbest bırak
						DestroyIcon(iconHandle);
					}
				}
			}
		}

		private string StripCountryCode(string phoneNumber)
		{
			if (phoneNumber.StartsWith("+90"))
			{
				return phoneNumber.Substring(3);
			}
			return phoneNumber;
		}

		private void RemovePreviousFailedAttempts(string userIP, string actionName)
		{
			var previousFailedAttempts = _context.ActionTracks
				.Where(at => at.ActionIP == userIP && at.ActionName == $"{actionName}-failed")
				.ToList();
			_context.ActionTracks.RemoveRange(previousFailedAttempts);
			_context.SaveChanges();
		}

		private string LoadSiteTitleFromXml()
		{
			var settingsPath = Path.Combine(_hostingEnvironment.WebRootPath, "settings.xml");
			if (System.IO.File.Exists(settingsPath))
			{
				var settings = XElement.Load(settingsPath);
				return settings.Element("Title")?.Value;
			}
			return "None";  // Eğer ayar dosyası yoksa veya başlık bulunamazsa varsayılan değer
		}

		private void SendActivationEmail(Customer customer)
		{
			try
			{
				var activationLink = Url.Action("Activate", "AdmPanel",
					new { code = customer.ActivationCode }, protocol: Request.Scheme);
				_emailService.SendActivationEmail(customer.Email, activationLink);
				_smsService.SendActivationSms(customer.Phone, activationLink);
			}
			catch (Exception ex)
			{
				throw new Exception($"Email sending failed: {ex.Message}");
			}
		}

		private string DecodeBase64(string encodedData)
		{
			var base64EncodedBytes = Convert.FromBase64String(encodedData);
			return Encoding.UTF8.GetString(base64EncodedBytes);
		}

		private string EncodeBase64(string input)
		{
			var plainTextBytes = Encoding.UTF8.GetBytes(input);
			return Convert.ToBase64String(plainTextBytes);
		}

		private bool IsValidEmail(string email)
		{
			try
			{
				var mailAddress = new MailAddress(email);
				return mailAddress.Address == email;
			}
			catch
			{
				return false;
			}
		}

		private void LogAction(string userIP, string actionName, string status, bool success)
		{
			if (!success)
			{
				var actionTrack = new ActionTrack
				{
					ActionIP = userIP,
					ActionName = $"{actionName}-failed",
					Date = DateTime.Now
				};

				_context.ActionTracks.Add(actionTrack);
				_context.SaveChanges();
			}
		}

		private static string DetectIPAddress(HttpRequest request)
		{
			return request.HttpContext.Connection.RemoteIpAddress?.ToString();
		}

		// "510.00:00:00" formatındaki string'i dakikaya çevirme
		private int ConvertTimeSpanStringToMinutes(string timeSpanString)
		{
			// "510.00:00:00" formatından saat kısmını al ve dakika cinsinden dönüştür
			int hours = int.Parse(timeSpanString.Split('.')[0]); // Saat değeri
			return hours;  // Saati direkt dakika olarak döndür, çünkü bu zaten dakika olarak verilmiş
		}

		[System.Runtime.InteropServices.DllImport("user32.dll", CharSet = CharSet.Auto)]
		extern static bool DestroyIcon(IntPtr handle);

		#endregion
	}
}