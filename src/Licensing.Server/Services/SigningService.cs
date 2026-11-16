using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text.Json;
using Licensing.Client;

namespace Licensing.Server.Services;

public sealed class SigningService
{
    readonly IConfiguration _config;
    readonly IWebHostEnvironment _environment;
    readonly object _gate = new();
    RSA? _rsa;
    PublicKeyRing _publicKeys = new();
    string? _password;
    string _dataDirectory = "";

    public SigningService(IConfiguration config, IWebHostEnvironment environment)
    {
        _config = config;
        _environment = environment;
    }

    public string CurrentKid { get; private set; } = "";
    public string PublicKeyPath => Path.Combine(_dataDirectory, "public-keys.json");

    public void Initialize(Data.AppDbContext db)
    {
        _dataDirectory = Path.Combine(_environment.ContentRootPath, "storage");
        Directory.CreateDirectory(Path.Combine(_dataDirectory, "keys"));
        _password = ResolvePassword();
        if (!db.SigningKeys.Any())
            CreateKey(db);
        else
            LoadCurrent(db);
        WritePublicFile(db);
    }

    public void Rotate(Data.AppDbContext db)
    {
        CreateKey(db);
        WritePublicFile(db);
    }

    public bool Verify(string canonical, string signature)
    {
        lock (_gate)
            return _publicKeys.Verify(canonical, signature);
    }

    public string Sign(string canonical)
    {
        lock (_gate)
        {
            if (_rsa == null)
                throw new InvalidOperationException("Signing key is not loaded.");
            return Convert.ToBase64String(_rsa.SignData(
                System.Text.Encoding.UTF8.GetBytes(canonical),
                HashAlgorithmName.SHA256,
                RSASignaturePadding.Pkcs1));
        }
    }

    void CreateKey(Data.AppDbContext db)
    {
        var kid = "k" + DateTimeOffset.UtcNow.ToString("yyyyMMddHHmmss");
        var rsa = RSA.Create(2048);
        var request = new CertificateRequest("CN=Licensing Signing " + kid, rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        var certificate = request.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddYears(30));
        File.WriteAllBytes(Path.Combine(_dataDirectory, "keys", kid + ".pfx"), certificate.Export(X509ContentType.Pkcs12, _password));
        var publicKey = rsa.ExportParameters(false);
        foreach (var existing in db.SigningKeys)
            existing.IsCurrent = false;
        db.SigningKeys.Add(new Data.SigningKey
        {
            Kid = kid,
            Modulus = Convert.ToBase64String(publicKey.Modulus!),
            Exponent = Convert.ToBase64String(publicKey.Exponent!),
            CreatedAt = DateTimeOffset.UtcNow,
            IsCurrent = true
        });
        db.SaveChanges();
        lock (_gate)
        {
            _rsa?.Dispose();
            _rsa = rsa;
            CurrentKid = kid;
        }
    }

    void LoadCurrent(Data.AppDbContext db)
    {
        var current = db.SigningKeys.Single(x => x.IsCurrent);
        var certificate = X509CertificateLoader.LoadPkcs12FromFile(
            Path.Combine(_dataDirectory, "keys", current.Kid + ".pfx"),
            _password,
            X509KeyStorageFlags.Exportable);
        lock (_gate)
        {
            _rsa?.Dispose();
            _rsa = certificate.GetRSAPrivateKey() ?? throw new InvalidOperationException("PFX has no RSA key.");
            CurrentKid = current.Kid;
        }
    }

    void WritePublicFile(Data.AppDbContext db)
    {
        var keys = db.SigningKeys.AsEnumerable().OrderBy(x => x.CreatedAt).Select(x => new PublicKeyEntry
        {
            Kid = x.Kid,
            Modulus = x.Modulus,
            Exponent = x.Exponent
        }).ToList();
        var file = new PublicKeyFile { Keys = keys };
        File.WriteAllText(PublicKeyPath, JsonSerializer.Serialize(file, new JsonSerializerOptions { WriteIndented = true }));
        var ring = new PublicKeyRing();
        foreach (var key in keys)
            ring.Add(key.Kid, Convert.FromBase64String(key.Modulus), Convert.FromBase64String(key.Exponent));
        lock (_gate)
            _publicKeys = ring;
    }

    string ResolvePassword()
    {
        var configured = _config["Signing:PfxPassword"];
        var file = Path.Combine(_dataDirectory, "signing-password.txt");
        if (!string.IsNullOrWhiteSpace(configured))
            return configured;
        if (File.Exists(file))
            return File.ReadAllText(file).Trim();
        var generated = Convert.ToBase64String(RandomNumberGenerator.GetBytes(24));
        File.WriteAllText(file, generated);
        return generated;
    }
}
