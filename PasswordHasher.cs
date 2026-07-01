using System.Security.Cryptography;
using System.Text;

namespace ActionPlanApi.Services;

public static class PasswordHasher
{
    // مطابق با فلاتر: SHA256 ساده روی رشته‌ی رمز
    public static string Hash(string password)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(password));
        var sb = new StringBuilder();
        foreach (var x in bytes) sb.Append(x.ToString("x2"));
        return sb.ToString();
    }

    public static bool Verify(string password, string hash)
        => Hash(password) == hash;
}
