using System.ComponentModel.DataAnnotations;
using BrowserGame.Admin.Auth;
using BrowserGame.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace BrowserGame.Admin.Pages;

/// <summary>SMTP-Einstellungen pflegen und testen (004 AK-7–AK-10, AK-12).</summary>
public sealed class SmtpModel(SmtpSettingsStore store, SmtpMailer mailer, AuditLog audit) : PageModel
{
    public sealed class SmtpInput
    {
        [Required(ErrorMessage = "Bitte Server angeben.")]
        public string Host { get; set; } = "";

        [Range(1, 65535, ErrorMessage = "Port muss zwischen 1 und 65535 liegen.")]
        public int Port { get; set; } = 465;

        public SmtpSecurity Security { get; set; } = SmtpSecurity.Auto;

        public string? Username { get; set; }

        /// <summary>Leer = unverändert (AK-8).</summary>
        public string? Password { get; set; }

        [Required(ErrorMessage = "Bitte Absender-Adresse angeben.")]
        [EmailAddress(ErrorMessage = "Keine gültige Absender-Adresse.")]
        public string FromAddress { get; set; } = "";

        public string? FromName { get; set; }
    }

    [BindProperty]
    public SmtpInput Input { get; set; } = new();

    [BindProperty]
    [EmailAddress(ErrorMessage = "Keine gültige Empfänger-Adresse.")]
    public string? TestRecipient { get; set; }

    public bool Configured { get; private set; }

    public bool PasswordSet { get; private set; }

    public string? Message { get; private set; }

    public string? TestError { get; private set; }

    public async Task OnGetAsync()
    {
        if (await store.GetAsync() is { } current)
        {
            Input = new SmtpInput
            {
                Host = current.Host,
                Port = current.Port,
                Security = current.Security,
                Username = current.Username,
                FromAddress = current.FromAddress,
                FromName = current.FromName,
            };
        }

        await LoadStatusAsync();
    }

    public async Task<IActionResult> OnPostSaveAsync()
    {
        ModelState.Remove(nameof(TestRecipient));
        if (!ModelState.IsValid)
        {
            await LoadStatusAsync();
            return Page();
        }

        var changed = await store.SaveAsync(new SmtpSettings(
            Input.Host, Input.Port, Input.Security, string.IsNullOrWhiteSpace(Input.Username) ? null : Input.Username,
            Input.Password, Input.FromAddress, string.IsNullOrWhiteSpace(Input.FromName) ? null : Input.FromName));
        if (changed.Count > 0)
        {
            await audit.WriteAsync(User.Identity!.Name!, "SMTP-Einstellungen geändert", $"Felder: {string.Join(", ", changed)}");
        }

        Input.Password = null;
        Message = changed.Count > 0 ? "Gespeichert." : "Keine Änderungen.";
        await LoadStatusAsync();
        return Page();
    }

    public async Task<IActionResult> OnPostTestAsync()
    {
        ModelState.Clear();
        if (string.IsNullOrWhiteSpace(TestRecipient) || !new EmailAddressAttribute().IsValid(TestRecipient))
        {
            TestError = "Bitte eine gültige Empfänger-Adresse angeben.";
        }
        else
        {
            var result = await mailer.SendAsync(TestRecipient, "Testmail vom Browser-Game-Admin",
                "Wenn du diese Mail liest, funktioniert der Mailversand.", cancellationToken: HttpContext.RequestAborted);
            if (result.Status == MailStatus.Sent)
            {
                Message = $"Testmail an {TestRecipient} verschickt.";
            }
            else
            {
                TestError = result.Error;
            }

            await audit.WriteAsync(User.Identity!.Name!, "Testmail gesendet",
                result.Status == MailStatus.Sent ? $"an {TestRecipient}" : $"an {TestRecipient}, fehlgeschlagen: {result.Error}");
        }

        await OnGetAsync();
        return Page();
    }

    private async Task LoadStatusAsync()
    {
        var current = await store.GetAsync();
        Configured = current is not null;
        PasswordSet = current?.HasPassword == true;
    }
}
