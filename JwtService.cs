using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using ActionPlanApi.Models;
using Microsoft.IdentityModel.Tokens;

namespace ActionPlanApi.Services;

public class JwtService
{
    private readonly IConfiguration _config;
    public JwtService(IConfiguration config) => _config = config;

    public string CreateToken(User user, List<string> roleCodes)
    {
        var key = _config["Jwt:Key"]!;
        var issuer = _config["Jwt:Issuer"];
        var audience = _config["Jwt:Audience"];
        var hours = int.TryParse(_config["Jwt:ExpiryHours"], out var h) ? h : 12;

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new("uid", user.Id.ToString()),
            new("username", user.Username),
            new("fullName", user.FullName),
        };
        foreach (var r in roleCodes)
            claims.Add(new Claim(ClaimTypes.Role, r));

        var creds = new SigningCredentials(
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key)),
            SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: issuer,
            audience: audience,
            claims: claims,
            expires: DateTime.UtcNow.AddHours(hours),
            signingCredentials: creds);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
