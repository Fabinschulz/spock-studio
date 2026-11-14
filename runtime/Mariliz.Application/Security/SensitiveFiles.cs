using Mariliz.Core.Policy;

namespace Mariliz.Application.Security;

// Arquivos que guardam segredo pelo que são, não pelo conteúdo: o scanner nem abre, só confere se o .gitignore os protege.
internal static class SensitiveFiles
{
    private static readonly IgnorePatterns Credentials = IgnorePatterns.Parse(
    [
        ".env", ".env.*", "*.key", "*.p12", "*.pfx", "*.jks", "*.keystore", "id_rsa", "id_dsa", "id_ecdsa", "id_ed25519",
        "credentials.json", "service-account*.json", "*.tfstate", "*.tfstate.*"
    ]);

    // Certificado público também usa .pem; pode ser só a cadeia, então pesa menos.
    private static readonly IgnorePatterns Certificates = IgnorePatterns.Parse(["*.pem"]);

    private static readonly string[] TemplateSuffixes = [".example", ".sample", ".template", ".dist", ".pub"];

    public static string? Severity(string path)
    {
        var name = path.Split('/')[^1];
        if (TemplateSuffixes.Any(suffix => name.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))) return null;
        if (Credentials.Matches(path)) return FindingSeverities.High;
        return Certificates.Matches(path) ? FindingSeverities.Medium : null;
    }

    // Ancorado na raiz: protege este arquivo sem esconder outro de mesmo nome em outra pasta.
    public static string IgnoreEntry(string path) => "/" + path;
}
