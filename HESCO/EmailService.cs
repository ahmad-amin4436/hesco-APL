using Microsoft.Extensions.Configuration;
using System.Net;
using System.Net.Mail;
using System.Threading.Tasks;

public class EmailService
{
    private readonly IConfiguration _configuration;

    public EmailService(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public async Task SendEmailWithAttachmentAsync(string subject, string body, string toEmail, string fromEmail, List<string> ccEmails, byte[] attachment, string attachmentName)
    {
        var smtpClient = new SmtpClient(_configuration["EmailSettings:SmtpHost"])
        {
            Port = int.Parse(_configuration["EmailSettings:SmtpPort"]),
            Credentials = new NetworkCredential(_configuration["EmailSettings:SmtpUser"], _configuration["EmailSettings:SmtpPass"]),
            EnableSsl = bool.Parse(_configuration["EmailSettings:UseSSL"])
        };
        var mailMessage = new MailMessage
        {
            From = new MailAddress(fromEmail),
            Subject = subject,
            Body = body,
            IsBodyHtml = true,
        };

        mailMessage.To.Add(toEmail);

        foreach (var ccEmail in ccEmails)
        {
            mailMessage.CC.Add(ccEmail);
        }
        using (var ms = new MemoryStream(attachment))
        {
            mailMessage.Attachments.Add(new Attachment(ms, attachmentName));
            await smtpClient.SendMailAsync(mailMessage);
        }
    }
}
