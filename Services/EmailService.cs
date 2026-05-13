using System.Configuration;
using System.Net;
using System.Net.Mail;
using System.Threading.Tasks;

namespace ProjectDish.Services
{
    public class EmailService
    {
        private static readonly string SmtpHost = ConfigurationManager.AppSettings["SmtpHost"];
        private static readonly int SmtpPort = int.Parse(ConfigurationManager.AppSettings["SmtpPort"]);
        private static readonly bool SmtpUseSsl = bool.Parse(ConfigurationManager.AppSettings["SmtpUseSsl"]);
        private static readonly string SmtpUser = ConfigurationManager.AppSettings["SmtpUser"];
        private static readonly string SmtpPassword = ConfigurationManager.AppSettings["SmtpPassword"];
        private static readonly string FromEmail = ConfigurationManager.AppSettings["FromEmail"];

        public static async Task SendPasswordResetCodeAsync(string toEmail, string code)
        {
            string subject = "Восстановление пароля в ProjectDishes";
            string body = $"Здравствуйте!\n\nВы запросили восстановление пароля.\nВаш код: {code}\n\nКод действителен 15 минут.";

            var message = new MailMessage(FromEmail, toEmail, subject, body);

            using var smtp = new SmtpClient(SmtpHost, SmtpPort)
            {
                EnableSsl = SmtpUseSsl,
                Credentials = new NetworkCredential(SmtpUser, SmtpPassword)
            };

            await smtp.SendMailAsync(message);
        }
    }
}
