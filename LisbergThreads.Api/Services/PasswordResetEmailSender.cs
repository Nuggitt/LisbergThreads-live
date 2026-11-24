using System.Net;
using System.Net.Mail;
using System.Text;

namespace LisbergThreads.Api.Services;

public sealed class PasswordResetEmailSender
{
    private readonly ILogger<PasswordResetEmailSender> _logger;
    private readonly string? _host;
    private readonly int _port;
    private readonly bool _enableSsl;
    private readonly string? _userName;
    private readonly string? _password;
    private readonly MailAddress? _from;
    private readonly string? _clientBaseUrl;

    public bool IsConfigured { get; }

    public PasswordResetEmailSender(
        IConfiguration configuration,
        ILogger<PasswordResetEmailSender> logger)
    {
        _logger = logger;
        _host = configuration["Smtp:Host"]?.Trim();
        _userName = configuration["Smtp:UserName"];
        _password = configuration["Smtp:Password"];
        var fromAddress = configuration["Smtp:FromAddress"];
        var clientBaseUrl = configuration["PasswordReset:ClientBaseUrl"]?.Trim();

        if (string.IsNullOrWhiteSpace(_host)
            || !int.TryParse(configuration["Smtp:Port"], out _port)
            || _port is < 1 or > 65535
            || !bool.TryParse(configuration["Smtp:EnableSsl"], out _enableSsl)
            || !MailAddress.TryCreate(fromAddress, configuration["Smtp:FromName"], out var from)
            || !Uri.TryCreate(clientBaseUrl, UriKind.Absolute, out var clientUri)
            || (clientUri.Scheme != Uri.UriSchemeHttps && clientUri.Scheme != Uri.UriSchemeHttp)
            || !string.IsNullOrEmpty(clientUri.UserInfo)
            || !string.IsNullOrEmpty(clientUri.Query)
            || !string.IsNullOrEmpty(clientUri.Fragment)
            || string.IsNullOrWhiteSpace(_userName) != string.IsNullOrWhiteSpace(_password))
        {
            return;
        }

        _from = from;
        _clientBaseUrl = clientUri.AbsoluteUri.TrimEnd('/');
        IsConfigured = true;
    }

    public async Task SendAsync(string email, string token, CancellationToken cancellationToken)
    {
        if (!IsConfigured)
        {
            throw new InvalidOperationException("Password reset email is not configured.");
        }

        try
        {
            var resetUrl = $"{_clientBaseUrl}/reset-password?email={Uri.EscapeDataString(email)}&token={Uri.EscapeDataString(token)}";
            using var message = new MailMessage
            {
                From = _from!,
                Subject = "Nulstil din adgangskode hos Lisberg Threads",
                SubjectEncoding = Encoding.UTF8,
                BodyEncoding = Encoding.UTF8,
                IsBodyHtml = false,
                Body = $"Du har bedt om at nulstille din adgangskode hos Lisberg Threads.\n\nÅbn dette link for at vælge en ny adgangskode:\n{resetUrl}\n\nLinket udløber efter én time og kan kun bruges én gang.\nHvis du ikke har bedt om en nulstilling, kan du ignorere denne e-mail."
            };
            message.To.Add(new MailAddress(email));

            using var smtp = new SmtpClient(_host!, _port)
            {
                EnableSsl = _enableSsl,
                UseDefaultCredentials = false,
                DeliveryMethod = SmtpDeliveryMethod.Network
            };
            if (!string.IsNullOrWhiteSpace(_userName))
            {
                smtp.Credentials = new NetworkCredential(_userName, _password);
            }

            await smtp.SendMailAsync(message, cancellationToken);
        }
        catch (Exception exception) when (exception is SmtpException or InvalidOperationException or FormatException or IOException)
        {
            // SMTP exceptions can contain recipients or the message. Never log their details.
            _logger.LogWarning("Password reset email could not be sent.");
        }
    }
}
