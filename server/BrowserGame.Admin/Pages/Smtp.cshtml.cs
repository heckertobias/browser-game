using System.ComponentModel.DataAnnotations;
using System.Security.Cryptography;
using BrowserGame.Admin.Auth;
using BrowserGame.Admin.Resources;
using BrowserGame.Data;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Localization;

namespace BrowserGame.Admin.Pages;

/// <summary>Gespeicherter Stand des SMTP-Passworts, wie ihn die Seite zeigt (005 AK-16, AK-18).</summary>
public enum SmtpPasswordStatus
{
    NotStored,
    Stored,

    /// <summary>Kein Benutzername gespeichert: Mails gehen ohne Anmeldung, ein Passwort wird nicht gespeichert.</summary>
    NoLogin,
}

/// <summary>
/// SMTP-Einstellungen pflegen und testen (004 AK-7–AK-10, AK-12; 005 AK-15–AK-18). Die Formular-Testmail nutzt die Werte
/// im Formular, ohne zu speichern; nach Erfolg trägt die Seite das getestete Passwort geschützt und zeitbegrenzt bis zum
/// Speichern mit. Die Testmail mit gespeichertem Stand geht über <see cref="SmtpMailer"/> wie die Mails des Spiels.
/// </summary>
public sealed partial class SmtpModel(
    SmtpSettingsStore store, SmtpMailer mailer, AuditLog audit, UserManager<AdminUser> users,
    IDataProtectionProvider dataProtection, ILogger<SmtpModel> logger, IStringLocalizer<AdminTexts> t) : PageModel
{
    internal const string TestedPasswordPurpose = "Admin.Smtp.TestedPassword";
    internal static readonly TimeSpan TestedPasswordLifetime = TimeSpan.FromMinutes(30);

    /// <summary>Ports zum Abholen von Mails (IMAP/POP3) – zum Versenden ungeeignet.</summary>
    private static readonly Dictionary<int, string> RetrievalPorts = new() { [993] = "IMAP", [143] = "IMAP", [995] = "POP3", [110] = "POP3" };

    private readonly ITimeLimitedDataProtector _testedPassword =
        dataProtection.CreateProtector(TestedPasswordPurpose).ToTimeLimitedDataProtector();

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

    /// <summary>Zuletzt erfolgreich getestetes Passwort, geschützt und 30 Minuten gültig (005 AK-15); nie Klartext.</summary>
    [BindProperty]
    public string? TestedPassword { get; set; }

    public bool Configured { get; private set; }

    public SmtpPasswordStatus PasswordStatus { get; private set; }

    /// <summary>Benutzername gespeichert, aber kein Passwort: die Anmeldung am Mailserver wird scheitern (005 AK-16).</summary>
    public bool PasswordMissing { get; private set; }

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
        if (!Validate() || !TryGetNewPassword(out var password))
        {
            return await PageAsync();
        }

        var changed = await store.SaveAsync(ToSettings(password));
        TestedPassword = null;
        if (changed.Count > 0)
        {
            await audit.WriteAsync(User.Identity!.Name!, t["AuditEntry.SmtpChanged"], t["AuditEntry.SmtpChangedFields", string.Join(", ", changed)]);
        }

        Message = changed.Count > 0 ? t["Smtp.Saved"] : t["Smtp.NoChanges"];
        return await PageAsync();
    }

    /// <summary>
    /// Testmail mit den Werten im Formular (nicht gespeichert). Passwort: eingetippt, sonst getestet, sonst gespeichert.
    /// Nach Erfolg mit eingetipptem oder getestetem Passwort gibt die Seite es als <see cref="TestedPassword"/> zurück.
    /// </summary>
    public async Task<IActionResult> OnPostTestAsync()
    {
        ValidateRecipient();
        if (!Validate() || !TryGetNewPassword(out var newPassword))
        {
            return await PageAsync();
        }

        var password = newPassword ?? (await store.GetAsync())?.Password;
        var result = await SmtpMailer.SendAsync(ToSettings(password), TestRecipient!, t["Mail.Test.Subject"],
            t["Mail.Test.Body"], cancellationToken: HttpContext.RequestAborted);

        if (result.Status == MailStatus.Sent)
        {
            Message = t["Smtp.TestSent", TestRecipient!];
            if (newPassword is not null)
            {
                TestedPassword = _testedPassword.Protect(newPassword, TestedPasswordLifetime);
            }
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

    /// <summary>Testmail genau mit dem gespeicherten Stand, über denselben Weg wie das Spiel (005 AK-17). Formularwerte zählen nicht.</summary>
    public async Task<IActionResult> OnPostTestSavedAsync()
    {
        foreach (var key in ModelState.Keys.Where(k => k.StartsWith("Input.", StringComparison.Ordinal)).ToList())
        {
            ModelState.Remove(key);
        }

        ValidateRecipient();
        if (!ModelState.IsValid)
        {
            return await PageAsync();
        }

        var result = await mailer.SendAsync(TestRecipient!, t["Mail.Test.Subject"], t["Mail.Test.Body"],
            cancellationToken: HttpContext.RequestAborted);

        if (result.Status == MailStatus.Sent)
        {
            Message = t["Smtp.TestSavedSent", TestRecipient!];
        }
        else
        {
            ModelState.AddModelError("", result.Error ?? t["Smtp.TestFailed"].Value);
            LogTestSavedFailed(result.Detail);
        }

        await audit.WriteAsync(User.Identity!.Name!, t["AuditEntry.TestMailSavedSent"],
            result.Status == MailStatus.Sent ? t["AuditEntry.To", TestRecipient!] : t["AuditEntry.ToFailed", TestRecipient!, result.Error ?? ""]);
        return await PageAsync();
    }

    private void ValidateRecipient()
    {
        if (string.IsNullOrWhiteSpace(TestRecipient) || !new EmailAddressAttribute().IsValid(TestRecipient))
        {
            ModelState.AddModelError(nameof(TestRecipient), t["Smtp.Error.RecipientInvalid"]);
        }
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

    /// <summary>
    /// Neues Passwort: eingetippt vor getestet (005 AK-15); <c>null</c> = keins, das gespeicherte gilt. Ohne Benutzername
    /// zählt weder Eingabe noch getestetes Passwort (keine Anmeldung, 005 AK-18).
    /// <c>false</c>, wenn das getestete Passwort abgelaufen oder ungültig ist – dann Fehler am Passwortfeld.
    /// </summary>
    private bool TryGetNewPassword(out string? password)
    {
        password = null;
        if (string.IsNullOrWhiteSpace(Input.Username))
        {
            TestedPassword = null;
            return true;
        }

        if (!string.IsNullOrEmpty(Input.Password))
        {
            password = Input.Password;
            return true;
        }

        if (string.IsNullOrEmpty(TestedPassword))
        {
            return true;
        }

        password = UnprotectTestedPassword(TestedPassword);
        if (password is not null)
        {
            return true;
        }

        TestedPassword = null;
        ModelState.AddModelError("Input.Password", t["Smtp.Error.TestedPasswordInvalid"]);
        return false;
    }

    private string? UnprotectTestedPassword(string token)
    {
        try
        {
            return _testedPassword.Unprotect(token, out _);
        }
        catch (CryptographicException)
        {
            return null;
        }
        catch (FormatException)
        {
            return null;
        }
    }

    private SmtpSettings ToSettings(string? password) => new(
        Input.Host, Input.Port, Input.Security, string.IsNullOrWhiteSpace(Input.Username) ? null : Input.Username.Trim(),
        password, Input.FromAddress.Trim(), string.IsNullOrWhiteSpace(Input.FromName) ? null : Input.FromName.Trim());

    /// <summary>
    /// Seite mit den Eingaben des Nutzers neu zeigen (nicht aus der DB überschreiben); Passwort nie zurückgeben.
    /// Ein Fehler am Passwortfeld bleibt stehen – er entsteht nur bei leerem Feld (ungültiges getestetes Passwort).
    /// </summary>
    private async Task<IActionResult> PageAsync()
    {
        Input.Password = null;
        if (ModelState.GetFieldValidationState("Input.Password") != ModelValidationState.Invalid)
        {
            ModelState.Remove("Input.Password");
        }

        await LoadStatusAsync();
        return Page();
    }

    private async Task LoadStatusAsync()
    {
        var current = await store.GetAsync();
        Configured = current is not null;
        PasswordStatus = current is null ? SmtpPasswordStatus.NotStored
            : string.IsNullOrWhiteSpace(current.Username) ? SmtpPasswordStatus.NoLogin
            : current.HasPassword ? SmtpPasswordStatus.Stored
            : SmtpPasswordStatus.NotStored;
        PasswordMissing = !string.IsNullOrEmpty(current?.Username) && current?.HasPassword != true;
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Testmail über {Host}:{Port} ({Security}) fehlgeschlagen: {Detail}")]
    private partial void LogTestFailed(string host, int port, SmtpSecurity security, string? detail);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Testmail mit gespeicherten Einstellungen fehlgeschlagen: {Detail}")]
    private partial void LogTestSavedFailed(string? detail);
}
