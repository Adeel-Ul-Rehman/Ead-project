using System.Text;

namespace attendence.Services.Helpers;

public static class PasswordGenerator
{
    private static readonly Random _random = new Random();

    // Character sets for password generation
    private const string Uppercase = "ABCDEFGHIJKLMNOPQRSTUVWXYZ";
    private const string Lowercase = "abcdefghijklmnopqrstuvwxyz";
    private const string Digits = "0123456789";
    private const string SpecialChars = "@#$!%*?&";

    /// <summary>
    /// Generate a secure random password that meets requirements:
    /// - Minimum 8 characters
    /// - At least 1 uppercase letter
    /// - At least 1 lowercase letter
    /// - At least 1 number
    /// - At least 1 special character
    /// </summary>
    public static string GeneratePassword(int length = 10)
    {
        if (length < 8)
            length = 8;

        var password = new StringBuilder();

        // Ensure at least one character from each required set
        password.Append(Uppercase[_random.Next(Uppercase.Length)]);
        password.Append(Lowercase[_random.Next(Lowercase.Length)]);
        password.Append(Digits[_random.Next(Digits.Length)]);
        password.Append(SpecialChars[_random.Next(SpecialChars.Length)]);

        // Fill the rest randomly from all character sets
        string allChars = Uppercase + Lowercase + Digits + SpecialChars;
        for (int i = 4; i < length; i++)
        {
            password.Append(allChars[_random.Next(allChars.Length)]);
        }

        // Shuffle the password to avoid predictable pattern
        return Shuffle(password.ToString());
    }

    /// <summary>
    /// Generate multiple unique passwords
    /// </summary>
    public static List<string> GenerateMultiplePasswords(int count, int length = 10)
    {
        var passwords = new HashSet<string>();
        
        while (passwords.Count < count)
        {
            passwords.Add(GeneratePassword(length));
        }

        return passwords.ToList();
    }

    /// <summary>
    /// Shuffle string characters randomly
    /// </summary>
    private static string Shuffle(string input)
    {
        var array = input.ToCharArray();
        int n = array.Length;
        
        for (int i = n - 1; i > 0; i--)
        {
            int j = _random.Next(i + 1);
            (array[i], array[j]) = (array[j], array[i]); // Swap
        }

        return new string(array);
    }

    /// <summary>
    /// Validate if password meets requirements
    /// </summary>
    public static bool IsValidPassword(string password)
    {
        if (string.IsNullOrEmpty(password) || password.Length < 8)
            return false;

        bool hasUppercase = password.Any(char.IsUpper);
        bool hasLowercase = password.Any(char.IsLower);
        bool hasDigit = password.Any(char.IsDigit);
        bool hasSpecialChar = password.Any(c => SpecialChars.Contains(c));

        return hasUppercase && hasLowercase && hasDigit && hasSpecialChar;
    }
}
