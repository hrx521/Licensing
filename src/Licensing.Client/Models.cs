using System;
using System.Collections.Generic;

namespace Licensing.Client
{
    public sealed class DongleSnapshot
    {
        public string DongleId { get; set; }
        public string Edition { get; set; }
        public List<string> Features { get; set; }
    }

    public sealed class LicenseCallRequest
    {
        public string ProductId { get; set; }
        public string LicenseKey { get; set; }
        public string MachineId { get; set; }
        public string AppVersion { get; set; }
        public bool ClockRollback { get; set; }
        public DongleSnapshot Dongle { get; set; }
        public string CurrentCanonical { get; set; }
        public string CurrentSignature { get; set; }
    }

    public sealed class OfflineGrantFile
    {
        public string Canonical { get; set; }
        public string Signature { get; set; }
        public string LicenseKey { get; set; }
    }

    public sealed class SignedGrant
    {
        public string Canonical { get; set; }
        public string Signature { get; set; }
    }

    public sealed class LicenseCallResponse
    {
        public string Status { get; set; }
        public string Message { get; set; }
        public DateTimeOffset ServerTime { get; set; }
        public SignedGrant Grant { get; set; }
    }

    public sealed class GrantPayload
    {
        public int Version { get; set; }
        public string KeyId { get; set; }
        public string LicenseId { get; set; }
        public string ProductId { get; set; }
        public string Kind { get; set; }
        public string Edition { get; set; }
        public List<string> Features { get; set; }
        public string MachineId { get; set; }
        public string DongleId { get; set; }
        public string IssuedAt { get; set; }
        public string ExpiresAt { get; set; }
        public string SubscriptionEndsAt { get; set; }
        public bool Perpetual { get; set; }
        public string OfflinePolicy { get; set; }
        public int HeartbeatSeconds { get; set; }
    }

    public sealed class LicenseDecision
    {
        public string Status { get; set; }
        public string Message { get; set; }
        public bool FromCache { get; set; }
        public string Edition { get; set; }
        public IReadOnlyList<string> Features { get; set; }
        public DateTimeOffset? ExpiresAt { get; set; }
        public DateTimeOffset? SubscriptionEndsAt { get; set; }
        public bool Perpetual { get; set; }
        public bool ClockRollback { get; set; }
        public string Kind { get; set; }
        public int HeartbeatSeconds { get; set; }
    }

    public sealed class PublicKeyEntry
    {
        public string Kid { get; set; }
        public string Modulus { get; set; }
        public string Exponent { get; set; }
    }

    public sealed class PublicKeyFile
    {
        public List<PublicKeyEntry> Keys { get; set; }
    }

    public sealed class LocalState
    {
        public DateTimeOffset? LastSeenUtc { get; set; }
        public string DenialStatus { get; set; }
        public DateTimeOffset? DenialAt { get; set; }
        public string LicenseKey { get; set; }
    }
}
