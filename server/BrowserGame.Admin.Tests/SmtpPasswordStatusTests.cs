using BrowserGame.Server.Tests;
using static BrowserGame.Admin.Tests.SmtpTestHelpers;

namespace BrowserGame.Admin.Tests;

public sealed class SmtpPasswordStatusTests
{
    private const string Save = "/Smtp?handler=Save";

    [Fact] // AK-16
    public async Task Username_without_password_shows_not_stored_and_a_warning()
    {
        await using var factory = new AdminFactory();
        await using var smtp = new FakeSmtpServer();
        var admin = await SignedInAsync(factory);

        var (_, saved) = await admin.Client.PostAsync("/Smtp", Save, Form(smtp, null));
        Assert.Equal("NotStored", PasswordStatus(saved));
        Assert.Equal([ExpectedText.Get("Smtp.PasswordMissing")], Alerts(saved, "alert-warning"));
        Assert.Single(Alerts(saved, "alert-success"));

        var (_, page) = await admin.Client.GetAsync("/Smtp");
        Assert.Equal("NotStored", PasswordStatus(page));
        Assert.Equal([ExpectedText.Get("Smtp.PasswordMissing")], Alerts(page, "alert-warning"));
    }

    [Fact] // AK-16
    public async Task Stored_password_shows_stored_and_no_warning()
    {
        await using var factory = new AdminFactory();
        await using var smtp = new FakeSmtpServer();
        var admin = await SignedInAsync(factory);

        var (_, saved) = await admin.Client.PostAsync("/Smtp", Save, Form(smtp, smtp.Password));

        Assert.Equal("Stored", PasswordStatus(saved));
        Assert.Empty(Alerts(saved, "alert-warning"));
        AdminPagesTests.AssertLabelAboveFieldAndHintBelow(saved, "Input.Password", expectHint: true);
    }

    [Fact] // AK-16
    public async Task Saving_without_password_keeps_and_shows_the_stored_one()
    {
        await using var factory = new AdminFactory();
        await using var smtp = new FakeSmtpServer();
        var admin = await SignedInAsync(factory);
        await admin.Client.PostAsync("/Smtp", Save, Form(smtp, smtp.Password));

        var (_, saved) = await admin.Client.PostAsync("/Smtp", Save, Form(smtp, null));

        Assert.Equal("Stored", PasswordStatus(saved));
        Assert.Empty(Alerts(saved, "alert-warning"));
        Assert.Equal(smtp.Password, (await StoredAsync(factory))!.Password);
    }
}
