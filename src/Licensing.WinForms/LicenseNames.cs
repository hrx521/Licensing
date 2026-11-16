using System;
using System.Collections.Generic;

namespace Licensing.WinForms
{
    public sealed class LicenseNames
    {
        readonly Dictionary<string, string> _editions;
        readonly Dictionary<string, string> _features;

        public LicenseNames()
            : this(null, null)
        {
        }

        public LicenseNames(IDictionary<string, string> editions, IDictionary<string, string> features)
        {
            _editions = Copy(editions);
            _features = Copy(features);
        }

        public string Edition(string code)
        {
            return Lookup(_editions, code);
        }

        public string Features(IReadOnlyList<string> codes)
        {
            if (codes == null || codes.Count == 0)
                return "";

            var names = new List<string>();
            for (var i = 0; i < codes.Count; i++)
            {
                var name = Lookup(_features, codes[i]);
                if (name.Length > 0)
                    names.Add(name);
            }

            return string.Join("、", names);
        }

        static string Lookup(Dictionary<string, string> map, string code)
        {
            if (string.IsNullOrWhiteSpace(code))
                return "";

            var trimmed = code.Trim();
            string name;
            if (map.TryGetValue(trimmed, out name) && !string.IsNullOrWhiteSpace(name))
                return name.Trim();
            return trimmed;
        }

        static Dictionary<string, string> Copy(IDictionary<string, string> source)
        {
            var copy = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (source == null)
                return copy;

            foreach (var pair in source)
            {
                if (!string.IsNullOrWhiteSpace(pair.Key) && !string.IsNullOrWhiteSpace(pair.Value))
                    copy[pair.Key.Trim()] = pair.Value.Trim();
            }

            return copy;
        }
    }
}
