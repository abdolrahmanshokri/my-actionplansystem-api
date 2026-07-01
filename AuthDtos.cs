namespace ActionPlanApi.Dtos;

public class LoginRequest
{
    public string Username { get; set; } = "";
    public string Password { get; set; } = "";
}

public class LoginResponse
{
    public string Token { get; set; } = "";
    public int UserId { get; set; }
    public string Username { get; set; } = "";
    public string FullName { get; set; } = "";
    public List<string> Roles { get; set; } = new();
}

public class UserDto
{
    public int Id { get; set; }
    public string Username { get; set; } = "";
    public string FullName { get; set; } = "";
    public bool IsActive { get; set; }
    public List<string> Roles { get; set; } = new();
}

public class CreateUserRequest
{
    public string Username { get; set; } = "";
    public string Password { get; set; } = "";
    public string FullName { get; set; } = "";
    public bool IsActive { get; set; } = true;
    public List<string> Roles { get; set; } = new();
}

public class UpdateUserRequest
{
    public string FullName { get; set; } = "";
    public bool IsActive { get; set; } = true;
    public List<string> Roles { get; set; } = new();
}

public class ChangePasswordRequest
{
    public string NewPassword { get; set; } = "";
}

public class SsoExchangeRequest
{
    public string Ticket { get; set; } = "";
}

public class AdTestRequest
{
    public string LdapPath { get; set; } = "";
    public string ServiceUser { get; set; } = "";
    public string ServicePass { get; set; } = "";
}
