using System.Net;
using System.Net.Mail;
using System.Text;
using LisbergThreads.Contracts.Contact;

namespace LisbergThreads.Api.Services.Email;

public sealed class ContactEmailSender
{
    private readonly ILogger<ContactEmailSender> logger;
    private readonly string? host;
    private readonly int port;
    private readonly bool enableSsl;
    private readonly string? userName;
    private readonly string? password;
    private readonly MailAddress? from;
    private readonly MailAddress? recipient;
    public bool IsConfigured { get; }

    public ContactEmailSender(IConfiguration configuration, ILogger<ContactEmailSender> logger)
    {
        this.logger = logger;
        host = configuration["Smtp:Host"]?.Trim();
        userName = configuration["Smtp:UserName"];
        password = configuration["Smtp:Password"];
        if (string.IsNullOrWhiteSpace(host)
            || !int.TryParse(configuration["Smtp:Port"], out port) || port is < 1 or > 65535
            || !bool.TryParse(configuration["Smtp:EnableSsl"], out enableSsl)
            || !MailAddress.TryCreate(configuration["Smtp:FromAddress"], configuration["Smtp:FromName"], out var fromAddress)
            || !MailAddress.TryCreate(configuration["Contact:RecipientEmail"], out var recipientAddress)
            || string.IsNullOrWhiteSpace(userName) != string.IsNullOrWhiteSpace(password)) return;
        from = fromAddress;
        recipient = recipientAddress;
        IsConfigured = true;
    }

    public async Task<bool> SendAsync(ContactRequestDto request, CancellationToken cancellationToken)
    {
        if (!IsConfigured) return false;
        try
        {
            using var message = new MailMessage
            {
                From = from!,
                Subject = $"Lisberg Threads – {ContactTopics.All[request.Topic]}",
                SubjectEncoding = Encoding.UTF8, BodyEncoding = Encoding.UTF8, IsBodyHtml = false,
                Body = $"Navn: {request.Name.Trim()}\nE-mail: {request.Email.Trim()}\nEmne: {ContactTopics.All[request.Topic]}\n"
                    + (ContactTopics.UsesOrder(request.Topic) && !string.IsNullOrWhiteSpace(request.OrderNumber) ? $"Ordrenummer: {request.OrderNumber.Trim()}\n" : "")
                    + (ContactTopics.UsesProduct(request.Topic) && !string.IsNullOrWhiteSpace(request.Product) ? $"Produkt: {request.Product.Trim()}\n" : "")
                    + $"\nBesked:\n{request.Message.Trim()}"
            };
            message.To.Add(recipient!);
            message.ReplyToList.Add(new MailAddress(request.Email.Trim()));
            using var smtp = new SmtpClient(host!, port)
            {
                EnableSsl = enableSsl, UseDefaultCredentials = false, DeliveryMethod = SmtpDeliveryMethod.Network
            };
            if (!string.IsNullOrWhiteSpace(userName)) smtp.Credentials = new NetworkCredential(userName, password);
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(TimeSpan.FromSeconds(30));
            await smtp.SendMailAsync(message, timeout.Token);
            return true;
        }
        catch (Exception ex) when (ex is SmtpException or InvalidOperationException or FormatException or IOException or OperationCanceledException)
        {
            // Log aldrig modtagere, beskedindhold eller SMTP-fejlens detaljer.
            logger.LogWarning("Contact email could not be confirmed.");
            return false;
        }
    }
}
