using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;

if (args.Length == 0 || !args[0].Equals("register-project", StringComparison.OrdinalIgnoreCase))
    return 2;

string? Get(string name)
{
    var i = Array.FindIndex(args, x => x.Equals(name, StringComparison.OrdinalIgnoreCase));
    return i >= 0 && i + 1 < args.Length ? args[i + 1] : null;
}

try
{
    var domain = (Get("--domain") ?? "").Trim().ToLowerInvariant();
    var sslDir = Get("--ssl-dir") ?? "";
    var https = bool.TryParse(Get("--https"), out var enabled) && enabled;
    if (string.IsNullOrWhiteSpace(domain) || domain.Any(c => !(char.IsLetterOrDigit(c) || c is '.' or '-')))
        throw new InvalidOperationException("Некорректный локальный домен.");

    RegisterHosts(domain);
    if (https) CreateAndTrustCertificate(domain, sslDir);
    return 0;
}
catch (Exception ex)
{
    Console.Error.WriteLine(ex.Message);
    return 1;
}

static void RegisterHosts(string domain)
{
    var hosts = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "drivers", "etc", "hosts");
    var lines = File.Exists(hosts) ? File.ReadAllLines(hosts).ToList() : new List<string>();
    var marker = $"# STAVCMS:{domain}";
    if (lines.Any(l => l.Contains(marker, StringComparison.OrdinalIgnoreCase))) return;

    lines.Add($"127.0.0.1\t{domain}\t{marker}");
    File.WriteAllLines(hosts, lines, new UTF8Encoding(false));
}

static void CreateAndTrustCertificate(string domain, string sslDir)
{
    Directory.CreateDirectory(sslDir);
    var certPath = Path.Combine(sslDir, domain + ".crt.pem");
    var keyPath = Path.Combine(sslDir, domain + ".key.pem");

    if (!File.Exists(certPath) || !File.Exists(keyPath))
    {
        using var rsa = RSA.Create(2048);
        var request = new CertificateRequest($"CN={domain}", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        var san = new SubjectAlternativeNameBuilder();
        san.AddDnsName(domain);
        san.AddDnsName("localhost");
        request.CertificateExtensions.Add(san.Build());
        request.CertificateExtensions.Add(new X509BasicConstraintsExtension(false, false, 0, true));
        request.CertificateExtensions.Add(new X509KeyUsageExtension(X509KeyUsageFlags.DigitalSignature | X509KeyUsageFlags.KeyEncipherment, true));
        request.CertificateExtensions.Add(new X509EnhancedKeyUsageExtension(new OidCollection { new Oid("1.3.6.1.5.5.7.3.1") }, true));

        using var cert = request.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddYears(2));
        File.WriteAllText(certPath, cert.ExportCertificatePem(), new UTF8Encoding(false));
        File.WriteAllText(keyPath, rsa.ExportPkcs8PrivateKeyPem(), new UTF8Encoding(false));

        using var root = new X509Store(StoreName.Root, StoreLocation.LocalMachine);
        root.Open(OpenFlags.ReadWrite);
        root.Add(new X509Certificate2(cert.Export(X509ContentType.Cert)));
    }
    else
    {
        using var existing = X509Certificate2.CreateFromPemFile(certPath);
        using var root = new X509Store(StoreName.Root, StoreLocation.LocalMachine);
        root.Open(OpenFlags.ReadWrite);
        if (!root.Certificates.Any(c => c.Thumbprint == existing.Thumbprint)) root.Add(existing);
    }
}
