using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using BrowserGame.Data;

namespace BrowserGame.Server.Tests;

/// <summary>Zertifikatsfehler beim Mailserver sollen den richtigen Servernamen verraten (0.2.2: mail.endofweb.xyz vs. Netcup-Zertifikat).</summary>
public class CertificateNameTests
{
    [Fact]
    public void Names_come_from_subject_alternative_names()
    {
        using var key = RSA.Create(2048);
        var request = new CertificateRequest("CN=mx2ea8.netcup.net", key, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        var san = new SubjectAlternativeNameBuilder();
        san.AddDnsName("mx2ea8.netcup.net");
        san.AddDnsName("*.netcup.net");
        request.CertificateExtensions.Add(san.Build());
        using var cert = request.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(1));

        Assert.Equal(["mx2ea8.netcup.net", "*.netcup.net"], SmtpMailer.CertificateNames(cert));
    }

    [Theory]
    [InlineData("mx2ea8.netcup.net", "mx2ea8.netcup.net", true)]
    [InlineData("MX2EA8.netcup.net", "mx2ea8.netcup.net", true)]
    [InlineData("*.netcup.net", "mx2ea8.netcup.net", true)]
    [InlineData("*.netcup.net", "a.b.netcup.net", false)]
    [InlineData("mx2ea8.netcup.net", "mail.endofweb.xyz", false)]
    [InlineData("*.endofweb.xyz", "endofweb.xyz", false)]
    public void Host_matching_handles_wildcards(string certificateName, string host, bool expected)
    {
        Assert.Equal(expected, SmtpMailer.MatchesHost(certificateName, host));
    }
}
