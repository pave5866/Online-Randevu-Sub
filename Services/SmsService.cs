using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;
using Microsoft.Extensions.Options;
using Randevu.Models;

namespace Randevu.Controllers
{
    public class SmsService(IOptions<SmsSettings> smsSettings)
    {
        private readonly SmsSettings _smsSettings = smsSettings.Value;

        private async Task SendSmsAsync(SmsModel smsModel)
        {
            try
            {
                var httpClient = new HttpClient();

                // XML belgesini oluştur
                var smspack = new XElement("smspack",
                    new XAttribute("ka", _smsSettings.Ka),
                    new XAttribute("pwd", _smsSettings.Pwd)
                );

                if (!string.IsNullOrEmpty(_smsSettings.Org))
                {
                    XAttribute attr = new XAttribute("org", _smsSettings.Org);
                    smspack.Add(attr);
                };

                // Her bir mesaj için XML elementini ekle
                // foreach (var (message, numbers) in messages)
                // {
                var mesajElement = new XElement("mesaj",
                    new XElement("metin", smsModel.Content),
                    new XElement("nums", string.Join(",", smsModel.NumberList))
                );

                smspack.Add(mesajElement);
                // }

                // XDocument'i stringe dönüştür
                var xmlString = new XDocument(new XDeclaration("1.0", "UTF-8", "yes"), smspack).ToString();

                // StringContent oluştur ve Content-Type'ı ayarla
                var content = new StringContent(xmlString, Encoding.UTF8, "text/xml");

                // POST isteği gönder
                var response = await httpClient.PostAsync(_smsSettings.Url, content);

                if (response.IsSuccessStatusCode)
                {
                    var responseContent = await response.Content.ReadAsStringAsync();
                    // Yanıtı işleyin
                }
                else
                {
                    // Hata durumunu işleyin
                }
            }
            catch (System.Exception ex)
            {
                throw ex;
            }

        }

        public void SendPasswordResetSms(string to, string resetLink)
        {

            if (string.IsNullOrEmpty(to))
            {
                throw new ArgumentNullException("number", "A valid 'number' address must be specified.");
            }

            SmsModel smsModel = new SmsModel()
            {
                NumberList = [to],
                Content = $"Şifrenizi sıfırlamak için lütfen bağlantıya tıklayın: {resetLink}"
            };

            SendSmsAsync(smsModel);
        }

        public void SendActivationSms(string to, string activationLink)
        {
            if (string.IsNullOrEmpty(to))
            {
                throw new ArgumentNullException("FromAddress", "A valid 'From' address must be specified.");
            }

            SmsModel smsModel = new SmsModel()
            {
                NumberList = [to],
                Content = $"Hesabınızı aktive etmek için bağlantıya tıklayın: {activationLink}"
            };

            SendSmsAsync(smsModel);
        }

        public void SendActivationAppointmentSms(string to, string name, DateTime date, TimeSpan time, string staffFullName)
        {
            var formattedDate = date.ToString("dd MMMM yyyy");
            var formattedTime = time.ToString(@"hh\:mm");
            var content = $@"Sayın {name}, {formattedDate} - {formattedTime} için randevu talebi başarıyla oluşturulmuştur. 
            {staffFullName} onayından sonra sms ve mail ile bilgilendirileceksiniz. Saygılarımızla.";

            SmsModel smsModel = new SmsModel()
            {
                NumberList = [to],
                Content = content
            };

            SendSmsAsync(smsModel);
        }

        public void SendActivationStaffAppointmentSms(string to, string reviewLink)
        {
            var content = $@"Yeni bir randevu talebiniz mevcuttur, onaylamak veya reddetmek için bağlantıya tıklayın: {reviewLink}";

            SmsModel smsModel = new SmsModel()
            {
                NumberList = [to],
                Content = content
            };

            SendSmsAsync(smsModel);
        }

        public void SendPaymentRequestSms(string to, int price, DateTime appointmentDate, TimeSpan appointmentTime, string paymentLink)
        {
            if (string.IsNullOrEmpty(to))
            {
                throw new ArgumentNullException("FromAddress", "A valid 'From' address must be specified.");
            }

            var formattedDate = appointmentDate.ToString("dd MMMM yyyy");
            var formattedTime = appointmentTime.ToString(@"hh\:mm");

            var content = $@"Merhaba, {formattedDate} tarihinde, saat {formattedTime} için oluşturduğunuz {price} TL tutarındaki randevu talebiniz ödeme beklemektedir.
                    Ödemenizi tamamlamak için 20 dakikanız bulunmaktadır. {paymentLink}";

            SmsModel smsModel = new()
            {
                NumberList = [to],
                Content = content
            };

            SendSmsAsync(smsModel);
        }

        public void SendConfirmationSms(string to, string name, DateTime date, TimeSpan time)
        {
            var formattedDate = date.ToString("dd MMMM yyyy");
            var formattedTime = time.ToString(@"hh\:mm");

            var content = $@"Merhaba {name}, {formattedDate} tarihinde, saat {formattedTime} için randevunuz başarıyla onaylandı. Saygılarımızla";

            SmsModel smsModel = new()
            {
                NumberList = [to],
                Content = content
            };

            SendSmsAsync(smsModel);
        }

        public void SendRejectionSms(string to, string name, DateTime date, TimeSpan time, string reason)
        {
            var formattedDate = date.ToString("dd MMMM yyyy");
            var formattedTime = time.ToString(@"hh\:mm");

            var content = $@"Merhaba {name}, {formattedDate} tarihinde, saat {formattedTime} için olan randevu talebiniz '{reason}' sebebiyle reddedilmiştir. Saygılarımızla";

            SmsModel smsModel = new()
            {
                NumberList = [to],
                Content = content
            };

            SendSmsAsync(smsModel);
        }

        public void SendAppointmentConfirmationSms(string to, string name, DateTime appointmentDate, TimeSpan appointmentTime, string staffFullName, decimal price)
        {
            if (string.IsNullOrEmpty(to))
            {
                throw new ArgumentNullException("FromAddress", "A valid 'From' address must be specified.");
            }

            // settings.xml dosyasından site başlığını oku
            string xmlFilePath = Path.Combine(Environment.CurrentDirectory, "wwwroot", "settings.xml");
            XElement settings = XElement.Load(xmlFilePath);
            string siteTitle = settings.Element("Title").Value;  // Title elementini oku


            var formattedDate = appointmentDate.ToString("dd MMMM yyyy");
            var formattedTime = appointmentTime.ToString(@"hh\:mm");
            var subject = "Randevu Onaylandı";
            var content = $@"
                Merhaba {name}, {staffFullName} işletmesinden {formattedDate} - {formattedTime} tarih ve {price} TL ücretli randevunuz onaylandı.
                Randevu gününde sizi görmekten mutluluk duyacağız!
                Saygılarımızla,
                {siteTitle} Ekibi
            ";

            SmsModel smsModel = new()
            {
                NumberList = [to],
                Content = content
            };

            SendSmsAsync(smsModel);
        }
    }
}