using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Licensing.Client
{
    public sealed class PublicKeyRing
    {
        readonly Dictionary<string, RSAParameters> _keys = new Dictionary<string, RSAParameters>(StringComparer.Ordinal);

        public int Count => _keys.Count;

        public void Add(string kid, byte[] modulus, byte[] exponent)
        {
            if (string.IsNullOrWhiteSpace(kid))
                throw new ArgumentException("Missing key id.", nameof(kid));
            if (modulus == null || modulus.Length == 0)
                throw new ArgumentException("Missing modulus.", nameof(modulus));
            if (exponent == null || exponent.Length == 0)
                throw new ArgumentException("Missing exponent.", nameof(exponent));

            _keys[kid] = new RSAParameters { Modulus = modulus, Exponent = exponent };
        }

        public static PublicKeyRing Load(string path)
        {
            var json = File.ReadAllText(path, Encoding.UTF8);
            return Parse(json);
        }

        public static PublicKeyRing Parse(string json)
        {
            var file = JsonSerializer.Deserialize<PublicKeyFile>(json, Serializer.Options);
            var ring = new PublicKeyRing();
            if (file?.Keys == null)
                return ring;

            foreach (var entry in file.Keys)
            {
                if (entry == null || string.IsNullOrWhiteSpace(entry.Kid))
                    continue;
                ring.Add(entry.Kid, Convert.FromBase64String(entry.Modulus), Convert.FromBase64String(entry.Exponent));
            }

            return ring;
        }

        public bool Verify(string canonical, string signatureBase64)
        {
            if (string.IsNullOrEmpty(canonical) || string.IsNullOrEmpty(signatureBase64))
                return false;

            string kid;
            using (var document = JsonDocument.Parse(canonical))
                kid = document.RootElement.GetProperty("kid").GetString();

            if (string.IsNullOrEmpty(kid) || !_keys.TryGetValue(kid, out var parameters))
                return false;

            using (var rsa = RSA.Create())
            {
                rsa.ImportParameters(parameters);
                var data = Encoding.UTF8.GetBytes(canonical);
                var signature = Convert.FromBase64String(signatureBase64);
                return rsa.VerifyData(data, signature, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
            }
        }
    }

    static class Serializer
    {
        public static readonly JsonSerializerOptions Options = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };
    }
}
