using System.DirectoryServices;

namespace ActionPlanApi.Services;

// سرویس احراز هویت با Active Directory (LDAP).
// توجه: System.DirectoryServices فقط روی ویندوز کار می‌کند.
// روی سرور شرکت (ویندوز/IIS) اجرا می‌شود.
public class ActiveDirectoryService
{
    public class AdUserInfo
    {
        public string Username { get; set; } = "";
        public string DisplayName { get; set; } = "";
    }

    // نرمال‌سازی نام کاربری: اگر user@domain بود، فقط user
    public static string NormalizeUsername(string username)
    {
        if (string.IsNullOrEmpty(username)) return "";
        return username.Contains('@') ? username.Split('@')[0] : username;
    }

    // بررسی نام کاربری و رمز با LDAP
    public bool Authenticate(string ldapPath, string username, string password)
    {
        try
        {
            using var entry = new DirectoryEntry(ldapPath, username, password);
            var obj = entry.NativeObject; // اگر معتبر نباشد، استثنا می‌دهد
            return obj != null;
        }
        catch
        {
            return false;
        }
    }

    // گرفتن اطلاعات کاربر (displayName) با کاربر سرویس
    public AdUserInfo GetUserInfo(
        string ldapPath, string serviceUser, string servicePass,
        string targetUsername)
    {
        var info = new AdUserInfo
        {
            Username = targetUsername,
            DisplayName = targetUsername
        };
        try
        {
            using var entry = string.IsNullOrEmpty(serviceUser)
                ? new DirectoryEntry(ldapPath)
                : new DirectoryEntry(ldapPath, serviceUser, servicePass);
            using var searcher = new DirectorySearcher(entry);
            searcher.Filter = $"(sAMAccountName={targetUsername})";
            searcher.PropertiesToLoad.Add("sAMAccountName");
            searcher.PropertiesToLoad.Add("displayName");

            var result = searcher.FindOne();
            if (result != null)
            {
                if (result.Properties["sAMAccountName"].Count > 0)
                    info.Username =
                        result.Properties["sAMAccountName"][0]?.ToString() ?? targetUsername;
                if (result.Properties["displayName"].Count > 0)
                    info.DisplayName =
                        result.Properties["displayName"][0]?.ToString() ?? targetUsername;
            }
        }
        catch
        {
            // در صورت خطا، همان targetUsername می‌ماند
        }
        return info;
    }
}
