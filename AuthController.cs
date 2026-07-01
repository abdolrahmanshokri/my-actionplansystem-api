using ActionPlanApi.Data;
using ActionPlanApi.Dtos;
using ActionPlanApi.Models;
using ActionPlanApi.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Collections.Concurrent;

namespace ActionPlanApi.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly JwtService _jwt;
    private readonly IHttpClientFactory _httpFactory;
    private readonly ActiveDirectoryService _ad;

    public AuthController(AppDbContext db, JwtService jwt,
        IHttpClientFactory httpFactory, ActiveDirectoryService ad)
    {
        _db = db;
        _jwt = jwt;
        _httpFactory = httpFactory;
        _ad = ad;
    }

    // نگه‌داری موقت state (returnUrl) و ticketهای یکبارمصرف در حافظه
    private static readonly ConcurrentDictionary<string, string> _stateReturnUrls
        = new();
    private static readonly ConcurrentDictionary<string, (string username, DateTime exp)>
        _tickets = new();

    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest req)
    {
        var user = await _db.Users
            .Include(u => u.UserRoles).ThenInclude(ur => ur.Role)
            .FirstOrDefaultAsync(u => u.Username == req.Username);

        if (user == null)
            return Unauthorized(new { message = "نام کاربری یا رمز عبور اشتباه است" });

        if (!user.IsActive)
            return Unauthorized(new { message = "حساب کاربری غیرفعال است" });

        if (!PasswordHasher.Verify(req.Password, user.PasswordHash))
            return Unauthorized(new { message = "نام کاربری یا رمز عبور اشتباه است" });

        var roleCodes = user.UserRoles
            .Where(ur => ur.Role != null)
            .Select(ur => ur.Role!.Code)
            .ToList();

        var token = _jwt.CreateToken(user, roleCodes);

        return Ok(new LoginResponse
        {
            Token = token,
            UserId = user.Id,
            Username = user.Username,
            FullName = user.FullName,
            Roles = roleCodes
        });
    }

    // ورود با Active Directory
    [HttpPost("ad-login")]
    public async Task<IActionResult> AdLogin([FromBody] LoginRequest req)
    {
        var enabled = await Setting("ad_enabled");
        if (enabled != "true")
            return BadRequest(new { message = "ورود با اکتیو دایرکتوری فعال نیست" });

        var ldapPath = await Setting("ad_ldap_path");
        var serviceUser = await Setting("ad_service_user");
        var servicePass = await Setting("ad_service_pass");
        if (string.IsNullOrEmpty(ldapPath))
            return BadRequest(new { message = "تنظیمات اکتیو دایرکتوری ناقص است" });

        var username = ActiveDirectoryService.NormalizeUsername(req.Username);

        // بررسی نام کاربری و رمز با LDAP
        var ok = _ad.Authenticate(ldapPath, req.Username, req.Password);
        if (!ok)
            return Unauthorized(new { message = "نام کاربری یا رمز عبور اشتباه است" });

        // گرفتن نام نمایشی
        var info = _ad.GetUserInfo(ldapPath, serviceUser, servicePass, username);
        var displayName = string.IsNullOrEmpty(info.DisplayName)
            ? username : info.DisplayName;

        // یافتن یا ساختن کاربر
        var user = await _db.Users
            .Include(u => u.UserRoles).ThenInclude(ur => ur.Role)
            .FirstOrDefaultAsync(u => u.Username == username);
        if (user == null)
        {
            user = new User
            {
                Username = username,
                FullName = displayName,
                PasswordHash = PasswordHasher.Hash(Guid.NewGuid().ToString()),
                IsActive = true
            };
            _db.Users.Add(user);
            await _db.SaveChangesAsync();

            var role = await _db.Roles.FirstOrDefaultAsync(r => r.Code == "action_user");
            if (role != null)
            {
                _db.UserRoles.Add(new UserRole { UserId = user.Id, RoleId = role.Id });
                await _db.SaveChangesAsync();
            }
            user = await _db.Users
                .Include(u => u.UserRoles).ThenInclude(ur => ur.Role)
                .FirstAsync(u => u.Id == user.Id);
        }

        if (!user.IsActive)
            return Unauthorized(new { message = "حساب کاربری غیرفعال است" });

        var roleCodes = user.UserRoles
            .Where(ur => ur.Role != null)
            .Select(ur => ur.Role!.Code)
            .ToList();
        var token = _jwt.CreateToken(user, roleCodes);

        return Ok(new LoginResponse
        {
            Token = token,
            UserId = user.Id,
            Username = user.Username,
            FullName = user.FullName,
            Roles = roleCodes
        });
    }

    // تست اتصال به LDAP با کاربر سرویس (برای ادمین)
    [HttpPost("ad-test")]
    public async Task<IActionResult> AdTest([FromBody] AdTestRequest req)
    {
        var ldapPath = string.IsNullOrEmpty(req.LdapPath)
            ? await Setting("ad_ldap_path") : req.LdapPath;
        var serviceUser = string.IsNullOrEmpty(req.ServiceUser)
            ? await Setting("ad_service_user") : req.ServiceUser;
        var servicePass = string.IsNullOrEmpty(req.ServicePass)
            ? await Setting("ad_service_pass") : req.ServicePass;

        if (string.IsNullOrEmpty(ldapPath))
            return Ok(new { ok = false, message = "آدرس LDAP خالی است" });

        try
        {
            var ok = _ad.Authenticate(ldapPath, serviceUser, servicePass);
            if (ok)
                return Ok(new { ok = true, message = "اتصال موفق بود" });
            return Ok(new { ok = false, message = "اتصال ناموفق: نام کاربری یا رمز سرویس اشتباه است" });
        }
        catch (Exception ex)
        {
            return Ok(new { ok = false, message = $"خطا: {ex.Message}" });
        }
    }

    // شروع جریان SSO: کاربر را به authorize سرور هویت هدایت می‌کند
    [HttpGet("sso-login")]
    public async Task<IActionResult> SsoLogin([FromQuery] string returnUrl)
    {
        var enabled = await Setting("sso_enabled");
        if (enabled != "true")
            return BadRequest(new { message = "ورود با SSO فعال نیست" });

        var clientId = await Setting("sso_client_id");
        var authorizeUrl = await Setting("sso_authorize_url");
        var scope = await Setting("sso_scope");
        if (string.IsNullOrEmpty(clientId) || string.IsNullOrEmpty(authorizeUrl))
            return BadRequest(new { message = "تنظیمات SSO ناقص است" });

        var state = Guid.NewGuid().ToString("N");
        _stateReturnUrls[state] = returnUrl ?? "";

        var redirectUri = $"{Request.Scheme}://{Request.Host}/api/Auth/sso-callback";
        var url = $"{authorizeUrl}?client_id={Uri.EscapeDataString(clientId)}" +
                  $"&redirect_uri={Uri.EscapeDataString(redirectUri)}" +
                  $"&response_type=code" +
                  $"&scope={Uri.EscapeDataString(scope)}" +
                  $"&state={state}";
        return Redirect(url);
    }

    // بازگشت از SSO با code
    [HttpGet("sso-callback")]
    public async Task<IActionResult> SsoCallback(
        [FromQuery] string code, [FromQuery] string state)
    {
        _stateReturnUrls.TryRemove(state, out var returnUrl);
        returnUrl ??= "";

        if (string.IsNullOrEmpty(code))
            return Redirect(AppendError(returnUrl, "کد دریافت نشد"));

        var clientId = await Setting("sso_client_id");
        var clientSecret = await Setting("sso_client_secret");
        var tokenUrl = await Setting("sso_token_url");
        var userinfoUrl = await Setting("sso_userinfo_url");
        var usernameField = await Setting("sso_username_field");
        if (string.IsNullOrEmpty(usernameField)) usernameField = "name";

        var redirectUri = $"{Request.Scheme}://{Request.Host}/api/Auth/sso-callback";
        var http = _httpFactory.CreateClient();

        // تبادل code با access_token
        var tokenParams = new Dictionary<string, string>
        {
            { "client_id", clientId },
            { "client_secret", clientSecret },
            { "code", code },
            { "redirect_uri", redirectUri },
            { "grant_type", "authorization_code" }
        };
        var tokenResp = await http.PostAsync(tokenUrl,
            new FormUrlEncodedContent(tokenParams));
        if (!tokenResp.IsSuccessStatusCode)
            return Redirect(AppendError(returnUrl, "خطا در دریافت توکن"));

        var tokenJson = System.Text.Json.JsonDocument.Parse(
            await tokenResp.Content.ReadAsStringAsync());
        if (!tokenJson.RootElement.TryGetProperty("access_token", out var atElem))
            return Redirect(AppendError(returnUrl, "توکن نامعتبر"));
        var accessToken = atElem.GetString();

        // گرفتن اطلاعات کاربر
        http.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);
        var userResp = await http.GetAsync(userinfoUrl);
        if (!userResp.IsSuccessStatusCode)
            return Redirect(AppendError(returnUrl, "خطا در دریافت اطلاعات کاربر"));

        var userJson = System.Text.Json.JsonDocument.Parse(
            await userResp.Content.ReadAsStringAsync());
        var username = ReadField(userJson.RootElement, usernameField);
        if (string.IsNullOrEmpty(username))
            return Redirect(AppendError(returnUrl, "نام کاربری یافت نشد"));

        // یافتن یا ساختن کاربر
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Username == username);
        if (user == null)
        {
            user = new User
            {
                Username = username,
                FullName = username,
                PasswordHash = PasswordHasher.Hash(Guid.NewGuid().ToString()),
                IsActive = true
            };
            _db.Users.Add(user);
            await _db.SaveChangesAsync();

            var role = await _db.Roles.FirstOrDefaultAsync(r => r.Code == "action_user");
            if (role != null)
            {
                _db.UserRoles.Add(new UserRole { UserId = user.Id, RoleId = role.Id });
                await _db.SaveChangesAsync();
            }
        }

        if (!user.IsActive)
            return Redirect(AppendError(returnUrl, "حساب کاربری غیرفعال است"));

        // ساخت ticket یکبارمصرف (کوتاه‌عمر)
        var ticket = Guid.NewGuid().ToString("N");
        _tickets[ticket] = (username, DateTime.UtcNow.AddMinutes(2));

        var sep = returnUrl.Contains('?') ? '&' : '?';
        return Redirect($"{returnUrl}{sep}ssoTicket={ticket}");
    }

    // تبدیل ticket یکبارمصرف به توکن JWT اصلی
    [HttpPost("sso-exchange")]
    public async Task<IActionResult> SsoExchange([FromBody] SsoExchangeRequest req)
    {
        if (!_tickets.TryRemove(req.Ticket, out var entry))
            return Unauthorized(new { message = "بلیت نامعتبر است" });
        if (DateTime.UtcNow > entry.exp)
            return Unauthorized(new { message = "بلیت منقضی شده است" });

        var user = await _db.Users
            .Include(u => u.UserRoles).ThenInclude(ur => ur.Role)
            .FirstOrDefaultAsync(u => u.Username == entry.username);
        if (user == null)
            return Unauthorized(new { message = "کاربر یافت نشد" });

        var roleCodes = user.UserRoles
            .Where(ur => ur.Role != null)
            .Select(ur => ur.Role!.Code)
            .ToList();
        var token = _jwt.CreateToken(user, roleCodes);

        return Ok(new LoginResponse
        {
            Token = token,
            UserId = user.Id,
            Username = user.Username,
            FullName = user.FullName,
            Roles = roleCodes
        });
    }

    private async Task<string> Setting(string key)
    {
        var s = await _db.AppSettings.FirstOrDefaultAsync(x => x.Key == key);
        return s?.Value ?? "";
    }

    // خواندن یک فیلد از JSON (پشتیبانی از فیلد تودرتو مثل data.name)
    private static string ReadField(System.Text.Json.JsonElement root, string field)
    {
        if (root.TryGetProperty(field, out var direct))
            return direct.ValueKind == System.Text.Json.JsonValueKind.String
                ? direct.GetString() ?? "" : direct.ToString();
        // جستجو در data
        if (root.TryGetProperty("data", out var data) &&
            data.TryGetProperty(field, out var nested))
            return nested.ValueKind == System.Text.Json.JsonValueKind.String
                ? nested.GetString() ?? "" : nested.ToString();
        return "";
    }

    private static string AppendError(string returnUrl, string msg)
    {
        var sep = returnUrl.Contains('?') ? '&' : '?';
        return $"{returnUrl}{sep}ssoError={Uri.EscapeDataString(msg)}";
    }
}
