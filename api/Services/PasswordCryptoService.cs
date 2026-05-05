using System.Security.Cryptography;
using System.Text;

namespace AutoCo.Api.Services;

public interface IPasswordCryptoService
{
    string  Encrypt(string password);
    string? TryDecrypt(string? encrypted);
}

public class PasswordCryptoService(string jwtSecret) : IPasswordCryptoService
{
    private readonly byte[] _key = SHA256.HashData(Encoding.UTF8.GetBytes(jwtSecret));

    public string Encrypt(string password)
    {
        var nonce      = new byte[AesGcm.NonceByteSizes.MaxSize];
        var tag        = new byte[AesGcm.TagByteSizes.MaxSize];
        var plainBytes = Encoding.UTF8.GetBytes(password);
        var cipher     = new byte[plainBytes.Length];

        RandomNumberGenerator.Fill(nonce);
        using var aes = new AesGcm(_key, AesGcm.TagByteSizes.MaxSize);
        aes.Encrypt(nonce, plainBytes, cipher, tag);

        // nonce (12) + tag (16) + cipher
        var result = new byte[nonce.Length + tag.Length + cipher.Length];
        nonce.CopyTo(result, 0);
        tag.CopyTo(result, nonce.Length);
        cipher.CopyTo(result, nonce.Length + tag.Length);
        return Convert.ToBase64String(result);
    }

    public string? TryDecrypt(string? encrypted)
    {
        if (encrypted is null) return null;
        try
        {
            var data   = Convert.FromBase64String(encrypted);
            var nonce  = data[..12];
            var tag    = data[12..28];
            var cipher = data[28..];
            var plain  = new byte[cipher.Length];

            using var aes = new AesGcm(_key, AesGcm.TagByteSizes.MaxSize);
            aes.Decrypt(nonce, cipher, tag, plain);
            return Encoding.UTF8.GetString(plain);
        }
        catch { return null; }
    }
}
