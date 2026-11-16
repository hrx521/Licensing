using System.Text;
using Licensing.Client;

var command = args.Length > 0 ? args[0] : "help";
var language = Value(args, "--lang") ?? "zh-CN";
var english = ClientText.IsEnglish(language);
if (command is "help" or "--help" or "-h")
{
    Console.WriteLine(english
        ? "Usage: Licensing.Sample <activate|heartbeat|deactivate|rehost|check|import|machine> --server URL --product ID --key KEY [--file GRANT] [--public-key FILE] [--dongle-id ID] [--dongle-edition CODE] [--dongle-features A,B] [--app-version TEXT] [--lang zh-CN|en]"
        : "用法: Licensing.Sample <activate|heartbeat|deactivate|rehost|check|import|machine> --server 地址 --product 产品标识 --key 许可码 [--file 授权文件] [--public-key 公钥文件] [--dongle-id 锁号] [--dongle-edition 版本] [--dongle-features A,B] [--app-version 版本] [--lang zh-CN|en]");
    return 0;
}

if (command == "machine")
{
    Console.WriteLine((english ? "Machine id: " : "机器码: ") + ReadMachineId());
    return 0;
}

var server = Value(args, "--server") ?? "http://localhost:5277";
var product = Value(args, "--product");
var key = Value(args, "--key");
var grantFile = Value(args, "--file");
if (command == "import")
{
    if (string.IsNullOrWhiteSpace(product) || string.IsNullOrWhiteSpace(grantFile))
    {
        Console.Error.WriteLine(english ? "Product id and --file are required." : "导入需要产品标识和 --file。");
        return 2;
    }
}
else if (string.IsNullOrWhiteSpace(product) || (string.IsNullOrWhiteSpace(key) && string.IsNullOrWhiteSpace(Value(args, "--dongle-id"))))
{
    Console.Error.WriteLine(english ? "Product id and a license key or dongle id are required." : "需要产品标识，以及许可码或锁号。");
    return 2;
}

var machine = ReadMachineId();
Console.WriteLine((english ? "Machine id: " : "机器码: ") + machine);

PublicKeyRing keys;
try
{
    keys = PublicKeyRing.Load(FindPublicKeys(Value(args, "--public-key")));
}
catch (Exception ex)
{
    Console.Error.WriteLine((english ? "Could not read the public key file: " : "读不到公钥文件: ") + ex.Message);
    return 2;
}

var request = new LicenseCallRequest
{
    ProductId = product,
    LicenseKey = key,
    MachineId = machine,
    AppVersion = Value(args, "--app-version"),
    Dongle = BuildDongle(args)
};

using var client = new LicenseClient(server, keys, language);
LicenseDecision? decision;
if (command == "import")
{
    string fileJson;
    try
    {
        fileJson = File.ReadAllText(grantFile!);
    }
    catch (Exception ex)
    {
        Console.Error.WriteLine((english ? "Could not read the grant file: " : "读不到授权文件: ") + ex.Message);
        return 2;
    }
    decision = client.ImportOfflineFile(request, fileJson);
}
else
{
    decision = command switch
    {
        "activate" => await client.ActivateAsync(request),
        "heartbeat" => await client.HeartbeatAsync(request),
        "deactivate" => await client.DeactivateAsync(request),
        "rehost" => await client.RehostAsync(request),
        "check" => client.CheckOffline(request),
        _ => null
    };
}

if (decision == null)
{
    Console.Error.WriteLine(english ? "Unknown command." : "未知命令。");
    return 2;
}

if (decision.Status is LicenseStatuses.Unreachable or LicenseStatuses.SeatLimit)
{
    var offline = client.CheckOffline(request);
    if (offline.Status == LicenseStatuses.Active)
    {
        Print(offline, english);
        Console.WriteLine(english
            ? "The server did not refresh the seat. The cached license remains valid until " + offline.ExpiresAt?.ToUniversalTime().ToString("yyyy-MM-dd HH:mm") + " UTC."
            : "服务器没有续上席位。本机缓存仍可用到 " + offline.ExpiresAt?.ToUniversalTime().ToString("yyyy-MM-dd HH:mm") + " UTC。");
        return 0;
    }
}

Print(decision, english);
return decision.Status == LicenseStatuses.Active || decision.Status == LicenseStatuses.Released ? 0 : 1;

static void Print(LicenseDecision decision, bool english)
{
    Console.WriteLine((english ? "Status: " : "状态: ") + decision.Status);
    Console.WriteLine(decision.Message);
    if (!string.IsNullOrEmpty(decision.Kind))
        Console.WriteLine((english ? "Kind: " : "种类: ") + decision.Kind);
    if (!string.IsNullOrEmpty(decision.Edition))
        Console.WriteLine((english ? "Edition: " : "版本: ") + decision.Edition);
    if (decision.Features != null && decision.Features.Count > 0)
        Console.WriteLine((english ? "Features: " : "功能: ") + string.Join(", ", decision.Features));
    if (decision.ExpiresAt != null)
        Console.WriteLine((english ? "Valid until UTC: " : "有效至 UTC: ") + decision.ExpiresAt.Value.ToUniversalTime().ToString("yyyy-MM-dd HH:mm:ss"));
    if (decision.ClockRollback)
        Console.WriteLine(english ? "Clock rollback was observed." : "观察到系统时间回拨。");
}

static DongleSnapshot? BuildDongle(string[] args)
{
    var id = Value(args, "--dongle-id");
    if (string.IsNullOrWhiteSpace(id))
        return null;
    var features = Value(args, "--dongle-features");
    return new DongleSnapshot
    {
        DongleId = id,
        Edition = Value(args, "--dongle-edition"),
        Features = string.IsNullOrWhiteSpace(features)
            ? new List<string>()
            : features.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList()
    };
}

static string ReadMachineId()
{
    if (OperatingSystem.IsWindows())
    {
        using var key = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Cryptography");
        var value = key?.GetValue("MachineGuid") as string;
        if (!string.IsNullOrWhiteSpace(value))
            return value.Trim();
    }

    if (OperatingSystem.IsLinux() && File.Exists("/etc/machine-id"))
    {
        var value = File.ReadAllText("/etc/machine-id", Encoding.UTF8).Trim();
        if (value.Length > 0)
            return value;
    }

    throw new InvalidOperationException("Machine id was not found. Windows uses MachineGuid; Linux uses /etc/machine-id.");
}

static string FindPublicKeys(string? explicitPath)
{
    if (!string.IsNullOrWhiteSpace(explicitPath))
        return explicitPath;

    var candidates = new[]
    {
        Path.Combine("storage", "public-keys.json"),
        Path.Combine("src", "Licensing.Server", "storage", "public-keys.json"),
        Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "Licensing.Server", "storage", "public-keys.json"))
    };
    foreach (var candidate in candidates)
    {
        if (File.Exists(candidate))
            return candidate;
    }

    throw new FileNotFoundException("public-keys.json was not found. Pass --public-key.");
}

static string? Value(string[] args, string name)
{
    for (var i = 0; i < args.Length - 1; i++)
    {
        if (string.Equals(args[i], name, StringComparison.OrdinalIgnoreCase))
            return args[i + 1];
    }

    return null;
}
