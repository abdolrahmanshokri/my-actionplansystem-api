using System.Text;
using ActionPlanApi.Data;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);

// اگر محیط مشخص نشده، پیش‌فرض Development (دیتابیس سیستم خودت)
if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT")))
{
    builder.Environment.EnvironmentName = "Development";
}

// پورت ثابت 5080 (مستقل از launchSettings)
builder.WebHost.UseUrls("http://0.0.0.0:5080");

// اتصال به SQL Server
var connStr = builder.Configuration.GetConnectionString("Default");
if (string.IsNullOrWhiteSpace(connStr))
{
    throw new InvalidOperationException(
        "رشته‌ی اتصال خالی است. لطفاً در appsettings.Development.json " +
        "یا appsettings.Production.json مقدار ConnectionStrings:Default را تنظیم کنید.");
}
builder.Services.AddDbContext<AppDbContext>(opt =>
    opt.UseSqlServer(connStr));

// JWT
var jwtKey = builder.Configuration["Jwt:Key"]!;
var jwtIssuer = builder.Configuration["Jwt:Issuer"];
var jwtAudience = builder.Configuration["Jwt:Audience"];

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(opt =>
    {
        opt.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwtIssuer,
            ValidAudience = jwtAudience,
            IssuerSigningKey =
                new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey))
        };
    });

builder.Services.AddAuthorization();
builder.Services.AddScoped<ActionPlanApi.Services.JwtService>();
builder.Services.AddScoped<ActionPlanApi.Services.ActivityStatusService>();
builder.Services.AddScoped<ActionPlanApi.Services.ActiveDirectoryService>();
builder.Services.AddHttpClient();
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.AddSecurityDefinition("Bearer", new Microsoft.OpenApi.Models.OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = Microsoft.OpenApi.Models.SecuritySchemeType.Http,
        Scheme = "Bearer",
        BearerFormat = "JWT",
        In = Microsoft.OpenApi.Models.ParameterLocation.Header,
        Description = "توکن JWT را اینجا وارد کنید (بدون کلمه‌ی Bearer)"
    });
    c.AddSecurityRequirement(new Microsoft.OpenApi.Models.OpenApiSecurityRequirement
    {
        {
            new Microsoft.OpenApi.Models.OpenApiSecurityScheme
            {
                Reference = new Microsoft.OpenApi.Models.OpenApiReference
                {
                    Type = Microsoft.OpenApi.Models.ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            Array.Empty<string>()
        }
    });
});

// CORS تا فلاتر وب بتواند وصل شود
builder.Services.AddCors(opt =>
{
    opt.AddPolicy("AllowAll", p =>
        p.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod());
});

var app = builder.Build();

// ساخت دیتابیس و داده‌ی اولیه به‌صورت خودکار
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.Migrate();
    DbSeeder.Seed(db);

    var env = app.Environment.EnvironmentName;
    var server = db.Database.GetDbConnection().DataSource;
    Console.WriteLine("====================================");
    Console.WriteLine($"  محیط (Environment): {env}");
    Console.WriteLine($"  سرور دیتابیس: {server}");
    Console.WriteLine("====================================");
}

app.UseSwagger();
app.UseSwaggerUI();

app.UseCors("AllowAll");

// سرو کردن فایل‌های وب فلاتر (که در پوشه‌ی wwwroot قرار می‌گیرند)
app.UseDefaultFiles();
app.UseStaticFiles();

app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

// هر مسیری که کنترلر/فایل نبود، به index.html فلاتر برود (SPA fallback)
app.MapFallbackToFile("index.html");

app.Run();
