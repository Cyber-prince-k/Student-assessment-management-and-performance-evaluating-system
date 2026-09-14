using System;
using System.Configuration;
using System.Net;
using System.Net.Mail;

namespace Assessment_management_and_performance_evaluation
{
    internal static class EmailService
    {
        private static string SmtpHost => ConfigurationManager.AppSettings["SmtpHost"] ?? "smtp.gmail.com";
        private static int SmtpPort
        {
            get
            {
                string portStr = ConfigurationManager.AppSettings["SmtpPort"];
                return int.TryParse(portStr, out int port) ? port : 587;
            }
        }

        private static string SenderEmail => ConfigurationManager.AppSettings["SenderEmail"] ?? string.Empty;
        private static string SenderAppPassword => ConfigurationManager.AppSettings["SenderAppPassword"] ?? string.Empty;
        private static bool EnableSsl
        {
            get
            {
                string sslStr = ConfigurationManager.AppSettings["EnableSsl"];
                return bool.TryParse(sslStr, out bool ssl) ? ssl : true;
            }
        }

        public static bool SendEmail(string recipientEmail, string subject, string body, out string errorMessage)
        {
            errorMessage = string.Empty;
            try
            {
                // Ensure TLS 1.2 or higher for secure modern SMTP handshakes
                ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;

                using (MailMessage mail = new MailMessage())
                {
                    mail.From = new MailAddress(SenderEmail, "School Administration");
                    mail.To.Add(recipientEmail);
                    mail.Subject = subject;
                    mail.Body = body;
                    mail.IsBodyHtml = false;

                    using (SmtpClient smtp = new SmtpClient(SmtpHost, SmtpPort))
                    {
                        // Explicitly set UseDefaultCredentials to false before setting Credentials
                        smtp.UseDefaultCredentials = false;
                        smtp.Credentials = new NetworkCredential(SenderEmail, SenderAppPassword);
                        smtp.EnableSsl = EnableSsl;
                        smtp.DeliveryMethod = SmtpDeliveryMethod.Network;
                        smtp.Timeout = 15000; // 15 seconds

                        smtp.Send(mail);
                    }
                }

                return true;
            }
            catch (SmtpException smtpEx)
            {
                errorMessage = $"SMTP Error ({smtpEx.StatusCode}): {smtpEx.Message}";
                if (smtpEx.InnerException != null)
                {
                    errorMessage += $" Details: {smtpEx.InnerException.Message}";
                }
                return false;
            }
            catch (Exception ex)
            {
                errorMessage = ex.Message;
                return false;
            }
        }
    }
}
