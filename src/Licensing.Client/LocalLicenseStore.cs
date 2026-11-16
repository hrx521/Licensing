using System;
using System.IO;
using System.Text;
using System.Text.Json;

namespace Licensing.Client
{
    public sealed class LocalLicenseStore
    {
        readonly string _root;

        public LocalLicenseStore(string root)
        {
            _root = string.IsNullOrWhiteSpace(root)
                ? Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData)
                : root;
        }

        public string ProductDirectory(string productId)
        {
            return Path.Combine(_root, "Licensing", Safe(productId));
        }

        public SignedGrant LoadGrant(string productId)
        {
            var path = GrantPath(productId);
            if (!File.Exists(path))
                return null;

            var json = File.ReadAllText(path, Encoding.UTF8);
            return JsonSerializer.Deserialize<SignedGrant>(json, Serializer.Options);
        }

        public void SaveGrant(string productId, SignedGrant grant)
        {
            var directory = ProductDirectory(productId);
            Directory.CreateDirectory(directory);
            WriteAtomic(GrantPath(productId), JsonSerializer.Serialize(grant, Serializer.Options));
        }

        public void DeleteGrant(string productId)
        {
            var path = GrantPath(productId);
            if (File.Exists(path))
                File.Delete(path);
        }

        public LocalState LoadState(string productId)
        {
            var path = StatePath(productId);
            if (!File.Exists(path))
                return new LocalState();

            var json = File.ReadAllText(path, Encoding.UTF8);
            return JsonSerializer.Deserialize<LocalState>(json, Serializer.Options) ?? new LocalState();
        }

        public void SaveState(string productId, LocalState state)
        {
            var directory = ProductDirectory(productId);
            Directory.CreateDirectory(directory);
            WriteAtomic(StatePath(productId), JsonSerializer.Serialize(state, Serializer.Options));
        }

        string GrantPath(string productId) => Path.Combine(ProductDirectory(productId), "grant.json");

        string StatePath(string productId) => Path.Combine(ProductDirectory(productId), "state.json");

        static void WriteAtomic(string path, string contents)
        {
            var temporary = path + ".tmp";
            File.WriteAllText(temporary, contents, Encoding.UTF8);
            if (File.Exists(path))
                File.Delete(path);
            File.Move(temporary, path);
        }

        static string Safe(string productId)
        {
            if (string.IsNullOrWhiteSpace(productId))
                return "_";

            var invalid = Path.GetInvalidFileNameChars();
            var characters = productId.Trim().ToCharArray();
            for (var i = 0; i < characters.Length; i++)
            {
                if (Array.IndexOf(invalid, characters[i]) >= 0)
                    characters[i] = '_';
            }

            return new string(characters);
        }
    }
}
