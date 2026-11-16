namespace Licensing.Server.Data;

public sealed class AppUser : Microsoft.AspNetCore.Identity.IdentityUser
{
    public bool IsSuperAdmin { get; set; }
    public string DisplayName { get; set; } = "";
}

public enum LicenseKind
{
    SingleMachine,
    MachinePool,
    Floating,
    Dongle,
    LegacyToken
}

public enum LicenseState
{
    Active,
    Suspended,
    Revoked
}

public enum RehostActor
{
    Client,
    Admin
}

public sealed class Customer
{
    public Guid Id { get; set; }
    public string Name { get; set; } = "";
    public string? Email { get; set; }
    public string? Company { get; set; }
    public string? Phone { get; set; }
    public string? Notes { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public List<License> Licenses { get; set; } = new();
}

public sealed class Product
{
    public Guid Id { get; set; }
    public string PublicId { get; set; } = "";
    public string Name { get; set; } = "";
    public List<Edition> Editions { get; set; } = new();
    public List<FeatureDefinition> Features { get; set; } = new();
}

public sealed class Edition
{
    public Guid Id { get; set; }
    public Guid ProductId { get; set; }
    public Product Product { get; set; } = null!;
    public string Code { get; set; } = "";
    public string Name { get; set; } = "";
}

public sealed class FeatureDefinition
{
    public Guid Id { get; set; }
    public Guid ProductId { get; set; }
    public Product Product { get; set; } = null!;
    public string Code { get; set; } = "";
    public string Name { get; set; } = "";
}

public sealed class License
{
    public Guid Id { get; set; }
    public Guid CustomerId { get; set; }
    public Customer Customer { get; set; } = null!;
    public Guid ProductId { get; set; }
    public Product Product { get; set; } = null!;
    public string KeyDisplay { get; set; } = "";
    public string KeyNormalized { get; set; } = "";
    public LicenseKind Kind { get; set; }
    public string EditionCode { get; set; } = "";
    public int SeatLimit { get; set; }
    public string? PersonLabel { get; set; }
    public bool IsPerpetual { get; set; }
    public DateTimeOffset? SubscriptionEndsAt { get; set; }
    public int GraceSeconds { get; set; }
    public int HeartbeatSeconds { get; set; }
    public int FloatingReleaseSeconds { get; set; }
    public int RehostLimitPerYear { get; set; }
    public LicenseState Status { get; set; }
    public string? DongleId { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public List<LicenseFeature> Features { get; set; } = new();
    public List<Activation> Activations { get; set; } = new();
}

public sealed class LicenseFeature
{
    public Guid LicenseId { get; set; }
    public License License { get; set; } = null!;
    public string FeatureCode { get; set; } = "";
}

public sealed class Activation
{
    public Guid Id { get; set; }
    public Guid LicenseId { get; set; }
    public License License { get; set; } = null!;
    public string MachineId { get; set; } = "";
    public string? DongleId { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset LastHeartbeatAt { get; set; }
    public DateTimeOffset? LastClockRollbackAt { get; set; }
    public string? AppVersion { get; set; }
    public DateTimeOffset? ReleasedAt { get; set; }
    public string? ReleaseReason { get; set; }
    public bool FeatureMismatch { get; set; }
    public string? SnapshotEdition { get; set; }
    public string? SnapshotFeatures { get; set; }
}

public sealed class RehostEvent
{
    public Guid Id { get; set; }
    public Guid LicenseId { get; set; }
    public License License { get; set; } = null!;
    public string? FromMachineId { get; set; }
    public string ToMachineId { get; set; } = "";
    public DateTimeOffset OccurredAt { get; set; }
    public RehostActor Actor { get; set; }
    public int Year { get; set; }
}

public sealed class StaffCustomer
{
    public string UserId { get; set; } = "";
    public Guid CustomerId { get; set; }
    public Customer Customer { get; set; } = null!;
}

public sealed class SigningKey
{
    public string Kid { get; set; } = "";
    public string Modulus { get; set; } = "";
    public string Exponent { get; set; } = "";
    public DateTimeOffset CreatedAt { get; set; }
    public bool IsCurrent { get; set; }
}

public sealed class AuditLog
{
    public Guid Id { get; set; }
    public DateTimeOffset At { get; set; }
    public string? ActorUserId { get; set; }
    public string Action { get; set; } = "";
    public Guid? CustomerId { get; set; }
    public Guid? LicenseId { get; set; }
    public string? Detail { get; set; }
    public string? Ip { get; set; }
}
