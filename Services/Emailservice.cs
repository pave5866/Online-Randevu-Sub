using Microsoft.Extensions.Options;
using Randevu.Models;
using System.Net;
using System.Net.Mail;
using iTextSharp.text;
using iTextSharp.text.pdf;
using System.Xml.Linq;
using System.IO;
using System.Globalization;
using System;

namespace Randevu.Controllers
{
    public class EmailService
    {
        private readonly SmtpSetting _smtpSettings;

        public EmailService(IOptions<SmtpSetting> smtpSettings)
        {
            _smtpSettings = smtpSettings.Value;
        }

        public void SendActivationEmail(string to, string activationLink)
        {
            if (string.IsNullOrEmpty(_smtpSettings.FromAddress))
            {
                throw new ArgumentNullException("FromAddress", "A valid 'From' address must be specified.");
            }

            var message = new MailMessage();
            message.To.Add(new MailAddress(to));
            message.Subject = "Hesap Aktivasyonu";
            message.Body = $"Lütfen hesabınızı aktive etmek için aşağıdaki bağlantıya tıklayın:\n\n{activationLink}";
            message.IsBodyHtml = false;
            message.From = new MailAddress(_smtpSettings.FromAddress);

            using (var smtpClient = new SmtpClient(_smtpSettings.Host, _smtpSettings.Port))
            {
                smtpClient.EnableSsl = _smtpSettings.EnableSsl;
                smtpClient.Credentials = new NetworkCredential(_smtpSettings.Username, _smtpSettings.Password);
                smtpClient.Send(message);
            }
        }

        public void SendActivationAppointmentEmail(string to, string name, DateTime date, TimeSpan time, string staffFullName)
        {
            var formattedDate = date.ToString("dd MMMM yyyy");
            var formattedTime = time.ToString(@"hh\:mm");
            var body = $@"
    <html>
        <body>
            <p>Merhaba {name},</p>
            <p>Randevu tarihi: <strong>{formattedDate}</strong> saat <strong>{formattedTime}</strong> için oluşturmuş olduğunuz randevu talebi başarıyla oluşturulmuştur.</p>
            <p>Randevunuzun onaylanması için <strong>{staffFullName}</strong> kişisinin onaylamasını beklemeniz gerekecektir. Onaylandıktan sonra size tekrar bilgilendirme maili gelecektir.</p>
            <p>Saygılarımızla,</p>
            <p>Randevu Sistemi Ekibi</p>
        </body>
    </html>";

            var mailMessage = new MailMessage
            {
                From = new MailAddress(_smtpSettings.FromAddress),
                Subject = "Randevu Talebiniz Başarıyla Alındı",
                Body = body,
                IsBodyHtml = true
            };
            mailMessage.To.Add(new MailAddress(to));

            using (var smtpClient = new SmtpClient(_smtpSettings.Host, _smtpSettings.Port))
            {
                smtpClient.EnableSsl = _smtpSettings.EnableSsl;
                smtpClient.Credentials = new NetworkCredential(_smtpSettings.Username, _smtpSettings.Password);
                smtpClient.Send(mailMessage);
            }
        }

        public void SendActivationStaffAppointmentEmail(string to, string name, DateTime date, TimeSpan time, string phone, string email, string comment, string reviewLink)
        {
            var formattedDate = date.ToString("dd MMMM yyyy");
            var formattedTime = time.ToString(@"hh\:mm");

            var body = $@"
    <html>
        <body>
            <p>Merhaba,</p>
            <p>Size yeni bir randevu talebi geldi. Randevu detayları aşağıda yer almaktadır:</p>
            <ul>
                <li>İsim: {name}</li>
                <li>Randevu Tarihi: {formattedDate} saat {formattedTime}</li>
                <li>İletişim Bilgileri: Telefon: {phone}, E-posta: {email}</li>";

            if (!string.IsNullOrEmpty(comment))
            {
                body += $"<li>Yorum: {comment}</li>";
            }

            body += $@"
            </ul>
            <p>Lütfen randevuyu onaylamak veya reddetmek için aşağıdaki bağlantıya tıklayın:</p>
            <p><a href='{reviewLink}'>Randevu İnceleme</a></p>
            <p>Saygılarımızla,</p>
            <p>Randevu Sistemi Ekibi</p>
        </body>
    </html>";

            var mailMessage = new MailMessage
            {
                From = new MailAddress(_smtpSettings.FromAddress),
                Subject = "Yeni Randevu Talebi",
                Body = body,
                IsBodyHtml = true
            };
            mailMessage.To.Add(new MailAddress(to));

            using (var smtpClient = new SmtpClient(_smtpSettings.Host, _smtpSettings.Port))
            {
                smtpClient.EnableSsl = _smtpSettings.EnableSsl;
                smtpClient.Credentials = new NetworkCredential(_smtpSettings.Username, _smtpSettings.Password);
                smtpClient.Send(mailMessage);
            }
        }

        public void SendPasswordResetEmail(string to, string resetLink)
        {
            if (string.IsNullOrEmpty(_smtpSettings.FromAddress))
            {
                throw new ArgumentNullException("FromAddress", "A valid 'From' address must be specified.");
            }

            var message = new MailMessage();
            message.To.Add(new MailAddress(to));
            message.Subject = "Şifre Sıfırlama Talebi";
            message.Body = $"Şifrenizi sıfırlamak için lütfen aşağıdaki bağlantıya tıklayın:\n\n{resetLink}";
            message.IsBodyHtml = false;
            message.From = new MailAddress(_smtpSettings.FromAddress);

            using (var smtpClient = new SmtpClient(_smtpSettings.Host, _smtpSettings.Port))
            {
                smtpClient.EnableSsl = _smtpSettings.EnableSsl;
                smtpClient.Credentials = new NetworkCredential(_smtpSettings.Username, _smtpSettings.Password);
                smtpClient.Send(message);
            }
        }

        public void SendPaymentRequestEmail(string to, string message, int price, DateTime appointmentDate, TimeSpan appointmentTime, string paymentLink)
        {
            if (string.IsNullOrEmpty(_smtpSettings.FromAddress))
            {
                throw new ArgumentNullException("FromAddress", "A valid 'From' address must be specified.");
            }

            var formattedDate = appointmentDate.ToString("dd MMMM yyyy");
            var formattedTime = appointmentTime.ToString(@"hh\:mm");

            var body = $@"
    <html>
        <head>
            <style>
                body {{ font-family: 'Arial', sans-serif; }}
                p {{ margin: 0 0 10px 0; }}
            </style>
        </head>
        <body>
            <p>Merhaba,</p>
            <p>{formattedDate} tarihinde, saat {formattedTime} için oluşturduğunuz {price} TL tutarındaki randevu talebiniz ödeme beklemektedir. Ödemenizi tamamlamak için 20 dakikanız bulunmaktadır. Ödemenizi tamamlayarak randevunuzu kesinleştirebilirsiniz.</p>
            <p><a href='{paymentLink}'>Ödeme yapmak için buraya tıklayınız</a></p>
            <p>Teşekkürler.</p>
        </body>
    </html>";

            var mailMessage = new MailMessage
            {
                From = new MailAddress(_smtpSettings.FromAddress),
                Subject = "Randevu Ödeme Onayı Gerekiyor",
                Body = body,
                IsBodyHtml = true // E-posta içeriğinin HTML olduğunu belirtin
            };

            mailMessage.To.Add(new MailAddress(to));

            using (var smtpClient = new SmtpClient(_smtpSettings.Host, _smtpSettings.Port))
            {
                smtpClient.EnableSsl = _smtpSettings.EnableSsl;
                smtpClient.Credentials = new NetworkCredential(_smtpSettings.Username, _smtpSettings.Password);
                smtpClient.Send(mailMessage);
            }
        }

        public void SendConfirmationEmail(string to, string name, DateTime date, TimeSpan time)
        {
            var formattedDate = date.ToString("dd MMMM yyyy");
            var formattedTime = time.ToString(@"hh\:mm");

            var body = $@"
    <html>
        <body>
            <p>Merhaba {name},</p>
            <p>{formattedDate} tarihinde, saat {formattedTime} için randevunuz başarıyla onaylandı. Belirtilen zamanda hizmet almak üzere hazır bulunmanız rica olunur.</p>
            <p>Sorularınız için bizimle iletişime geçebilirsiniz.</p>
            <p>İyi günler dileriz.</p>
            <p>Saygılarımızla,</p>
            <p>Randevu Sistemi Ekibi</p>
        </body>
    </html>";

            var mailMessage = new MailMessage(_smtpSettings.FromAddress, to)
            {
                Subject = "Randevunuz Onaylandı",
                Body = body,
                IsBodyHtml = true
            };

            using (var smtpClient = new SmtpClient(_smtpSettings.Host, _smtpSettings.Port))
            {
                smtpClient.EnableSsl = _smtpSettings.EnableSsl;
                smtpClient.Credentials = new NetworkCredential(_smtpSettings.Username, _smtpSettings.Password);
                smtpClient.Send(mailMessage);
            }
        }

        public void SendRejectionEmail(string to, string name, DateTime date, TimeSpan time, string reason)
        {
            var formattedDate = date.ToString("dd MMMM yyyy");
            var formattedTime = time.ToString(@"hh\:mm");

            var body = $@"
    <html>
        <body>
            <p>Merhaba {name},</p>
            <p>Maalesef {formattedDate} tarihinde, saat {formattedTime} için olan randevu talebiniz aşağıdaki sebep(ler) nedeniyle reddedilmiştir:</p>
            <p>{reason}</p>
            <p>Lütfen başka bir zaman aralığı seçiniz veya daha fazla bilgi için bizimle iletişime geçiniz.</p>
            <p>Saygılarımızla,</p>
            <p>Randevu Sistemi Ekibi</p>
        </body>
    </html>";

            var mailMessage = new MailMessage(_smtpSettings.FromAddress, to)
            {
                Subject = "Randevu Talebiniz Reddedildi",
                Body = body,
                IsBodyHtml = true
            };

            using (var smtpClient = new SmtpClient(_smtpSettings.Host, _smtpSettings.Port))
            {
                smtpClient.EnableSsl = _smtpSettings.EnableSsl;
                smtpClient.Credentials = new NetworkCredential(_smtpSettings.Username, _smtpSettings.Password);
                smtpClient.Send(mailMessage);
            }
        }

        public void SendAppointmentConfirmationEmail(string to, string name, DateTime appointmentDate, TimeSpan appointmentTime, string staffFullName, decimal price)
        {
            if (string.IsNullOrEmpty(_smtpSettings.FromAddress))
            {
                throw new ArgumentNullException("FromAddress", "A valid 'From' address must be specified.");
            }

            // settings.xml dosyasından site başlığını oku
            string xmlFilePath = Path.Combine(Environment.CurrentDirectory, "wwwroot", "settings.xml");
            XElement settings = XElement.Load(xmlFilePath);
            string siteTitle = settings.Element("Title").Value;  // Title elementini oku

            // PDF belgesini oluştur
            byte[] pdfBytes = CreatePdfInvoice(name, appointmentDate, appointmentTime, staffFullName, price);

            var formattedDate = appointmentDate.ToString("dd MMMM yyyy");
            var formattedTime = appointmentTime.ToString(@"hh\:mm");
            var subject = "Randevu Onaylandı";
            var body = $@"
        <html>
        <body>
            <p>Merhaba {name},</p>
            <p>Randevunuz aşağıdaki bilgiler ile onaylanmıştır:</p>
            <ul>
                <li>Tarih: {formattedDate}</li>
                <li>Saat: {formattedTime}</li>
                <li>Randevu Aldığınız Kişi: {staffFullName}</li>
                <li>Ücret: {price} TL</li>
            </ul>
            <p>Randevu gününde sizi görmekten mutluluk duyacağız!</p>
            <p>Saygılarımızla,</p>
            <p>{siteTitle} Ekibi</p>  <!-- Site başlığı kullanılarak dinamik bir imza oluşturuldu -->
        </body>
        </html>";

            var message = new MailMessage()
            {
                From = new MailAddress(_smtpSettings.FromAddress),
                Subject = subject,
                Body = body,
                IsBodyHtml = true
            };
            message.To.Add(new MailAddress(to));

            // PDF faturayı ekle
            var attachment = new Attachment(new MemoryStream(pdfBytes), "Fatura.pdf", "application/pdf");
            message.Attachments.Add(attachment);

            using (var smtpClient = new SmtpClient(_smtpSettings.Host, _smtpSettings.Port))
            {
                smtpClient.EnableSsl = _smtpSettings.EnableSsl;
                smtpClient.Credentials = new NetworkCredential(_smtpSettings.Username, _smtpSettings.Password);
                smtpClient.Send(message);
            }
        }

        private byte[] CreatePdfInvoice(string name, DateTime date, TimeSpan time, string staffName, decimal price)
        {
            using (MemoryStream memoryStream = new MemoryStream())
            {
                Document document = new Document(PageSize.A4, 50, 50, 50, 50); // Sayfa boyutu ve kenar boşlukları
                PdfWriter writer = PdfWriter.GetInstance(document, memoryStream);
                document.Open();

                // Logo ekleme
                string imagePath = Path.Combine(Environment.CurrentDirectory, "wwwroot", "img", "logo.png");
                Image logo = Image.GetInstance(imagePath);
                logo.ScaleToFit(100, 100); // Logo boyutu
                logo.SetAbsolutePosition(document.Left + 15, document.Top - 50); // Logo konumu
                document.Add(logo);

                // Başlık
                Font titleFont = new Font(Font.FontFamily.HELVETICA, 18, Font.BOLD);
                Paragraph header = new Paragraph("Fatura", titleFont)
                {
                    Alignment = Element.ALIGN_CENTER,
                    SpacingAfter = 20
                };
                document.Add(header);

                // Fatura içeriği - Metin yerine tablo kullanarak daha düzenli gösterim
                PdfPTable table = new PdfPTable(2);
                table.WidthPercentage = 100;
                table.SpacingBefore = 10;
                table.SpacingAfter = 10;

                // Tablo sütunlarını eşit olarak ayarla
                table.SetWidths(new float[] { 1, 3 });

                // Tablo başlıkları
                AddCellToTable(table, "Adı", true);
                AddCellToTable(table, name, false);
                AddCellToTable(table, "Tarih", true);
                AddCellToTable(table, date.ToString("dd MMMM yyyy"), false);
                AddCellToTable(table, "Saat", true);
                AddCellToTable(table, time.ToString("hh\\:mm"), false);
                AddCellToTable(table, "Personel", true);
                AddCellToTable(table, staffName, false);
                AddCellToTable(table, "Tutar", true);
                AddCellToTable(table, price.ToString("C2", CultureInfo.CreateSpecificCulture("tr-TR")) + " TL", false); // "C2" - Para formatı

                document.Add(table);

                // Bilgilendirme notu
                Font disclaimerFont = new Font(Font.FontFamily.HELVETICA, 8, Font.ITALIC);
                Paragraph disclaimer = new Paragraph("Bu belge yalnizca bilgilendirme amaclidir ve resmi bir belge degildir.", disclaimerFont)
                {
                    Alignment = Element.ALIGN_CENTER,
                    SpacingBefore = 20
                };
                document.Add(disclaimer);

                document.Close();
                writer.Close();

                return memoryStream.ToArray();
            }
        }

        private void AddCellToTable(PdfPTable table, string content, bool isHeader)
        {
            Font cellFont = isHeader ? new Font(Font.FontFamily.HELVETICA, 12, Font.BOLD) : new Font(Font.FontFamily.HELVETICA, 12);
            PdfPCell cell = new PdfPCell(new Phrase(content, cellFont))
            {
                HorizontalAlignment = Element.ALIGN_LEFT,
                Padding = 8,
                Border = PdfPCell.NO_BORDER
            };
            table.AddCell(cell);
        }

    }

}

