using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using BrowserGame.Data;

namespace BrowserGame.Server.Tests;

/// <summary>Zertifikatsfehler beim Mailserver sollen den richtigen Servernamen verraten (0.2.2: mail.game.example vs. Zertifikat des Mail-Anbieters).</summary>
public class CertificateNameTests
{
    [Fact]
    public void Names_come_from_subject_alternative_names()
    {
        using var key = RSA.Create(2048);
        var request = new CertificateRequest("CN=mx.mailhost.example", key, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        var san = new SubjectAlternativeNameBuilder();
        san.AddDnsName("mx.mailhost.example");
        san.AddDnsName("*.mailhost.example");
        request.CertificateExtensions.Add(san.Build());
        using var cert = request.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(1));

        Assert.Equal(["mx.mailhost.example", "*.mailhost.example"], SmtpMailer.CertificateNames(cert));
    }

    [Theory]
    [InlineData("mx.mailhost.example", "mx.mailhost.example", true)]
    [InlineData("MX.MAILHOST.example", "mx.mailhost.example", true)]
    [InlineData("*.mailhost.example", "mx.mailhost.example", true)]
    [InlineData("*.mailhost.example", "a.b.mailhost.example", false)]
    [InlineData("mx.mailhost.example", "mail.game.example", false)]
    [InlineData("*.game.example", "game.example", false)]
    public void Host_matching_handles_wildcards(string certificateName, string host, bool expected)
    {
        Assert.Equal(expected, SmtpMailer.MatchesHost(certificateName, host));
    }
}
