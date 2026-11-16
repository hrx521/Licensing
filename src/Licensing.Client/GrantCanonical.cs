using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Text.Json;

namespace Licensing.Client
{
    public static class GrantCanonical
    {
        public static string FormatTimestamp(DateTimeOffset value)
        {
            return value.ToUniversalTime().ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture);
        }

        public static string Write(GrantPayload payload)
        {
            var features = payload.Features == null
                ? new List<string>()
                : new List<string>(payload.Features);
            features.Sort(StringComparer.Ordinal);

            var builder = new StringBuilder();
            var first = true;
            builder.Append('{');
            Number(builder, "v", payload.Version, ref first);
            Text(builder, "kid", payload.KeyId, ref first);
            Text(builder, "lic", payload.LicenseId, ref first);
            Text(builder, "prd", payload.ProductId, ref first);
            Text(builder, "kind", payload.Kind, ref first);
            Text(builder, "ed", payload.Edition ?? string.Empty, ref first);
            Raw(builder, "feat", FeatureArray(features), ref first);
            NullableText(builder, "mid", payload.MachineId, ref first);
            NullableText(builder, "did", payload.DongleId, ref first);
            Text(builder, "iat", payload.IssuedAt, ref first);
            Text(builder, "exp", payload.ExpiresAt, ref first);
            NullableText(builder, "subEnd", payload.SubscriptionEndsAt, ref first);
            Raw(builder, "perpetual", payload.Perpetual ? "true" : "false", ref first);
            Text(builder, "offlinePolicy", payload.OfflinePolicy, ref first);
            Number(builder, "hbSeconds", payload.HeartbeatSeconds, ref first);
            builder.Append('}');
            return builder.ToString();
        }

        public static GrantPayload Read(string canonical)
        {
            using (var document = JsonDocument.Parse(canonical))
            {
                var root = document.RootElement;
                var features = new List<string>();
                foreach (var item in root.GetProperty("feat").EnumerateArray())
                    features.Add(item.GetString());

                return new GrantPayload
                {
                    Version = root.GetProperty("v").GetInt32(),
                    KeyId = root.GetProperty("kid").GetString(),
                    LicenseId = root.GetProperty("lic").GetString(),
                    ProductId = root.GetProperty("prd").GetString(),
                    Kind = root.GetProperty("kind").GetString(),
                    Edition = root.GetProperty("ed").GetString(),
                    Features = features,
                    MachineId = ReadNullable(root, "mid"),
                    DongleId = ReadNullable(root, "did"),
                    IssuedAt = root.GetProperty("iat").GetString(),
                    ExpiresAt = root.GetProperty("exp").GetString(),
                    SubscriptionEndsAt = ReadNullable(root, "subEnd"),
                    Perpetual = root.GetProperty("perpetual").GetBoolean(),
                    OfflinePolicy = root.GetProperty("offlinePolicy").GetString(),
                    HeartbeatSeconds = root.GetProperty("hbSeconds").GetInt32()
                };
            }
        }

        public static DateTimeOffset ParseTimestamp(string value)
        {
            return DateTimeOffset.ParseExact(
                value,
                "yyyy-MM-dd'T'HH:mm:ss'Z'",
                CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal);
        }

        static string ReadNullable(JsonElement root, string name)
        {
            var value = root.GetProperty(name);
            return value.ValueKind == JsonValueKind.Null ? null : value.GetString();
        }

        static string FeatureArray(List<string> features)
        {
            var builder = new StringBuilder();
            builder.Append('[');
            for (var i = 0; i < features.Count; i++)
            {
                if (i > 0)
                    builder.Append(',');
                builder.Append(JsonSerializer.Serialize(features[i] ?? string.Empty));
            }

            builder.Append(']');
            return builder.ToString();
        }

        static void Number(StringBuilder builder, string name, int value, ref bool first)
        {
            Raw(builder, name, value.ToString(CultureInfo.InvariantCulture), ref first);
        }

        static void Text(StringBuilder builder, string name, string value, ref bool first)
        {
            Raw(builder, name, JsonSerializer.Serialize(value ?? string.Empty), ref first);
        }

        static void NullableText(StringBuilder builder, string name, string value, ref bool first)
        {
            Raw(builder, name, value == null ? "null" : JsonSerializer.Serialize(value), ref first);
        }

        static void Raw(StringBuilder builder, string name, string jsonValue, ref bool first)
        {
            if (!first)
                builder.Append(',');
            first = false;
            builder.Append('"').Append(name).Append('"').Append(':').Append(jsonValue);
        }
    }
}
