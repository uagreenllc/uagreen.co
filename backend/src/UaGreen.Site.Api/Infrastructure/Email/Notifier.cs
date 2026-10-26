using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Options;
using MimeKit;

namespace UaGreen.Site.Api.Infrastructure.Email;

public sealed class SmtpOptions
{
    public string Host { get; set; } = "";
    public int Port { get; set; } = 587;
    public bool UseStartTls { get; set; } = true;
    public string User { get; set; } = "";
    public string Password { get; set; } = "";
    public string From { get; set; } = "";
    public string FromName { get; set; } = "UA GREEN";
}

public sealed class NotificationOptions
{
    /// Where new orders, applications and contact requests are mailed.
    public string OrderEmail { get; set; } = "";
}

/// Plain-text emails to the managers' inbox. Best-effort: a mail failure is logged,
/// never surfaced to the customer who just placed an order.
public sealed class Notifier(IOptions<SmtpOptions> smtp, IOptions<NotificationOptions> notify, ILogger<Notifier> log)
{
    private readonly SmtpOptions _smtp = smtp.Value;
    private readonly string _to = notify.Value.OrderEmail;

    public async Task NotifyManagersAsync(string subject, string text, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(_to) || string.IsNullOrWhiteSpace(_smtp.Host))
        {
            log.LogInformation("Notification (no SMTP configured): {Subject}\n{Text}", subject, text);
            return;
        }
        try
        {
            var msg = new MimeMessage();
            msg.From.Add(new MailboxAddress(_smtp.FromName, _smtp.From));
            msg.To.Add(MailboxAddress.Parse(_to));
            msg.Subject = subject;
            msg.Body = new TextPart("plain") { Text = text };

            using var client = new SmtpClient();
            await client.ConnectAsync(_smtp.Host, _smtp.Port, _smtp.UseStartTls ? SecureSocketOptions.StartTls : SecureSocketOptions.Auto, ct);
            if (!string.IsNullOrEmpty(_smtp.User))
                await client.AuthenticateAsync(_smtp.User, _smtp.Password, ct);
            await client.SendAsync(msg, ct);
            await client.DisconnectAsync(true, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            log.LogError(ex, "Failed to send notification email: {Subject}", subject);
        }
    }
}
