using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;
using Rikud.DLL.AppDbContext;
using Rikud.WebAdmin.Models.Security;
using System;

var builder = WebApplication.CreateBuilder(args);

// ---------- Services ----------
builder.Services.AddControllersWithViews();
builder.Services.AddRazorPages();

builder.Services.AddHttpContextAccessor();
builder.Services.AddSingleton<IHttpContextAccessor, HttpContextAccessor>();

// EF Core: SqlTest ile çalış
builder.Services.AddDbContextPool<RikudDbContext>(opt =>
{
    var cs = builder.Configuration.GetConnectionString("SqlMain"); //SqlMain SqlTest
    opt.UseSqlServer(cs);

    if (builder.Environment.IsDevelopment())
    {
        opt.EnableSensitiveDataLogging();
        opt.EnableDetailedErrors();
    }
});

// Session
builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(o =>
{
    o.Cookie.Name = ".Rikud.Session";
    o.IdleTimeout = TimeSpan.FromDays(365);
    o.Cookie.HttpOnly = true;
    o.Cookie.IsEssential = true;
});

// Cookie Authentication
builder.Services
    .AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(o =>
    {
        o.Cookie.Name = "RikudAdmin";
        o.LoginPath = "/User/Login";
        o.ExpireTimeSpan = TimeSpan.FromDays(365);
        o.SlidingExpiration = true;
        o.Cookie.HttpOnly = true;
        o.Cookie.SameSite = SameSiteMode.Lax;
        // o.Cookie.SecurePolicy = CookieSecurePolicy.Always; // HTTPS zorunlu ise aç
    });

builder.Services.Configure<ReCAPTCHASettings>(builder.Configuration.GetSection("GoogleReCAPTCHA"));
builder.Services.AddHttpClient();
builder.Services.AddScoped<IReCaptchaService, GoogleReCaptchaService>();

var app = builder.Build();

// ---------- Pipeline ----------
if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
}
else
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

//app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();
app.UseSession();

// (Aspire kullanıyorsan) statik asset mapping
app.MapStaticAssets();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets(); // Aspire kullanmıyorsan bu satırı kaldır

app.MapRazorPages();

app.Run();
