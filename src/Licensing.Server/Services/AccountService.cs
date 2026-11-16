using System.Security.Claims;
using System.Text.Json;
using Licensing.Client;
using Licensing.Server.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Licensing.Server.Services;

public sealed class AuditService
{
    readonly IDbContextFactory<AppDbContext> _factory;
    readonly IHttpContextAccessor _http;

    public AuditService(IDbContextFactory<AppDbContext> factory, IHttpContextAccessor http)
    {
        _factory = factory;
        _http = http;
    }

    public async Task WriteAsync(string? actorUserId, string action, Guid? customerId, Guid? licenseId, string? detail, string? ip = null)
    {
        await using var db = await _factory.CreateDbContextAsync();
        db.AuditLogs.Add(new AuditLog
        {
            Id = Guid.NewGuid(),
            At = DateTimeOffset.UtcNow,
            ActorUserId = actorUserId,
            Action = action,
            CustomerId = customerId,
            LicenseId = licenseId,
            Detail = detail,
            Ip = ip ?? _http.HttpContext?.Connection.RemoteIpAddress?.ToString()
        });
        await db.SaveChangesAsync();
    }
}

public sealed class AccountService
{
    readonly IServiceScopeFactory _scopes;
    readonly AuditService _audit;

    public AccountService(IServiceScopeFactory scopes, AuditService audit)
    {
        _scopes = scopes;
        _audit = audit;
    }

    public async Task<List<StaffRow>> ListAsync()
    {
        await using var scope = _scopes.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var users = await db.Users.AsNoTracking().OrderBy(x => x.Email).ToListAsync();
        var links = await db.StaffCustomers.AsNoTracking().ToListAsync();
        return users.Select(user => new StaffRow
        {
            Id = user.Id,
            Email = user.Email ?? "",
            DisplayName = user.DisplayName,
            IsSuperAdmin = user.IsSuperAdmin,
            CustomerIds = links.Where(x => x.UserId == user.Id).Select(x => x.CustomerId).ToList()
        }).ToList();
    }

    public async Task<string?> CreateAsync(string email, string displayName, string password, bool superAdmin, IReadOnlyCollection<Guid> customerIds, ClaimsPrincipal actor)
    {
        await using var scope = _scopes.CreateAsyncScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var user = new AppUser
        {
            UserName = email.Trim(),
            Email = email.Trim(),
            EmailConfirmed = true,
            DisplayName = displayName.Trim(),
            IsSuperAdmin = superAdmin
        };
        var result = await users.CreateAsync(user, password);
        if (!result.Succeeded)
            return string.Join(" ", result.Errors.Select(x => x.Description));
        if (!superAdmin)
            await ReplaceLinks(db, user.Id, customerIds);
        await _audit.WriteAsync(Actor(actor), "StaffCreate", null, null, user.Email);
        return null;
    }

    public async Task<string?> ResetPasswordAsync(string userId, string password, ClaimsPrincipal actor)
    {
        await using var scope = _scopes.CreateAsyncScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();
        var user = await users.FindByIdAsync(userId);
        if (user == null)
            return Ui.T("没有这个账号。", "Account was not found.");
        var token = await users.GeneratePasswordResetTokenAsync(user);
        var result = await users.ResetPasswordAsync(user, token, password);
        if (!result.Succeeded)
            return string.Join(" ", result.Errors.Select(x => x.Description));
        await _audit.WriteAsync(Actor(actor), "StaffPassword", null, null, user.Email);
        return null;
    }

    public async Task AssignAsync(string userId, IReadOnlyCollection<Guid> customerIds, ClaimsPrincipal actor)
    {
        await using var scope = _scopes.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await ReplaceLinks(db, userId, customerIds);
        await _audit.WriteAsync(Actor(actor), "StaffAssign", null, null, userId);
    }

    static async Task ReplaceLinks(AppDbContext db, string userId, IReadOnlyCollection<Guid> customerIds)
    {
        var existing = await db.StaffCustomers.Where(x => x.UserId == userId).ToListAsync();
        db.StaffCustomers.RemoveRange(existing);
        foreach (var customerId in customerIds.Distinct())
            db.StaffCustomers.Add(new StaffCustomer { UserId = userId, CustomerId = customerId });
        await db.SaveChangesAsync();
    }

    static string? Actor(ClaimsPrincipal user) => user.FindFirstValue(ClaimTypes.NameIdentifier);
}

public sealed class StaffRow
{
    public string Id { get; set; } = "";
    public string Email { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public bool IsSuperAdmin { get; set; }
    public List<Guid> CustomerIds { get; set; } = new();
}
