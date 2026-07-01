using ActionPlanApi.Models;
using ActionPlanApi.Services;

namespace ActionPlanApi.Data;

public static class DbSeeder
{
    public static void Seed(AppDbContext db)
    {
        if (!db.Roles.Any())
        {
            db.Roles.AddRange(
                new Role { Code = "super_admin", Title = "مدیر ارشد" },
                new Role { Code = "admin", Title = "مدیر" },
                new Role { Code = "admin2", Title = "مدیر سطح ۲" },
                new Role { Code = "action_user", Title = "کاربر اکشن‌پلن" }
            );
            db.SaveChanges();
        }

        if (!db.Users.Any())
        {
            var roles = db.Roles.ToDictionary(r => r.Code, r => r.Id);
            var pass = PasswordHasher.Hash("1234");

            void AddUser(string username, string fullName, string roleCode)
            {
                var u = new User
                {
                    Username = username,
                    FullName = fullName,
                    PasswordHash = pass,
                    IsActive = true
                };
                db.Users.Add(u);
                db.SaveChanges();
                db.UserRoles.Add(new UserRole { UserId = u.Id, RoleId = roles[roleCode] });
                db.SaveChanges();
            }

            AddUser("ab.shokri", "عبدالرحمن شکری", "super_admin");
            AddUser("y.nikakhtar", "یاسر نیک‌اختر", "action_user");
            AddUser("m.amirsadat", "محمد امیرسادات", "action_user");
            AddUser("e.aradmehr", "احسان آرادمهر", "action_user");
        }

        if (!db.AppSettings.Any())
        {
            db.AppSettings.AddRange(
                new AppSetting { Key = "developer_mode", Value = "true" },
                new AppSetting { Key = "show_inactive_admin2", Value = "true" },
                new AppSetting { Key = "show_inactive_user", Value = "false" },
                new AppSetting { Key = "show_notarget_admin2", Value = "true" },
                new AppSetting { Key = "show_notarget_user", Value = "false" }
            );
            db.SaveChanges();
        }

        // تنظیمات SSO به‌صورت مستقل اضافه می‌شوند (اگر از قبل نباشند)
        SeedSettingIfMissing(db, "sso_enabled", "false");
        SeedSettingIfMissing(db, "sso_client_id", "8e4843510792f53f4308");
        SeedSettingIfMissing(db, "sso_client_secret",
            "793f90d2bbd30e288e9fc02bcfbd5651239107af");
        SeedSettingIfMissing(db, "sso_authorize_url",
            "http://172.16.7.54:8000/login/oauth/authorize");
        SeedSettingIfMissing(db, "sso_token_url",
            "http://172.16.7.54:8000/api/login/oauth/access_token");
        SeedSettingIfMissing(db, "sso_userinfo_url",
            "http://172.16.7.54:8000/api/get-account");
        SeedSettingIfMissing(db, "sso_scope", "openid profile");
        SeedSettingIfMissing(db, "sso_username_field", "name");

        // تنظیمات Active Directory (LDAP)
        SeedSettingIfMissing(db, "ad_enabled", "false");
        SeedSettingIfMissing(db, "ad_ldap_path", "LDAP://172.31.15.2");
        SeedSettingIfMissing(db, "ad_domain", "jpc.ir");
        SeedSettingIfMissing(db, "ad_service_user", "");
        SeedSettingIfMissing(db, "ad_service_pass", "");
    }

    private static void SeedSettingIfMissing(
        AppDbContext db, string key, string value)
    {
        if (!db.AppSettings.Any(s => s.Key == key))
        {
            db.AppSettings.Add(new AppSetting { Key = key, Value = value });
            db.SaveChanges();
        }
    }
}
