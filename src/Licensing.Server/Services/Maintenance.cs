using Licensing.Server.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Licensing.Server.Services;

public static class DbInitializer
{
    public static async Task InitializeAsync(IServiceProvider services)
    {
        await using var scope = services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.Database.MigrateAsync();
        var signing = scope.ServiceProvider.GetRequiredService<SigningService>();
        signing.Initialize(db);

        var users = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();
        if (await users.Users.AnyAsync())
            return;

        var config = scope.ServiceProvider.GetRequiredService<IConfiguration>();
        var email = config["Bootstrap:Email"];
        var password = config["Bootstrap:Password"];
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
        {
            var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("Licensing");
            logger.LogWarning("No accounts exist. Set Bootstrap:Email and Bootstrap:Password before the first start.");
            return;
        }

        var user = new AppUser
        {
            UserName = email.Trim(),
            Email = email.Trim(),
            EmailConfirmed = true,
            DisplayName = "Administrator",
            IsSuperAdmin = true
        };
        var result = await users.CreateAsync(user, password);
        if (!result.Succeeded)
        {
            var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("Licensing");
            logger.LogError("Bootstrap account was not created: {Errors}", string.Join("; ", result.Errors.Select(x => x.Description)));
        }
    }
}

public sealed class MaintenanceService : BackgroundService
{
    readonly IDbContextFactory<AppDbContext> _factory;
    readonly ILogger<MaintenanceService> _logger;

    public MaintenanceService(IDbContextFactory<AppDbContext> factory, ILogger<MaintenanceService> logger)
    {
        _factory = factory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await Task.Delay(TimeSpan.FromSeconds(15), stoppingToken);
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await using var db = await _factory.CreateDbContextAsync(stoppingToken);
                var now = DateTimeOffset.UtcNow;
                var floating = await db.Activations.Include(x => x.License)
                    .Where(x => x.ReleasedAt == null && x.License.Kind == LicenseKind.Floating)
                    .ToListAsync(stoppingToken);
                foreach (var row in floating)
                {
                    if (row.LastHeartbeatAt.AddSeconds(row.License.FloatingReleaseSeconds) <= now)
                    {
                        row.ReleasedAt = now;
                        row.ReleaseReason = "HeartbeatTimeout";
                    }
                }

                var cutoff = now.AddYears(-3);
                var expiredIds = (await db.AuditLogs.Select(x => new { x.Id, x.At }).ToListAsync(stoppingToken))
                    .Where(x => x.At < cutoff)
                    .Select(x => x.Id)
                    .ToList();
                if (expiredIds.Count > 0)
                    await db.AuditLogs.Where(x => expiredIds.Contains(x.Id)).ExecuteDeleteAsync(stoppingToken);
                await db.SaveChangesAsync(stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "License maintenance failed.");
            }

            await Task.Delay(TimeSpan.FromMinutes(2), stoppingToken);
        }
    }
}
