namespace Mariliz.Infrastructure;

// R-10: o filho herda o ambiente do motor menos o que parece segredo, com a mesma regra do terminal do host
// (desktop/host/src-tauri/src/terminal/env.rs). Um script do projeto, um teste ou um pacote instalado não encontra a chave
// de uma ferramenta esquecida numa variável. Quem precisa de credencial usa o login da própria ferramenta; a exceção é
// nomeada por quem inicia o processo.
public static class ChildEnvironment
{
    public static readonly IReadOnlyList<string> SecretMarkers =
    [
        "TOKEN", "SECRET", "PASSWORD", "PASSWD", "API_KEY", "APIKEY", "PRIVATE_KEY", "CREDENTIAL", "ACCESS_KEY",
        "CLIENT_KEY", "SESSION_KEY", "CONNECTION_STRING",
        // O .NET lê `ConnectionStrings__Default` do ambiente, sem sublinhado entre as palavras.
        "CONNECTIONSTRING"
    ];

    public static bool IsSecret(string name)
    {
        var upper = name.ToUpperInvariant();
        return SecretMarkers.Any(upper.Contains);
    }

    /// <param name="environment">O ambiente que o filho herdaria, alterado no lugar.</param>
    /// <param name="inheritedSecrets">Variáveis com cara de segredo que este filho pode herdar mesmo assim.</param>
    /// <param name="explicitValues">Valores que o próprio motor define; valem sempre, porque não vêm do ambiente herdado.</param>
    public static void Apply(IDictionary<string, string?> environment, IReadOnlyCollection<string>? inheritedSecrets,
        IReadOnlyDictionary<string, string>? explicitValues)
    {
        var keep = new HashSet<string>(inheritedSecrets ?? [], StringComparer.OrdinalIgnoreCase);
        foreach (var name in environment.Keys.Where(name => IsSecret(name) && !keep.Contains(name)).ToList())
            environment.Remove(name);
        foreach (var (name, value) in explicitValues ?? new Dictionary<string, string>()) environment[name] = value;
    }
}
