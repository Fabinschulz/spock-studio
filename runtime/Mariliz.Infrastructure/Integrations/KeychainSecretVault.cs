using System.Text;
using Mariliz.Application.Execution;
using Mariliz.Application.Ports;

namespace Mariliz.Infrastructure.Integrations;

// Caminho completo do security para um programa de mesmo nome em outra pasta do PATH não tomar o lugar dele.
// O security só aceita a senha como argumento de -w, visível a qualquer usuário; por isso a gravação usa o modo interativo (security -i) pela entrada padrão. Esse modo não devolve erro quando um comando falha, então CommandSecretVault.Store confere lendo de volta.
public sealed class KeychainSecretVault(IProcessRunner runner) : CommandSecretVault(runner)
{
    public const string Executable = "/usr/bin/security";

    /// <summary><c>find-generic-password</c> devolve 44 quando o item não existe: o chaveiro respondeu, só não tem aquele item.</summary>
    private const int ItemNotFound = 44;

    public override string Name => "Chaveiro do macOS";

    protected override (bool Available, string? Hint) ProbeCore()
    {
        var result = Run(Executable, ["find-generic-password", "-s", Service, "-a", ProbeId]);
        if (result.NotFound)
            return (false, "O comando security do macOS não foi encontrado em /usr/bin. Ele faz parte do sistema: reinstale as ferramentas de linha de comando ou reinicie o Spock Studio.");
        if (result.TimedOut)
            return (false, "O Chaveiro do macOS não respondeu no prazo. Desbloqueie o chaveiro de início de sessão e reinicie o Spock Studio.");
        return result.ExitCode is 0 or ItemNotFound
            ? (true, null)
            : (false, $"O Chaveiro do macOS recusou o acesso (código {result.ExitCode}). Desbloqueie o chaveiro de início de sessão, permita o acesso e reinicie o Spock Studio.");
    }

    protected override void StoreCore(string id, string encoded)
    {
        // As aspas protegem o espaço do nome do serviço; o resto só tem letras, números, hífen e os caracteres do base64.
        var command = $"add-generic-password -U -s \"{Service}\" -a \"{id}\" -w \"{encoded}\"\n";
        var result = Run(Executable, ["-i"], command);
        if (result.NotFound || result.TimedOut || result.ExitCode != 0)
            throw new EngineException("CREDENTIAL_VAULT_STORE_FAILED", "Não foi possível guardar a credencial no Chaveiro do macOS.");
    }

    protected override string? ReadCore(string id)
    {
        var result = Run(Executable, ["find-generic-password", "-s", Service, "-a", id, "-w"]);
        if (result.NotFound || result.TimedOut || result.ExitCode != 0) return null;
        var value = result.StandardOutput.Trim();
        return value.Length == 0 ? null : value;
    }

    protected override void RemoveCore(string id) =>
        Run(Executable, ["delete-generic-password", "-s", Service, "-a", id]);
}
