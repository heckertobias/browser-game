using BrowserGame.Server.Tests;
using static BrowserGame.Admin.Tests.SmtpTestHelpers;

namespace BrowserGame.Admin.Tests;

public sealed class SmtpSavedMessageTests
{
    [Fact] // 005 AK-8
    public async Task Saving_shows_saved_as_the_only_message()
    {
        await using var factory = new AdminFactory();
        await using var smtp = new FakeSmtpServer();
        var admin = await SignedInAsync(factory);

        var (_, saved) = await admin.Client.PostAsync("/Smtp", "/Smtp?handler=Save", Form(smtp, smtp.Password));

        Assert.Equal([ExpectedText.Get("Smtp.Saved")], Alerts(saved, "alert-success"));
        Assert.Empty(Alerts(saved, "alert-warning"));
        Assert.Empty(Alerts(saved, "alert-danger"));
    }
}
