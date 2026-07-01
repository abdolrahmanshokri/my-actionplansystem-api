# راهنمای اجرای API — مرحله ۱ (.NET 6 + SQL Auth)

## نکته‌ی مهم نسخه
این پروژه روی **.NET 6** ساخته شده (چون سرور شرکت .NET 6 دارد).
روی سیستم خودت که .NET 9 داری هم اجرا می‌شود (.NET 9 پروژه‌ی ۶ را اجرا می‌کند).

اتصال به دیتابیس: **SQL Authentication** (یوزر/پسورد) در هر دو محیط.

---

## قدم ۰: ساخت لاگین SQL روی سیستم خودت (فقط یک‌بار)
چون قبلاً فقط Windows Auth داشتی، باید یک کاربر SQL بسازی.
در SSMS یک New Query باز کن و این را اجرا کن (Execute):

```sql
-- فعال‌کردن حالت ترکیبی لاگین (اگر نبود)
-- بعد از این باید SQL Server را ری‌استارت کنی (در SSMS: راست‌کلیک روی سرور > Restart)
USE [master]
GO
CREATE LOGIN [actionplan] WITH PASSWORD = N'Aa@123456',
    CHECK_POLICY = OFF;
GO
ALTER SERVER ROLE [sysadmin] ADD MEMBER [actionplan];
GO
```

سپس از مسیر زیر حالت Authentication را روی «SQL Server and Windows Authentication mode» بگذار:
راست‌کلیک روی سرور در SSMS > Properties > Security > گزینه‌ی دوم را انتخاب کن > OK
بعد سرور را ری‌استارت کن (راست‌کلیک روی سرور > Restart).

> اگر لاگین `actionplan` از قبل وجود داشت، خطای «already exists» می‌دهد — اشکالی ندارد.

---

## قدم ۱: ساخت پوشه و کپی فایل‌ها
یک پوشه‌ی جدید بساز:
```
C:\src\actionplan_api
```
همه‌ی فایل‌ها را با همین ساختار داخلش بگذار.

## قدم ۲: نصب ابزار EF Core سازگار با .NET 6
```
dotnet tool install --global dotnet-ef --version 6.0.25
```
اگر نسخه‌ی جدیدتری از قبل نصب است و خطا داد، این را بزن:
```
dotnet tool update --global dotnet-ef --version 6.0.25
```

## قدم ۳: رفتن به پوشه
```
cd C:\src\actionplan_api
```

## قدم ۴: بازیابی پکیج‌ها
```
dotnet restore
```

## قدم ۵: ساخت Migration اولیه
```
dotnet ef migrations add InitialCreate
```

## قدم ۶: اجرا
```
dotnet run
```
برنامه خودکار: دیتابیس ActionPlanDB را می‌سازد، جدول‌ها را می‌سازد، و داده‌ی اولیه را وارد می‌کند.

در پیام‌های ترمینال این کادر را می‌بینی:
```
محیط (Environment): Development
سرور دیتابیس: DESKTOP-CHCN7L3
```

## قدم ۷: تست
مرورگر:
```
http://localhost:5080/swagger
```
`GET /api/Health` > Try it out > Execute. باید برگردد:
```json
{ "status": "ok", "environment": "Development", "server": "DESKTOP-CHCN7L3", "database": "connected", "roles": 4, "users": 4 }
```

در SSMS هم Refresh کن > دیتابیس ActionPlanDB با جدول‌هایش ساخته شده.

---

## رمز کاربران اپ: همه `1234`
- ab.shokri (مدیر ارشد)
- y.nikakhtar, m.amirsadat, e.aradmehr (کاربر)

## برای سرور شرکت
رشته‌ی اتصال شرکت در appsettings.Production.json از قبل تنظیم شده:
Server=.  (یعنی همان دستگاه)، یوزر actionplan. اگر فرق داشت، همان‌جا عوض کن.
اجرا در حالت شرکت:
```
$env:ASPNETCORE_ENVIRONMENT="Production"
dotnet run
```
