using System.Security.Cryptography;
using System.Text;

namespace ChunkingLibrary.Helpers;

public static class HashHelper
{
    public static string GenerateSha256Hash(string text)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(text));
        var hexHash = Convert.ToHexString(bytes).ToLowerInvariant();
        return $"sha256-{hexHash}";
    }
}
