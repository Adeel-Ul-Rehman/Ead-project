using attendence.Services.Services;
using System.Security.Cryptography;
using System.Text;
using System;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace attendence.Services.Services;

public interface IPasswordHasher
{
    string HashPassword(string password);
    bool VerifyPassword(string password, string hashedPassword);
}

public class PasswordHasher : IPasswordHasher
{
    // PBKDF2 parameters
    private const int SaltSize = 32; // bytes
    private const int KeySize = 32; // bytes
    private const int Iterations = 100_000;
    private const string Prefix = "PBKDF2";

    public string HashPassword(string password)
    {
        if (password is null) throw new ArgumentNullException(nameof(password));

        using var rng = RandomNumberGenerator.Create();
        var salt = new byte[SaltSize];
        rng.GetBytes(salt);

        using var pbkdf2 = new Rfc2898DeriveBytes(password, salt, Iterations, HashAlgorithmName.SHA256);
        var key = pbkdf2.GetBytes(KeySize);

        // Format: PBKDF2$iterations$base64salt$base64key
        return $"{Prefix}${Iterations}${Convert.ToBase64String(salt)}${Convert.ToBase64String(key)}";
    }

    public bool VerifyPassword(string password, string hashedPassword)
    {
        if (password is null) throw new ArgumentNullException(nameof(password));
        if (string.IsNullOrEmpty(hashedPassword)) return false;

        try
        {
            var parts = hashedPassword.Split('$');
            if (parts.Length == 4 && parts[0] == Prefix)
            {
                var iterations = int.Parse(parts[1]);
                var salt = Convert.FromBase64String(parts[2]);
                var expectedKey = Convert.FromBase64String(parts[3]);

                using var pbkdf2 = new Rfc2898DeriveBytes(password, salt, iterations, HashAlgorithmName.SHA256);
                var actualKey = pbkdf2.GetBytes(expectedKey.Length);

                return CryptographicOperations.FixedTimeEquals(actualKey, expectedKey);
            }

            // Not PBKDF2 format => this method doesn't verify legacy formats here.
            return false;
        }
        catch
        {
            return false;
        }
    }

    // Helper to compute SHA256 (for legacy migration)
    public static string ComputeSha256Base64(string input)
    {
        using var sha256 = SHA256.Create();
        var hash = sha256.ComputeHash(Encoding.UTF8.GetBytes(input));
        return Convert.ToBase64String(hash);
    }
}