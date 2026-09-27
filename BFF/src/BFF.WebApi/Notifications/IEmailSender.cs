namespace BFF.WebApi.Notifications;

public interface IEmailSender
{
    Task SendAsync(string to, string subject, string body, bool isHtml, CancellationToken ct);
}
