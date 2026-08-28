using System;
using System.Security.Cryptography;
using System.Text;

namespace ExpenseFlow.Web.Security
{
    /// <summary>
    /// Salted SHA-256, single round. Typical of its era and NOT acceptable
    /// today - no key stretching, so it is brute-forceable.
    ///
    /// Record this in the phase 3 assessment. The fix is PBKDF2 via
    /// ASP.NET Core Identity's PasswordHasher, with a rehash-on-login
    /// upgrade path so existing users are migrated transparently.
    /// </summary>
    public static class PasswordHasher
    {
        public static string Hash(string password, string salt)
        {
            if (password == null) password = string.Empty;
            if (salt == null) salt = string.Empty;

            using (var sha = SHA256.Create())
            {
                var bytes = Encoding.UTF8.GetBytes(salt + password);
                return Convert.ToBase64String(sha.ComputeHash(bytes));
            }
        }

        public static bool Verify(string password, string salt, string expectedHash)
        {
            var actual = Hash(password, salt);
            return FixedTimeEquals(actual, expectedHash);
        }

        private static bool FixedTimeEquals(string a, string b)
        {
            if (a == null || b == null) return false;
            if (a.Length != b.Length) return false;

            var diff = 0;
            for (var i = 0; i < a.Length; i++) diff |= a[i] ^ b[i];
            return diff == 0;
        }

        public static string NewSalt()
        {
            var buffer = new byte[16];
            using (var rng = RandomNumberGenerator.Create()) rng.GetBytes(buffer);
            return Convert.ToBase64String(buffer).Substring(0, 16);
        }
    }
}
