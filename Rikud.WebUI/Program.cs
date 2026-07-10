using Microsoft.EntityFrameworkCore;
using Rikud.DLL.AppDbContext;
using Rikud.WebUI.Middlewares;
using Rikud.WebUI.Models.Security;
using Microsoft.AspNetCore.DataProtection;
using System.IO;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
// ---------- Services ----------
builder.Services.AddControllersWithViews();
builder.Services.AddRazorPages();

builder.Services.AddHttpContextAccessor();
builder.Services.AddSingleton<IHttpContextAccessor, HttpContextAccessor>();

// EF Core: SqlTest ile çalış
builder.Services.AddDbContextPool<RikudDbContext>(opt =>
{
    var cs = builder.Configuration.GetConnectionString("SqlMain"); //SqlMain SqlTest
    opt.UseSqlServer(cs, sql =>
        sql.EnableRetryOnFailure(maxRetryCount: 3, maxRetryDelay: TimeSpan.FromSeconds(5), errorNumbersToAdd: null));

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

builder.Services.AddAuthentication("RikudUI")
    .AddCookie("RikudUI", o =>
    {
        o.Cookie.Name = "RikudUI";
        o.LoginPath = "/User/Login";
        o.LogoutPath = "/User/Logout";
        o.AccessDeniedPath = "/User/Login";
        o.ExpireTimeSpan = TimeSpan.FromDays(365);
        o.SlidingExpiration = true;
        o.Cookie.HttpOnly = true;
        o.Cookie.SameSite = SameSiteMode.Lax;
        // o.Cookie.SecurePolicy = CookieSecurePolicy.Always; // HTTPS varsa aç
    });

builder.Services.AddHttpClient();

builder.Services.AddMemoryCache();


builder.Services.Configure<ReCAPTCHASettings>(builder.Configuration.GetSection("GoogleReCAPTCHA"));
builder.Services.AddHttpClient();
builder.Services.AddDataProtection()
    .PersistKeysToFileSystem(new DirectoryInfo(
        Path.Combine(builder.Environment.ContentRootPath, "DataProtection-Keys")))
    .SetApplicationName("Rikud.WebUI");

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
app.UseSession();
app.UseAuthentication();
app.UseAuthorization();
//app.UseMiddleware<SessionGuardMiddleware>();

// (Aspire kullanıyorsan) statik asset mapping
app.MapStaticAssets();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets(); // Aspire kullanmıyorsan bu satırı kaldır

app.MapRazorPages();



app.Run();
