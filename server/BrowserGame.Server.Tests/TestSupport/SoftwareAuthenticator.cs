using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.WebUtilities;

namespace BrowserGame.Server.Tests;

/// <summary>Software-Passkey für Tests: erzeugt Registrierungs- und Anmelde-Antworten wie ein Browser (Attestation "none", ES256).</summary>
public sealed class SoftwareAuthenticator : IDisposable
{
    public const string Origin = "http://localhost";

    private static readonly string[] Transports = ["internal"];

    private readonly ECDsa _key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
    private string _userHandle = "";

    public byte[] CredentialId { get; } = RandomNumberGenerator.GetBytes(16);

    public string Id => WebEncoders.Base64UrlEncode(CredentialId);

    /// <summary>Antwort auf die Registrierungsoptionen (<c>navigator.credentials.create</c>).</summary>
    public string Create(string optionsJson)
    {
        using var options = JsonDocument.Parse(optionsJson);
        var root = options.RootElement;
        var challenge = root.GetProperty("challenge").GetString()!;
        var rpId = root.GetProperty("rp").GetProperty("id").GetString()!;
        _userHandle = root.GetProperty("user").GetProperty("id").GetString()!;

        var q = _key.ExportParameters(false).Q;
        byte[] cose = [0xA5, 0x01, 0x02, 0x03, 0x26, 0x20, 0x01, 0x21, 0x58, 0x20, .. q.X!, 0x22, 0x58, 0x20, .. q.Y!];
        byte[] authData =
        [
            .. SHA256.HashData(Encoding.UTF8.GetBytes(rpId)),
            0x45, 0, 0, 0, 0,
            .. new byte[16],
            (byte)(CredentialId.Length >> 8), (byte)CredentialId.Length,
            .. CredentialId,
            .. cose,
        ];
        byte[] attestation =
        [
            0xA3,
            0x63, .. "fmt"u8, 0x64, .. "none"u8,
            0x67, .. "attStmt"u8, 0xA0,
            0x68, .. "authData"u8, 0x58, (byte)authData.Length, .. authData,
        ];

        return JsonSerializer.Serialize(new
        {
            id = Id,
            rawId = Id,
            type = "public-key",
            authenticatorAttachment = "platform",
            response = new
            {
                clientDataJSON = Encode(ClientData("webauthn.create", challenge)),
                attestationObject = Encode(attestation),
                transports = Transports,
            },
            clientExtensionResults = new { },
        });
    }

    /// <summary>Antwort auf die Anmeldeoptionen (<c>navigator.credentials.get</c>).</summary>
    public string Get(string optionsJson)
    {
        using var options = JsonDocument.Parse(optionsJson);
        var root = options.RootElement;
        var challenge = root.GetProperty("challenge").GetString()!;
        var rpId = root.GetProperty("rpId").GetString()!;

        byte[] authData = [.. SHA256.HashData(Encoding.UTF8.GetBytes(rpId)), 0x05, 0, 0, 0, 0];
        var clientData = ClientData("webauthn.get", challenge);
        var signature = _key.SignData([.. authData, .. SHA256.HashData(clientData)], HashAlgorithmName.SHA256, DSASignatureFormat.Rfc3279DerSequence);

        return JsonSerializer.Serialize(new
        {
            id = Id,
            rawId = Id,
            type = "public-key",
            response = new
            {
                clientDataJSON = Encode(clientData),
                authenticatorData = Encode(authData),
                signature = Encode(signature),
                userHandle = _userHandle,
            },
            clientExtensionResults = new { },
        });
    }

    public void Dispose() => _key.Dispose();

    private static byte[] ClientData(string type, string challenge) =>
        JsonSerializer.SerializeToUtf8Bytes(new { type, challenge, origin = Origin, crossOrigin = false });

    private static string Encode(byte[] bytes) => WebEncoders.Base64UrlEncode(bytes);
}
