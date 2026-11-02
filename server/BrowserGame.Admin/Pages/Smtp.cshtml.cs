using System.ComponentModel.DataAnnotations;
using BrowserGame.Admin.Auth;
using BrowserGame.Admin.Resources;
using BrowserGame.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Localization;

namespace BrowserGame.Admin.Pages;

/// <summary>
/// SMTP-Einstellungen pflegen und testen (004 AK-7–AK-10, AK-12). Die Testmail nutzt die Werte im Formular, ohne zu
/// speichern – so lässt sich ausprobieren, bevor etwas live geht; die Eingaben bleiben dabei erhalten.
/// </summary>
public sealed partial class SmtpModel(
    SmtpSettingsStore store, AuditLog audit, UserManager<AdminUser> users, ILogger<SmtpModel> logger,
    IStringLocalizer<AdminTexts> t) : PageModel
{
    /// <summary>Ports zum Abholen von Mails (IMAP/POP3) – zum Versenden ungeeignet.</summary>
    private static readonly Dictionary<int, string> RetrievalPorts = new() { [993] = "IMAP", [143] = "IMAP", [995] = "POP3", [110] = "POP3" };

    public sealed class SmtpInput
    {
        [Required(ErrorMessage = "Validation.HostRequired")]
        public string Host { get; set; } = "";

        [Range(1, 65535, ErrorMessage = "Validation.PortRange")]
        public int Port { get; set; } = 465;

        public SmtpSecurity Security { get; set; } = SmtpSecurity.SslOnConnect;

        public string? Username { get; set; }

        /// <summary>Leer = unverändert (AK-8).</summary>
        public string? Password { get; set; }

        [Required(ErrorMessage = "Validation.FromAddressRequired")]
        [EmailAddress(ErrorMessage = "Validation.FromAddressInvalid")]
        public string FromAddress { get; set; } = "";

        public string? FromName { get; set; }
    }

    [BindProperty]
    public SmtpInput Input { get; set; } = new();

    [BindProperty]
    public string? TestRecipient { get; set; }

    public bool Configured { get; private set; }

    public bool PasswordSet { get; private set; }

    public string? Message { get; private set; }

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

        TestRecipient = (await users.GetUserAsync(User))?.Email;
        await LoadStatusAsync();
    }

    public async Task<IActionResult> OnPostSaveAsync()
    {
        if (!Validate())
        {
            return await PageAsync();
        }

        var changed = await store.SaveAsync(ToSettings(Input.Password));
        if (changed.Count > 0)
        {
            await audit.WriteAsync(User.Identity!.Name!, t["AuditEntry.SmtpChanged"], t["AuditEntry.SmtpChangedFields", string.Join(", ", changed)]);
        }

        Message = changed.Count > 0 ? t["Smtp.Saved"] : t["Smtp.NoChanges"];
        return await PageAsync();
    }

    /// <summary>Testmail mit den Werten im Formular (nicht gespeichert); leeres Passwort = gespeichertes Passwort.</summary>
    public async Task<IActionResult> OnPostTestAsync()
    {
        if (string.IsNullOrWhiteSpace(TestRecipient) || !new EmailAddressAttribute().IsValid(TestRecipient))
        {
            ModelState.AddModelError(nameof(TestRecipient), t["Smtp.Error.RecipientInvalid"]);
        }

        if (!Validate())
        {
            return await PageAsync();
        }

        var password = string.IsNullOrEmpty(Input.Password) ? (await store.GetAsync())?.Password : Input.Password;
        var result = await SmtpMailer.SendAsync(ToSettings(password), TestRecipient!, t["Mail.Test.Subject"],
            t["Mail.Test.Body"], cancellationToken: HttpContext.RequestAborted);

        if (result.Status == MailStatus.Sent)
        {
            Message = t["Smtp.TestSent", TestRecipient!];
        }
        else
        {
            ModelState.AddModelError("", result.Error ?? t["Smtp.TestFailed"].Value);
            LogTestFailed(Input.Host, Input.Port, Input.Security, result.Detail);
        }

        await audit.WriteAsync(User.Identity!.Name!, t["AuditEntry.TestMailSent"],
            result.Status == MailStatus.Sent ? t["AuditEntry.To", TestRecipient!] : t["AuditEntry.ToFailed", TestRecipient!, result.Error ?? ""]);
        return await PageAsync();
    }

    /// <summary>Formular prüfen, inklusive typischer Verwechslungen (Port im Servernamen, Abruf-Ports).</summary>
    private bool Validate()
    {
        Input.Host = Input.Host.Trim();
        if (Input.Host.Contains(':', StringComparison.Ordinal) || Input.Host.Contains('/', StringComparison.Ordinal) || Input.Host.Contains(' ', StringComparison.Ordinal))
        {
            ModelState.AddModelError("Input.Host", t["Smtp.Error.HostWithPort"]);
        }

        if (RetrievalPorts.TryGetValue(Input.Port, out var protocol))
        {
            ModelState.AddModelError("Input.Port", t["Smtp.Error.RetrievalPort", Input.Port, protocol]);
        }

        return ModelState.IsValid;
    }

    private SmtpSettings ToSettings(string? password) => new(
        Input.Host, Input.Port, Input.Security, string.IsNullOrWhiteSpace(Input.Username) ? null : Input.Username.Trim(),
        password, Input.FromAddress.Trim(), string.IsNullOrWhiteSpace(Input.FromName) ? null : Input.FromName.Trim());

    /// <summary>Seite mit den Eingaben des Nutzers neu zeigen (nicht aus der DB überschreiben); Passwort nie zurückgeben.</summary>
    private async Task<IActionResult> PageAsync()
    {
        Input.Password = null;
        ModelState.Remove("Input.Password");
        await LoadStatusAsync();
        return Page();
    }

    private async Task LoadStatusAsync()
    {
        var current = await store.GetAsync();
        Configured = current is not null;
        PasswordSet = current?.HasPassword == true;
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Testmail über {Host}:{Port} ({Security}) fehlgeschlagen: {Detail}")]
    private partial void LogTestFailed(string host, int port, SmtpSecurity security, string? detail);
}
