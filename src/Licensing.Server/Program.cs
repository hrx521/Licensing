using System.Globalization;
using Licensing.Server.Api;
using Licensing.Server.Components;
using Licensing.Server.Data;
using Licensing.Server.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Localization;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

var connection = new SqliteConnectionStringBuilder(builder.Configuration.GetConnectionString("Licensing") ?? "Data Source=storage/licensing.db");
if (!Path.IsPathRooted(connection.DataSource))
    connection.DataSource = Path.Combine(builder.Environment.ContentRootPath, connection.DataSource);
Directory.CreateDirectory(Path.GetDirectoryName(connection.DataSource)!);
var connectionString = connection.ToString();

builder.Services.AddDbContextFactory<AppDbContext>(options => options.UseSqlite(connectionString));
builder.Services.AddScoped(sp => sp.GetRequiredService<IDbContextFactory<AppDbContext>>().CreateDbContext());
builder.Services.AddIdentity<AppUser, IdentityRole>(options =>
{
    options.Password.RequiredLength = 8;
    options.Password.RequireDigit = true;
    options.Password.RequireUppercase = true;
    options.Password.RequireLowercase = true;
    options.Password.RequireNonAlphanumeric = false;
    options.Lockout.MaxFailedAccessAttempts = 5;
    options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
    options.User.RequireUniqueEmail = true;
})
.AddEntityFrameworkStores<AppDbContext>()
.AddClaimsPrincipalFactory<AppUserClaimsPrincipalFactory>()
.AddDefaultTokenProviders();
builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath = "/login";
    options.AccessDeniedPath = "/denied";
});
builder.Services.AddAuthorization(options => options.AddPolicy("SuperAdmin", policy => policy.RequireClaim("super", "1")));
builder.Services.AddCascadingAuthenticationState();
builder.Services.AddHttpContextAccessor();
builder.Services.AddSingleton<SigningService>();
builder.Services.AddScoped<AuditService>();
builder.Services.AddScoped<AccountService>();
builder.Services.AddScoped<LicenseAdminService>();
builder.Services.AddScoped<ClientLicenseService>();
builder.Services.AddHostedService<MaintenanceService>();
builder.Services.AddRazorComponents().AddInteractiveServerComponents();
builder.Services.AddLocalization();

var app = builder.Build();
await DbInitializer.InitializeAsync(app.Services);

var cultures = new[] { new CultureInfo("zh-CN"), new CultureInfo("en") };
app.UseRequestLocalization(new RequestLocalizationOptions
{
    DefaultRequestCulture = new RequestCulture("zh-CN"),
    SupportedCultures = cultures,
    SupportedUICultures = cultures
});

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();
app.UseAntiforgery();
app.MapStaticAssets();

app.MapGet("/culture", (string? culture, string? redirectUri, HttpContext http) =>
{
    if (culture != "en")
        culture = "zh-CN";
    if (string.IsNullOrEmpty(redirectUri) || !redirectUri.StartsWith('/') || redirectUri.StartsWith("//", StringComparison.Ordinal))
        redirectUri = "/";
    http.Response.Cookies.Append(
        CookieRequestCultureProvider.DefaultCookieName,
        CookieRequestCultureProvider.MakeCookieValue(new RequestCulture(culture)),
        new CookieOptions { IsEssential = true, Expires = DateTimeOffset.UtcNow.AddYears(1), Path = "/" });
    return Results.LocalRedirect(redirectUri);
});

app.MapPost("/account/login", async (HttpContext http, SignInManager<AppUser> signIn, UserManager<AppUser> users, AuditService audit) =>
{
    var form = await http.Request.ReadFormAsync();
    var email = form["email"].ToString().Trim();
    var password = form["password"].ToString();
    var returnUrl = form["returnUrl"].ToString();
    if (string.IsNullOrEmpty(returnUrl) || !returnUrl.StartsWith('/') || returnUrl.StartsWith("//", StringComparison.Ordinal))
        returnUrl = "/";

    var user = await users.FindByEmailAsync(email);
    if (user == null)
    {
        await audit.WriteAsync(null, "LoginFailed", null, null, email, http.Connection.RemoteIpAddress?.ToString());
        return Results.Redirect("/login?error=1");
    }

    var check = await signIn.CheckPasswordSignInAsync(user, password, lockoutOnFailure: true);
    if (check.IsLockedOut)
        return Results.Redirect("/login?error=lockout");
    if (!check.Succeeded)
    {
        await audit.WriteAsync(user.Id, "LoginFailed", null, null, email, http.Connection.RemoteIpAddress?.ToString());
        return Results.Redirect("/login?error=1");
    }

    await signIn.SignInAsync(user, new AuthenticationProperties { IsPersistent = true, ExpiresUtc = DateTimeOffset.UtcNow.AddDays(14) });
    await audit.WriteAsync(user.Id, "LoginSuccess", null, null, email, http.Connection.RemoteIpAddress?.ToString());
    return Results.LocalRedirect(returnUrl);
}).AllowAnonymous().DisableAntiforgery();

app.MapPost("/account/logout", async (SignInManager<AppUser> signIn) =>
{
    await signIn.SignOutAsync();
    return Results.Redirect("/login");
}).DisableAntiforgery();

app.MapLicenseApi();
app.MapOfflineGrant();
app.MapRazorComponents<App>().AddInteractiveServerRenderMode();
app.Run();
