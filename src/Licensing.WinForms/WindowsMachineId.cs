using System;
using Microsoft.Win32;

namespace Licensing.WinForms
{
    public static class WindowsMachineId
    {
        public static string Read()
        {
            var from64 = TryRead(RegistryView.Registry64);
            if (!string.IsNullOrWhiteSpace(from64))
                return from64.Trim();

            var fromDefault = TryRead(RegistryView.Default);
            if (!string.IsNullOrWhiteSpace(fromDefault))
                return fromDefault.Trim();

            throw new InvalidOperationException("读不到 MachineGuid。");
        }

        static string TryRead(RegistryView view)
        {
            try
            {
                using (var hive = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, view))
                using (var key = hive.OpenSubKey(@"SOFTWARE\Microsoft\Cryptography"))
                {
                    return key == null ? null : key.GetValue("MachineGuid") as string;
                }
            }
            catch (PlatformNotSupportedException)
            {
                return null;
            }
        }
    }
}
