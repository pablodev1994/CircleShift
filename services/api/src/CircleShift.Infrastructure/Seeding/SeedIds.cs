using System.Security.Cryptography;
using System.Text;

namespace CircleShift.Infrastructure.Seeding;

/// <summary>
/// Name-based (RFC 9562 version 5) ids for seed rows. The same key always
/// gives the same id, so re-running a seed or seeding a fresh database lines
/// up with links, fixtures and screenshots that reference those ids.
/// </summary>
public static class SeedIds
{
    private static readonly Guid Namespace = new("6f3d2c1a-8b4e-4f5a-9c7d-2e1b0a9f8c6d");

    public static Guid For(string kind, string key)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(kind);
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        var name = Encoding.UTF8.GetBytes($"{kind}:{key}");
        var input = new byte[16 + name.Length];
        Namespace.TryWriteBytes(input, bigEndian: true, out _);
        name.CopyTo(input, 16);

#pragma warning disable CA5350 // SHA-1 is what RFC 9562 specifies for v5 ids; it is not used for security here.
        var hash = SHA1.HashData(input);
#pragma warning restore CA5350

        var bytes = hash.AsSpan(0, 16).ToArray();
        bytes[6] = (byte)((bytes[6] & 0x0F) | 0x50); // version 5
        bytes[8] = (byte)((bytes[8] & 0x3F) | 0x80); // RFC variant
        return new Guid(bytes, bigEndian: true);
    }
}
