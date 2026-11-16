using System.Text.Json;
using Licensing.Client;
using Licensing.Server.Data;
using Microsoft.EntityFrameworkCore;

namespace Licensing.Server.Services;

public enum ClientOperation
{
    Activate,
    Heartbeat,
    Deactivate,
    Rehost
}

public sealed class ClientLicenseService
{
    readonly IDbContextFactory<AppDbContext> _factory;
    readonly SigningService _signing;

    public ClientLicenseService(IDbContextFactory<AppDbContext> factory, SigningService signing)
    {
        _factory = factory;
        _signing = signing;
    }

    public async Task<LicenseCallResponse> HandleAsync(ClientOperation operation, LicenseCallRequest? request, bool english, CancellationToken cancellationToken)
    {
        var now = Truncate(DateTimeOffset.UtcNow);
        if (request == null || string.IsNullOrWhiteSpace(request.ProductId))
            return Fail("InvalidRequest", "MissingProduct", english, now);
        if (string.IsNullOrWhiteSpace(request.MachineId))
            return Fail("InvalidRequest", "MissingMachine", english, now);

        var machineId = request.MachineId.Trim();
        var dongleId = request.Dongle?.DongleId?.Trim();
        var normalized = KeyNormalize.Normalize(request.LicenseKey);
        if (normalized.Length == 0 && string.IsNullOrEmpty(dongleId))
            return Fail("InvalidRequest", "MissingCredential", english, now);

        await using var db = await _factory.CreateDbContextAsync(cancellationToken);
        await using var tx = await db.Database.BeginTransactionAsync(cancellationToken);

        var located = await FindLicenseAsync(db, request.ProductId.Trim(), normalized, dongleId, cancellationToken);
        if (located.Ambiguous)
            return Fail("InvalidRequest", "AmbiguousDongle", english, now);
        var license = located.License;
        if (license == null)
            return Fail("NotFound", "NotFound", english, now);
        if (license.Product.PublicId != request.ProductId.Trim())
            return Fail("NotFound", "NotFound", english, now);

        if (operation == ClientOperation.Deactivate)
        {
            foreach (var row in license.Activations.Where(a => a.ReleasedAt == null && a.MachineId == machineId))
                Release(row, now, "Client");
            await db.SaveChangesAsync(cancellationToken);
            await tx.CommitAsync(cancellationToken);
            return Ok("Released", english, now, null);
        }

        if (operation == ClientOperation.Rehost && license.Kind is not (LicenseKind.SingleMachine or LicenseKind.MachinePool))
            return Fail("InvalidRequest", "RehostNotApplicable", english, now);

        if (license.Status == LicenseState.Revoked)
            return Fail("Revoked", "Revoked", english, now);
        if (license.Status == LicenseState.Suspended)
            return Fail("Suspended", "Suspended", english, now);
        if (!license.IsPerpetual && (license.SubscriptionEndsAt == null || license.SubscriptionEndsAt <= now))
            return Fail("Expired", "Expired", english, now);

        if (license.Kind == LicenseKind.Dongle)
        {
            if (string.IsNullOrEmpty(dongleId))
                return Fail("InvalidRequest", "MissingDongle", english, now);
            if (!string.Equals(license.DongleId, dongleId, StringComparison.Ordinal))
                return Fail("DongleMismatch", "DongleMismatch", english, now);
        }

        ExpireFloating(license, now);
        var decision = license.Kind switch
        {
            LicenseKind.Dongle => BindSighting(db, license, machineId, dongleId, now, request),
            LicenseKind.LegacyToken => BindSighting(db, license, machineId, dongleId, now, request),
            LicenseKind.Floating => BindFloating(db, license, machineId, now, request),
            _ => await BindMachineAsync(db, license, machineId, now, request, operation == ClientOperation.Rehost, cancellationToken)
        };

        if (decision != null)
            return Fail(decision, decision, english, now);

        var grant = Sign(license, machineId, dongleId, now, PresentedExpiry(request, license, machineId), null);
        await db.SaveChangesAsync(cancellationToken);
        await tx.CommitAsync(cancellationToken);
        return Ok("Active", english, now, grant);
    }

    static async Task<(License? License, bool Ambiguous)> FindLicenseAsync(AppDbContext db, string productId, string normalizedKey, string? dongleId, CancellationToken cancellationToken)
    {
        IQueryable<License> query = db.Licenses
            .AsSplitQuery()
            .Include(x => x.Product)
            .Include(x => x.Features)
            .Include(x => x.Activations);
        if (normalizedKey.Length > 0)
            return (await query.SingleOrDefaultAsync(x => x.KeyNormalized == normalizedKey && x.Product.PublicId == productId, cancellationToken), false);

        var matches = await query.Where(x => x.Kind == LicenseKind.Dongle && x.DongleId == dongleId && x.Product.PublicId == productId).ToListAsync(cancellationToken);
        if (matches.Count > 1)
            return (null, true);
        return (matches.SingleOrDefault(), false);
    }

    static string? BindSighting(AppDbContext db, License license, string machineId, string? dongleId, DateTimeOffset now, LicenseCallRequest request)
    {
        var row = Touch(db, license, machineId, now, request, dongleId);
        NoteSnapshot(license, row, request.Dongle);
        return null;
    }

    static string? BindFloating(AppDbContext db, License license, string machineId, DateTimeOffset now, LicenseCallRequest request)
    {
        var holding = license.Activations.Where(a => IsHolding(license, a, now)).ToList();
        if (holding.All(a => a.MachineId != machineId) && holding.Count >= Math.Max(1, license.SeatLimit))
            return "SeatLimit";
        Touch(db, license, machineId, now, request, request.Dongle?.DongleId);
        return null;
    }

    async Task<string?> BindMachineAsync(AppDbContext db, License license, string machineId, DateTimeOffset now, LicenseCallRequest request, bool rehost, CancellationToken cancellationToken)
    {
        var limit = Math.Max(1, license.SeatLimit);
        var holding = license.Activations.Where(a => IsHolding(license, a, now)).ToList();
        if (holding.Any(a => a.MachineId == machineId))
        {
            Touch(db, license, machineId, now, request, null);
            return null;
        }

        var others = holding.Where(a => a.MachineId != machineId).ToList();
        if (license.Kind == LicenseKind.SingleMachine && others.Count > 0)
        {
            if (!rehost)
                return "MachineInUse";
            if (!await QuotaAllowsAsync(db, license, now, cancellationToken))
                return "RehostLimit";
            foreach (var other in others)
                Release(other, now, "Rehost");
            Touch(db, license, machineId, now, request, null);
            AddRehost(db, license, others[0].MachineId, machineId, now, RehostActor.Client);
            return null;
        }

        if (holding.Count >= limit)
            return "SeatLimit";

        var seen = license.Activations.Any(a => a.MachineId == machineId);
        var distinct = license.Activations.Select(a => a.MachineId).Distinct(StringComparer.Ordinal).Count();
        var counts = !seen && license.Activations.Count > 0 && distinct >= limit;
        if (counts && !await QuotaAllowsAsync(db, license, now, cancellationToken))
            return "RehostLimit";

        Touch(db, license, machineId, now, request, null);
        if (counts)
        {
            var from = license.Activations
                .Where(a => a.MachineId != machineId)
                .OrderByDescending(a => a.LastHeartbeatAt)
                .Select(a => a.MachineId)
                .FirstOrDefault();
            AddRehost(db, license, from, machineId, now, RehostActor.Client);
        }

        return null;
    }

    static Activation Touch(AppDbContext db, License license, string machineId, DateTimeOffset now, LicenseCallRequest request, string? dongleId)
    {
        var row = license.Activations.Where(a => a.MachineId == machineId).OrderByDescending(a => a.CreatedAt).FirstOrDefault();
        if (row == null)
        {
            row = new Activation
            {
                LicenseId = license.Id,
                MachineId = machineId,
                CreatedAt = now
            };
            db.Activations.Add(row);
        }

        row.ReleasedAt = null;
        row.ReleaseReason = null;
        row.LastHeartbeatAt = now;
        row.DongleId = string.IsNullOrWhiteSpace(dongleId) ? row.DongleId : dongleId;
        row.AppVersion = string.IsNullOrWhiteSpace(request.AppVersion) ? row.AppVersion : Trim(request.AppVersion, 80);
        if (request.ClockRollback)
            row.LastClockRollbackAt = now;
        return row;
    }

    static void NoteSnapshot(License license, Activation row, DongleSnapshot? snapshot)
    {
        if (snapshot == null)
            return;
        row.SnapshotEdition = Trim(snapshot.Edition, 64);
        row.SnapshotFeatures = JsonSerializer.Serialize(snapshot.Features ?? new List<string>());
        var serverFeatures = license.Features.Select(x => x.FeatureCode.Trim()).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var clientFeatures = (snapshot.Features ?? new List<string>()).Select(x => x.Trim()).Where(x => x.Length > 0).ToHashSet(StringComparer.OrdinalIgnoreCase);
        row.FeatureMismatch = !string.Equals(license.EditionCode.Trim(), (snapshot.Edition ?? "").Trim(), StringComparison.OrdinalIgnoreCase)
            || !serverFeatures.SetEquals(clientFeatures);
    }

    static void ExpireFloating(License license, DateTimeOffset now)
    {
        if (license.Kind != LicenseKind.Floating)
            return;
        foreach (var row in license.Activations.Where(a => a.ReleasedAt == null && !IsHolding(license, a, now)))
            Release(row, now, "HeartbeatTimeout");
    }

    static bool IsHolding(License license, Activation row, DateTimeOffset now)
    {
        if (row.ReleasedAt != null)
            return false;
        if (license.Kind != LicenseKind.Floating)
            return true;
        return row.LastHeartbeatAt.AddSeconds(license.FloatingReleaseSeconds) > now;
    }

    static async Task<bool> QuotaAllowsAsync(AppDbContext db, License license, DateTimeOffset now, CancellationToken cancellationToken)
    {
        if (license.RehostLimitPerYear <= 0)
            return false;
        var used = await db.RehostEvents.CountAsync(
            x => x.LicenseId == license.Id && x.Year == now.Year && x.Actor == RehostActor.Client,
            cancellationToken);
        return used < license.RehostLimitPerYear;
    }

    static void AddRehost(AppDbContext db, License license, string? from, string to, DateTimeOffset now, RehostActor actor)
    {
        db.RehostEvents.Add(new RehostEvent
        {
            Id = Guid.NewGuid(),
            LicenseId = license.Id,
            FromMachineId = from,
            ToMachineId = to,
            OccurredAt = now,
            Actor = actor,
            Year = now.Year
        });
    }

    public SignedGrant SignGrant(License license, string machineId, DateTimeOffset now, DateTimeOffset exactExpiry)
        => Sign(license, machineId, null, now, null, exactExpiry);

    DateTimeOffset? PresentedExpiry(LicenseCallRequest request, License license, string machineId)
    {
        if (string.IsNullOrWhiteSpace(request.CurrentCanonical) || string.IsNullOrWhiteSpace(request.CurrentSignature))
            return null;
        if (!_signing.Verify(request.CurrentCanonical, request.CurrentSignature))
            return null;
        try
        {
            var payload = GrantCanonical.Read(request.CurrentCanonical);
            if (!string.Equals(payload.LicenseId, license.Id.ToString("D"), StringComparison.Ordinal))
                return null;
            if (!string.Equals(payload.MachineId, machineId, StringComparison.Ordinal))
                return null;
            return GrantCanonical.ParseTimestamp(payload.ExpiresAt);
        }
        catch (Exception)
        {
            return null;
        }
    }

    SignedGrant Sign(License license, string machineId, string? dongleId, DateTimeOffset now, DateTimeOffset? notBefore, DateTimeOffset? exactExpiry)
    {
        DateTimeOffset exp;
        if (exactExpiry != null)
            exp = exactExpiry.Value;
        else
        {
            var graceEnd = now.AddSeconds(Math.Max(1, license.GraceSeconds));
            exp = notBefore != null && notBefore.Value > graceEnd ? notBefore.Value : graceEnd;
        }
        if (!license.IsPerpetual && license.SubscriptionEndsAt != null && exp > license.SubscriptionEndsAt.Value)
            exp = license.SubscriptionEndsAt.Value;
        var payload = new GrantPayload
        {
            Version = 1,
            KeyId = _signing.CurrentKid,
            LicenseId = license.Id.ToString("D"),
            ProductId = license.Product.PublicId,
            Kind = license.Kind.ToString(),
            Edition = license.EditionCode,
            Features = license.Features.Select(x => x.FeatureCode).ToList(),
            MachineId = machineId,
            DongleId = license.Kind == LicenseKind.Dongle ? license.DongleId : null,
            IssuedAt = GrantCanonical.FormatTimestamp(now),
            ExpiresAt = GrantCanonical.FormatTimestamp(exp),
            SubscriptionEndsAt = license.IsPerpetual || license.SubscriptionEndsAt == null ? null : GrantCanonical.FormatTimestamp(license.SubscriptionEndsAt.Value),
            Perpetual = license.IsPerpetual,
            OfflinePolicy = license.Kind == LicenseKind.Dongle ? "Dongle" : "Grace",
            HeartbeatSeconds = Math.Max(1, license.HeartbeatSeconds)
        };
        var canonical = GrantCanonical.Write(payload);
        return new SignedGrant { Canonical = canonical, Signature = _signing.Sign(canonical) };
    }

    static void Release(Activation row, DateTimeOffset now, string reason)
    {
        row.ReleasedAt = now;
        row.ReleaseReason = reason;
    }

    static LicenseCallResponse Ok(string status, bool english, DateTimeOffset now, SignedGrant? grant)
        => new() { Status = status, Message = ApiText.Get(english, status), ServerTime = now, Grant = grant };

    static LicenseCallResponse Fail(string status, string messageKey, bool english, DateTimeOffset now)
        => new() { Status = status, Message = ApiText.Get(english, messageKey), ServerTime = now };

    static DateTimeOffset Truncate(DateTimeOffset value)
        => new(value.Year, value.Month, value.Day, value.Hour, value.Minute, value.Second, TimeSpan.Zero);

    static string Trim(string? value, int length)
    {
        var text = (value ?? "").Trim();
        return text.Length <= length ? text : text.Substring(0, length);
    }
}
