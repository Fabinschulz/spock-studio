using Mariliz.Application.Execution;
using Mariliz.Application.Ports;

namespace Mariliz.Infrastructure.Integrations;

/// <summary>
/// Credenciais no serviço de segredos do Linux (o GNOME Keyring, o KWallet e outros que falam o protocolo Secret Service), pelo
/// comando <c>secret-tool</c> do libsecret. O valor vai pela entrada padrão do comando, nunca em argumento.
/// <para>
/// Num servidor ou numa sessão sem serviço de chaves o comando existe mas não tem com quem falar: o cofre diz que não está
/// disponível, e a tela explica o que instalar ou iniciar, em vez de gravar a credencial em texto puro.
/// </para>
/// </summary>
public sealed class SecretServiceVault(IProcessRunner runner) : CommandSecretVault(runner)
{
    public const string Executable = "secret-tool";

    public override string Name => "Serviço de segredos do Linux (libsecret)";

    protected override (bool Available, string? Hint) ProbeCore()
    {
        // "lookup" devolve 1 quando não há item, em silêncio; quando não há serviço de chaves, devolve 1 com uma mensagem de erro.
        var result = Run(Executable, ["lookup", "service", "spock-studio", "account", ProbeId]);
        if (result.NotFound)
            return (false, "Instale o pacote libsecret-tools (o comando secret-tool) e um serviço de chaves, como o GNOME Keyring ou o KWallet, e reinicie o Spock Studio.");
        if (result.TimedOut)
            return (false, "O serviço de chaves não respondeu no prazo. Desbloqueie o chaveiro da sessão e reinicie o Spock Studio.");
        if (result.ExitCode is not (0 or 1) || result.StandardError.Trim().Length > 0)
            return (false, "Não há um serviço de chaves ativo nesta sessão. Inicie o GNOME Keyring ou o KWallet (ou entre numa sessão gráfica) e reinicie o Spock Studio.");
        return (true, null);
    }

    protected override void StoreCore(string id, string encoded)
    {
        var result = Run(Executable, ["store", "--label", "Spock Studio", "service", "spock-studio", "account", id], encoded);
        if (result.NotFound || result.TimedOut || result.ExitCode != 0)
            throw new EngineException("CREDENTIAL_VAULT_STORE_FAILED", "Não foi possível guardar a credencial no serviço de segredos do Linux.");
    }

    protected override string? ReadCore(string id)
    {
        var result = Run(Executable, ["lookup", "service", "spock-studio", "account", id]);
        if (result.NotFound || result.TimedOut || result.ExitCode != 0) return null;
        var value = result.StandardOutput.Trim();
        return value.Length == 0 ? null : value;
    }

    protected override void RemoveCore(string id) =>
        Run(Executable, ["clear", "service", "spock-studio", "account", id]);
}
