using System.Net;
using System.Net.Mail;

namespace BFF.WebApi.Notifications;

/// <summary>Отправляет письма через SMTP_HOST/SMTP_PORT (обычный System.Net.Mail.SmtpClient — контейнер
/// с почтой уже поднят инфраструктурой, отдельный пакет для SMTP тут не нужен). SMTP_USER/SMTP_PASSWORD
/// не заданы — соединение без авторизации (открытый релей внутри docker-сети).</summary>
public sealed class SmtpEmailSender : IEmailSender
{
    private readonly string _host;
    private readonly int _port;
    private readonly string _from;
    private readonly string? _user;
    private readonly string? _password;
    private readonly bool _useSsl;

    public SmtpEmailSender(IConfiguration configuration)
    {
        // SMTP_HOST/SMTP_FROM — в ValidateRequiredConfiguration (ServiceCollectionExtensions), поэтому
        // здесь они уже гарантированно заданы.
        _host = configuration["SMTP_HOST"]!;
        _from = configuration["SMTP_FROM"]!;
        _port = int.TryParse(configuration["SMTP_PORT"], out var port) ? port : 25;
        _user = configuration["SMTP_USER"];
        _password = configuration["SMTP_PASSWORD"];
        _useSsl = bool.TryParse(configuration["SMTP_USE_SSL"], out var useSsl) && useSsl;
    }

    public async Task SendAsync(string to, string subject, string body, bool isHtml, CancellationToken ct)
    {
        using var client = new SmtpClient(_host, _port) { EnableSsl = _useSsl };
        if (!string.IsNullOrEmpty(_user))
        {
            client.Credentials = new NetworkCredential(_user, _password);
        }

        using var message = new MailMessage(_from, to, subject, body) { IsBodyHtml = isHtml };
        await client.SendMailAsync(message, ct);
    }
}
