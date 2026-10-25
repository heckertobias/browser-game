using BrowserGame.Server.Accounts;

namespace BrowserGame.Server.Tests;

/// <summary>Sammelt Mails, statt sie zu verschicken.</summary>
public sealed class CapturingEmailSender : GameEmailSender
{
    private readonly List<(string To, string Subject, string Text)> _mails = [];

    public IReadOnlyList<(string To, string Subject, string Text)> To(string email)
    {
        lock (_mails)
        {
            return _mails.Where(m => string.Equals(m.To, email, StringComparison.OrdinalIgnoreCase)).ToList();
        }
    }

    protected override Task SendAsync(string recipient, string subject, string text, string link, string language)
    {
        lock (_mails)
        {
            _mails.Add((recipient, subject, text));
        }

        return Task.CompletedTask;
    }
}
