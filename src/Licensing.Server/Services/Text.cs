namespace Licensing.Server.Services;

public static class ApiText
{
    public static bool English(HttpRequest request)
    {
        var header = request.Headers.AcceptLanguage.ToString();
        return header.StartsWith("en", StringComparison.OrdinalIgnoreCase);
    }

    public static string Get(bool english, string key)
    {
        return (english, key) switch
        {
            (false, "Active") => "许可有效。",
            (true, "Active") => "License is valid.",
            (false, "Released") => "绑定已释放。",
            (true, "Released") => "The binding has been released.",
            (false, "NotFound") => "没有这张许可。",
            (true, "NotFound") => "License was not found.",
            (false, "Expired") => "许可已到期。",
            (true, "Expired") => "License has expired.",
            (false, "Revoked") => "许可已吊销。",
            (true, "Revoked") => "License has been revoked.",
            (false, "Suspended") => "许可已暂停。",
            (true, "Suspended") => "License is suspended.",
            (false, "SeatLimit") => "没有空余席位。",
            (true, "SeatLimit") => "No seat is available.",
            (false, "RehostLimit") => "已达到本年换机次数。",
            (true, "RehostLimit") => "The yearly machine-change limit has been reached.",
            (false, "MachineInUse") => "许可绑定在另一台机器上。",
            (true, "MachineInUse") => "The license is bound to another machine.",
            (false, "DongleMismatch") => "加密锁和这张许可不一致。",
            (true, "DongleMismatch") => "The dongle does not match this license.",
            (false, "MissingProduct") => "缺少产品标识。",
            (true, "MissingProduct") => "Product id is required.",
            (false, "MissingMachine") => "缺少机器码。",
            (true, "MissingMachine") => "Machine id is required.",
            (false, "MissingCredential") => "需要许可码或锁号。",
            (true, "MissingCredential") => "A license key or dongle id is required.",
            (false, "MissingDongle") => "这种许可需要加密锁快照。",
            (true, "MissingDongle") => "This license requires a dongle snapshot.",
            (false, "AmbiguousDongle") => "这个锁号对应了多张许可。",
            (true, "AmbiguousDongle") => "More than one license uses this dongle id.",
            (false, "RehostNotApplicable") => "这种许可不用换机接口。",
            (true, "RehostNotApplicable") => "This license kind does not use rehost.",
            (false, "ServerError") => "服务器出错。",
            (true, "ServerError") => "Server error.",
            (false, _) => "请求没有完成。",
            (true, _) => "The request was not completed."
        };
    }
}

public static class Ui
{
    public static string T(string zh, string en)
        => System.Globalization.CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "en" ? en : zh;

    public static string Kind(Data.LicenseKind kind) => kind switch
    {
        Data.LicenseKind.SingleMachine => T("单机", "Single machine"),
        Data.LicenseKind.MachinePool => T("机器池", "Machine pool"),
        Data.LicenseKind.Floating => T("浮动", "Floating"),
        Data.LicenseKind.Dongle => T("加密锁", "Dongle"),
        Data.LicenseKind.LegacyToken => T("导入老码", "Imported code"),
        _ => kind.ToString()
    };

    public static string State(Data.LicenseState state) => state switch
    {
        Data.LicenseState.Active => T("有效", "Active"),
        Data.LicenseState.Suspended => T("暂停", "Suspended"),
        Data.LicenseState.Revoked => T("吊销", "Revoked"),
        _ => state.ToString()
    };

    public static string Action(string action) => action switch
    {
        "LoginSuccess" => T("登录成功", "Login succeeded"),
        "LoginFailed" => T("登录失败", "Login failed"),
        "CustomerCreate" => T("新建客户", "Create customer"),
        "CustomerUpdate" => T("修改客户", "Update customer"),
        "LicenseIssue" => T("签发许可", "Issue license"),
        "LicenseUpdate" => T("修改许可", "Update license"),
        "LicenseSuspend" => T("暂停许可", "Suspend license"),
        "LicenseResume" => T("恢复许可", "Resume license"),
        "LicenseRevoke" => T("吊销许可", "Revoke license"),
        "Import" => T("导入老码", "Import code"),
        "ForceRelease" => T("强制释放", "Force release"),
        "AdminRehost" => T("后台换机", "Admin machine change"),
        "OfflinePresign" => T("预签离线授权", "Presign offline grant"),
        "StaffCreate" => T("新建账号", "Create account"),
        "StaffPassword" => T("重置密码", "Reset password"),
        "StaffAssign" => T("分配客户", "Assign customers"),
        "KeyRotate" => T("轮换签名密钥", "Rotate signing key"),
        _ => action
    };
}
