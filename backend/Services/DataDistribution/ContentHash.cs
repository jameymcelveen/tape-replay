using System.Security.Cryptography;

namespace TapeReplay.Api.Services.DataDistribution;

/// <summary>
/// SHA-256 helpers for content-addressed partition files.
/// </summary>
public static class ContentHash
{
    public static async Task<string> ComputeFileSha256HexAsync(string path, CancellationToken cancellationToken = default)
    {
        await using var stream = File.OpenRead(path);
        var hash = await SHA256.HashDataAsync(stream, cancellationToken);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    public static async Task VerifyFileSha256HexAsync(string path, string expectedHex, CancellationToken cancellationToken = default)
    {
        var actual = await ComputeFileSha256HexAsync(path, cancellationToken);
        if (!string.Equals(actual, expectedHex, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException($"SHA256 mismatch for {path}: expected {expectedHex}, got {actual}.");
        }
    }
}
