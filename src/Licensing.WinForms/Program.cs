using System;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using System.Windows.Forms;
using Licensing.Client;

namespace Licensing.WinForms
{
    static class Program
    {
        [STAThread]
        static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            TestSettings settings;
            try
            {
                settings = TestSettings.Load();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "授权测试", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            using (var license = new SingleMachineLicense(
                settings.Server,
                settings.ProductId,
                settings.Keys,
                settings.Names,
                storeRoot: null,
                appVersion: "winforms-test"))
            {
                if (!LicenseForm.Ensure(null, license))
                    return;

                Application.Run(new TestShell(license, settings));
            }
        }
    }

    sealed class TestSettings
    {
        public string Server { get; private set; }
        public string ProductId { get; private set; }
        public string PublicKeyFile { get; private set; }
        public PublicKeyRing Keys { get; private set; }
        public LicenseNames Names { get; private set; }

        public static TestSettings Load()
        {
            var server = Setting("LicenseServer", "http://localhost:5277");
            var productId = Setting("ProductId", "demo.product");
            var publicKeyFile = FindPublicKeys(Setting("PublicKeyFile", ""));
            return new TestSettings
            {
                Server = server,
                ProductId = productId,
                PublicKeyFile = publicKeyFile,
                Keys = PublicKeyRing.Load(publicKeyFile),
                Names = new LicenseNames(ParseMap(Setting("EditionNames", "")), ParseMap(Setting("FeatureNames", "")))
            };
        }

        static string Setting(string name, string fallback)
        {
            var value = ConfigurationManager.AppSettings[name];
            return string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
        }

        static string FindPublicKeys(string configured)
        {
            if (!string.IsNullOrWhiteSpace(configured))
            {
                var full = Path.GetFullPath(configured);
                if (!File.Exists(full))
                    throw new FileNotFoundException("找不到公钥文件：" + full);
                return full;
            }

            var relatives = new[]
            {
                "public-keys.json",
                Path.Combine("storage", "public-keys.json"),
                Path.Combine("src", "Licensing.Server", "storage", "public-keys.json"),
                Path.Combine("..", "..", "..", "..", "Licensing.Server", "storage", "public-keys.json")
            };
            var roots = new[] { AppDomain.CurrentDomain.BaseDirectory, Directory.GetCurrentDirectory() };
            for (var r = 0; r < roots.Length; r++)
            {
                for (var i = 0; i < relatives.Length; i++)
                {
                    var path = Path.GetFullPath(Path.Combine(roots[r], relatives[i]));
                    if (File.Exists(path))
                        return path;
                }
            }

            throw new FileNotFoundException("找不到 public-keys.json。请在 App.config 的 PublicKeyFile 里填写路径。");
        }

        static Dictionary<string, string> ParseMap(string text)
        {
            var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (string.IsNullOrWhiteSpace(text))
                return map;

            var pairs = text.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries);
            for (var i = 0; i < pairs.Length; i++)
            {
                var pair = pairs[i];
                var split = pair.IndexOf('=');
                if (split <= 0 || split == pair.Length - 1)
                    continue;
                map[pair.Substring(0, split).Trim()] = pair.Substring(split + 1).Trim();
            }

            return map;
        }
    }
}
