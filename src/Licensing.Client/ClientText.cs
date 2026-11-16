namespace Licensing.Client
{
    public static class ClientText
    {
        public static bool IsEnglish(string language)
        {
            return !string.IsNullOrEmpty(language) && language.StartsWith("en", System.StringComparison.OrdinalIgnoreCase);
        }

        public static string ForStatus(string language, string status)
        {
            var english = IsEnglish(language);
            switch (status)
            {
                case LicenseStatuses.Active:
                    return english ? "License is valid." : "许可有效。";
                case LicenseStatuses.Released:
                    return english ? "This machine has been released." : "这台机器的绑定已释放。";
                case LicenseStatuses.NotFound:
                    return english ? "License was not found." : "没有这张许可。";
                case LicenseStatuses.Expired:
                    return english ? "License has expired." : "许可已到期。";
                case LicenseStatuses.Revoked:
                    return english ? "License has been revoked." : "许可已吊销。";
                case LicenseStatuses.Suspended:
                    return english ? "License is suspended." : "许可已暂停。";
                case LicenseStatuses.SeatLimit:
                    return english ? "No seat is available." : "没有空余席位。";
                case LicenseStatuses.RehostLimit:
                    return english ? "The yearly machine-change limit has been reached." : "已达到本年换机次数。";
                case LicenseStatuses.MachineInUse:
                    return english ? "The license is bound to another machine." : "许可绑定在另一台机器上。";
                case LicenseStatuses.DongleMismatch:
                    return english ? "The dongle does not match this license." : "加密锁和这张许可不一致。";
                case LicenseStatuses.InvalidRequest:
                    return english ? "The request is incomplete." : "请求不完整。";
                case LicenseStatuses.Unreachable:
                    return english ? "The license server could not be reached." : "连不上授权服务器。";
                case LicenseStatuses.InvalidSignature:
                    return english ? "The license signature is not valid." : "许可签名无效。";
                case LicenseStatuses.NoGrant:
                    return english ? "This computer has no saved license." : "这台电脑还没有保存的许可。";
                case LicenseStatuses.MachineMismatch:
                    return english ? "The saved license belongs to another machine." : "保存的许可属于另一台机器。";
                case LicenseStatuses.DongleMissing:
                    return english ? "The dongle snapshot is missing." : "没有加密锁快照。";
                case LicenseStatuses.ServerError:
                    return english ? "The license server failed." : "授权服务器出错。";
                default:
                    return english ? "License check failed." : "许可检查没有通过。";
            }
        }

        public static string DongleUnconfirmed(string language)
        {
            return IsEnglish(language)
                ? "The dongle is present. The server has not confirmed it on this computer."
                : "检测到加密锁。这台电脑尚未得到服务器确认。";
        }
    }
}
