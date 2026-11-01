using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace BrowserGame.Server.Tests;

/// <summary>Minimaler SMTP-Server für Tests (ohne TLS): AUTH PLAIN mit festen Zugangsdaten, nimmt Mails entgegen.</summary>
public sealed class FakeSmtpServer : IAsyncDisposable
{
    private readonly TcpListener _listener = new(IPAddress.Loopback, 0);
    private readonly CancellationTokenSource _stop = new();
    private readonly Task _accepting;

    public FakeSmtpServer(string username = "game@example.net", string password = "geheim-123")
    {
        Username = username;
        Password = password;
        _listener.Start();
        _accepting = AcceptAsync();
    }

    public string Username { get; }
    public string Password { get; }
    public int Port => ((IPEndPoint)_listener.LocalEndpoint).Port;
    public ConcurrentQueue<string> Messages { get; } = new();

    private async Task AcceptAsync()
    {
        try
        {
            while (!_stop.IsCancellationRequested)
            {
                var client = await _listener.AcceptTcpClientAsync(_stop.Token);
                _ = HandleAsync(client);
            }
        }
        catch (OperationCanceledException)
        {
        }
    }

    private async Task HandleAsync(TcpClient client)
    {
        using var _ = client;
        var stream = client.GetStream();
        using var reader = new StreamReader(stream, Encoding.ASCII);
        await using var writer = new StreamWriter(stream, Encoding.ASCII) { NewLine = "\r\n", AutoFlush = true };
        await writer.WriteLineAsync("220 fake ESMTP");
        while (await reader.ReadLineAsync() is { } line)
        {
            var command = line.Split(' ', 2)[0].ToUpperInvariant();
            switch (command)
            {
                case "EHLO":
                    await writer.WriteLineAsync("250-fake");
                    await writer.WriteLineAsync("250 AUTH PLAIN");
                    break;
                case "AUTH":
                    var payload = line.Split(' ').ElementAtOrDefault(2);
                    if (payload is null)
                    {
                        await writer.WriteLineAsync("334 ");
                        payload = await reader.ReadLineAsync();
                    }

                    var parts = Encoding.UTF8.GetString(Convert.FromBase64String(payload ?? "")).Split('\0');
                    var ok = parts.Length == 3 && parts[1] == Username && parts[2] == Password;
                    await writer.WriteLineAsync(ok ? "235 2.7.0 ok" : "535 5.7.8 authentication failed");
                    break;
                case "MAIL":
                case "RCPT":
                case "RSET":
                case "NOOP":
                    await writer.WriteLineAsync("250 ok");
                    break;
                case "DATA":
                    await writer.WriteLineAsync("354 go ahead");
                    var data = new StringBuilder();
                    while (await reader.ReadLineAsync() is { } dataLine && dataLine != ".")
                    {
                        data.AppendLine(dataLine);
                    }

                    Messages.Enqueue(data.ToString());
                    await writer.WriteLineAsync("250 queued");
                    break;
                case "QUIT":
                    await writer.WriteLineAsync("221 bye");
                    return;
                default:
                    await writer.WriteLineAsync("502 not implemented");
                    break;
            }
        }
    }

    public async ValueTask DisposeAsync()
    {
        await _stop.CancelAsync();
        _listener.Stop();
        await _accepting;
        _stop.Dispose();
    }
}
