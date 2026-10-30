using System.Text.RegularExpressions;

namespace BrowserGame.Server.Tests;

/// <summary>006 AK-10: Manage und Delete folgen der Sprache des Accounts.</summary>
public sealed class ManagePagesLanguageTests(GameServerFactory factory) : IClassFixture<GameServerFactory>
{
    private static readonly Regex NonEnglish = new("[äöüßÄÖÜ]");

    private const string NewPassword = "ein-ganz-neues-passwort";

    private static void AssertEnglish(string html, params string[] parts)
    {
        var text = HtmlTexts.Visible(html);
        Assert.All(parts, p => Assert.Contains(p, text, StringComparison.Ordinal));
        Assert.DoesNotMatch(NonEnglish, text);
    }

    [Fact] // AK-10
    public async Task Pages_are_english_for_english_account()
    {
        var (client, _) = await AccountTestFlows.SignInAsync(factory, "en");
        using var _ = client;

        var (_, manage) = await client.GetAsync("/Account/Manage");
        var (_, delete) = await client.GetAsync("/Account/Delete");

        Assert.Contains("<html lang=\"en\"", manage, StringComparison.Ordinal);
        Assert.Contains("<html lang=\"en\"", delete, StringComparison.Ordinal);
        AssertEnglish(manage, "My account", "Your passkeys");
        AssertEnglish(delete, "Delete account permanently");
    }

    [Fact] // AK-10
    public async Task Pages_are_german_for_german_account()
    {
        var (client, _) = await AccountTestFlows.SignInAsync(factory, "de");
        using var _ = client;

        var (_, manage) = await client.GetAsync("/Account/Manage");
        var (_, delete) = await client.GetAsync("/Account/Delete");

        Assert.Contains("<html lang=\"de\"", manage, StringComparison.Ordinal);
        Assert.Contains("Mein Account", HtmlTexts.Visible(manage), StringComparison.Ordinal);
        Assert.Contains("Deine Passkeys", HtmlTexts.Visible(manage), StringComparison.Ordinal);
        Assert.Contains("Account endgültig löschen", HtmlTexts.Visible(delete), StringComparison.Ordinal);
    }

    [Fact] // AK-10
    public async Task Manage_messages_are_english_for_english_account()
    {
        var (client, account) = await AccountTestFlows.SignInAsync(factory, "en");
        using var _ = client;
        var first = await AccountTestFlows.AddStoredPasskeyAsync(factory, account.Email, new DateTimeOffset(2026, 1, 2, 12, 0, 0, TimeSpan.Zero));
        var second = await AccountTestFlows.AddStoredPasskeyAsync(factory, account.Email, new DateTimeOffset(2026, 1, 5, 12, 0, 0, TimeSpan.Zero));

        var (_, list) = await client.GetAsync("/Account/Manage");
        AssertEnglish(list, "Passkey 1, added 2026-01-02", "Passkey 2, added 2026-01-05");

        await AccountTestFlows.RemovePasswordDirectlyAsync(factory, account.Email);
        var (_, removed) = await AccountTestFlows.RemovePasskeyAsync(client, AccountTestFlows.PasskeyId(first));
        AssertEnglish(removed, "Passkey removed.", "You can't remove your only way to sign in.", "You don't have a password yet");

        var (_, last) = await AccountTestFlows.RemovePasskeyAsync(client, AccountTestFlows.PasskeyId(second));
        AssertEnglish(last, "This is your only way to sign in. Add a password or another passkey first.");

        var (_, set) = await AccountTestFlows.SetPasswordAsync(client, NewPassword);
        AssertEnglish(set, "Password set.", "You can also sign in with your password.");
    }

    [Fact] // AK-10
    public async Task Delete_messages_are_english_for_english_account()
    {
        var (client, _) = await AccountTestFlows.SignInAsync(factory, "en");
        using var _ = client;

        var (_, wrong) = await client.PostAsync("/Account/Delete", "/Account/Delete",
            new Dictionary<string, string> { ["Password"] = "falsches-passwort" });
        AssertEnglish(wrong, "Wrong password. After several failed attempts");

        var (_, done) = await client.PostAsync("/Account/Delete", "/Account/Delete",
            new Dictionary<string, string> { ["Password"] = AccountTestFlows.Password });
        AssertEnglish(done, "Your account has been deleted. Your player name is available again.", "Back to the start page");
    }
}
