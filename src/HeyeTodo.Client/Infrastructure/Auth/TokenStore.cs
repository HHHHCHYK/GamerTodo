using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace HeyeTodo.Client.Infrastructure.Auth;

public sealed record TokenBundle(
    string AccessToken,
    string RefreshToken,
    DateTimeOffset AccessTokenExpiresAt,
    Guid UserId,
    string Email);

/// <summary>
/// Encrypted at-rest token store.
/// Windows → DPAPI (ProtectedData) with CurrentUser scope.
/// macOS / Linux → raw bytes inside per-user data directory (mode 0600).
/// A Keychain/Secret-Service backend can be swapped in later.
/// </summary>
public sealed class TokenStore
{
    private readonly string _path;

    public TokenStore(string path) => _path = path;

    public void Save(TokenBundle? bundle)
    {
        if (bundle is null)
        {
            if (File.Exists(_path)) File.Delete(_path);
            return;
        }

        var json = JsonSerializer.SerializeToUtf8Bytes(bundle);
        var encrypted = Encrypt(json);
        File.WriteAllBytes(_path, encrypted);

        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            try { File.SetUnixFileMode(_path, UnixFileMode.UserRead | UnixFileMode.UserWrite); }
            catch { /* older fs may not support */ }
        }
    }

    public TokenBundle? Load()
    {
        if (!File.Exists(_path)) return null;
        try
        {
            var bytes = File.ReadAllBytes(_path);
            var plain = Decrypt(bytes);
            return JsonSerializer.Deserialize<TokenBundle>(plain);
        }
        catch
        {
            return null;
        }
    }

    private static byte[] Encrypt(byte[] plain)
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return ProtectedData.Protect(plain, optionalEntropy: null, scope: DataProtectionScope.CurrentUser);
        }
        return plain;
    }

    private static byte[] Decrypt(byte[] cipher)
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return ProtectedData.Unprotect(cipher, optionalEntropy: null, scope: DataProtectionScope.CurrentUser);
        }
        return cipher;
    }
}
