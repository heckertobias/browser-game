namespace BrowserGame.Server.Tests;

/// <summary>Kartendatei in einem eigenen Temp-Ordner; wird beim Entsorgen gelöscht.</summary>
internal sealed class TempMapFile : IDisposable
{
    private readonly DirectoryInfo _directory = Directory.CreateTempSubdirectory("browsergame-map");

    public TempMapFile(string json)
    {
        Path = System.IO.Path.Combine(_directory.FullName, "map.json");
        File.WriteAllText(Path, json);
    }

    public string Path { get; }

    /// <summary>Pfad im selben Ordner, an dem keine Datei liegt.</summary>
    public string MissingPath => System.IO.Path.Combine(_directory.FullName, "fehlt.json");

    public void Dispose() => _directory.Delete(recursive: true);
}
