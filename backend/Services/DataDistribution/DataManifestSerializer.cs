using System.Text.Json;
using System.Text.Json.Serialization;
using TapeReplay.Api.Models.DataDistribution;

namespace TapeReplay.Api.Services.DataDistribution;

/// <summary>
/// Reads and writes the static data CDN manifest.json.
/// </summary>
public static class DataManifestSerializer
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        WriteIndented = true
    };

    public static async Task<DataManifest> ReadAsync(Stream stream, CancellationToken cancellationToken = default)
    {
        var manifest = await JsonSerializer.DeserializeAsync<DataManifest>(stream, Options, cancellationToken);
        return manifest ?? throw new InvalidDataException("Manifest JSON was empty.");
    }

    public static async Task<DataManifest> ReadFromFileAsync(string path, CancellationToken cancellationToken = default)
    {
        await using var stream = File.OpenRead(path);
        return await ReadAsync(stream, cancellationToken);
    }

    public static async Task WriteToFileAsync(DataManifest manifest, string path, CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await using var stream = File.Create(path);
        await JsonSerializer.SerializeAsync(stream, manifest, Options, cancellationToken);
    }
}
