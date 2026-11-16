using System;
using System.Text;

namespace Licensing.Client
{
    public static class KeyNormalize
    {
        public static string Normalize(string key)
        {
            if (string.IsNullOrWhiteSpace(key))
                return string.Empty;

            var builder = new StringBuilder(key.Length);
            foreach (var character in key.Trim())
            {
                if (char.IsWhiteSpace(character) || character == '-' || character == '\u2010' || character == '\u2011' || character == '\u2013' || character == '\u2014')
                    continue;
                builder.Append(char.ToUpperInvariant(character));
            }

            return builder.ToString();
        }

        public static string FormatGrouped(string normalized)
        {
            if (string.IsNullOrEmpty(normalized) || normalized.Length != 25)
                return normalized;

            return normalized.Substring(0, 5) + "-"
                + normalized.Substring(5, 5) + "-"
                + normalized.Substring(10, 5) + "-"
                + normalized.Substring(15, 5) + "-"
                + normalized.Substring(20, 5);
        }
    }
}
