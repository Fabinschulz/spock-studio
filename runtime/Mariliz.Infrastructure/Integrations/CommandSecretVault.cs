using System.Text;
using System.Text.RegularExpressions;
using Mariliz.Application.Execution;
using Mariliz.Application.Integrations;
using Mariliz.Application.Ports;

namespace Mariliz.Infrastructure.Integrations;

/// <summary>
/// Base dos cofres que falam com o cofre do próprio sistema por um comando dele (o <c>security</c> do macOS, o
/// <c>secret-tool</c> do Linux). As regras que valem para os dois ficam aqui, uma vez:
/// <list type="bullet">
/// <item>o segredo nunca vai em argumento de linha de comando (qualquer usuário da máquina lê os argumentos de um processo): vai
/// só pela entrada padrão do comando;</item>
/// <item>o segredo é guardado em base64, para nenhum caractere dele ser interpretado pelo comando nem perdido numa quebra de linha;</item>
/// <item>o identificador que vai nos argumentos tem um formato fixo, então nada de fora vira opção ou outro comando;</item>
/// <item>só se dá o segredo por guardado quando o cofre o devolve igual, como na chave do modelo;</item>
/// <item>"disponível" quer dizer que o comando existe <b>e</b> o cofre responde, e a falta tem um motivo que a tela mostra.</item>
/// </list>
/// </summary>
public abstract partial class CommandSecretVault(IProcessRunner runner) : ISecretVault
{
    protected static readonly TimeSpan Timeout = TimeSpan.FromSeconds(20);

    protected const string Service = "Spock Studio";

    private (bool Available, string? Hint)? _probed;

    // Nunca é gravado: só serve para perguntar ao cofre se ele responde.
    protected const string ProbeId = "conn-000000000000";

    public abstract string Name { get; }

    public bool Available => Probe().Available;

    public string? UnavailableHint => Probe().Hint;

    // Roda uma vez; o motor reinicia para tentar de novo.
    protected abstract (bool Available, string? Hint) ProbeCore();

    protected abstract void StoreCore(string id, string encoded);

    protected abstract string? ReadCore(string id);

    protected abstract void RemoveCore(string id);

    public void Store(string connectionId, string secret)
    {
        RequireValidId(connectionId);
        var encoded = Convert.ToBase64String(Encoding.UTF8.GetBytes(secret));
        StoreCore(connectionId, encoded);
        // O comando pode ter terminado "bem" sem guardar (o macOS, no modo interativo, não devolve erro): só vale o que volta igual.
        if (ReadCore(connectionId) != encoded)
            throw new EngineException("CREDENTIAL_VAULT_STORE_FAILED", "Não foi possível guardar a credencial com a proteção do sistema.");
    }

    public bool TryRead(string connectionId, out string secret)
    {
        secret = "";
        if (!ValidId(connectionId)) return false;
        var stored = ReadCore(connectionId);
        if (string.IsNullOrEmpty(stored)) return false;
        try
        {
            secret = Encoding.UTF8.GetString(Convert.FromBase64String(stored));
            return secret.Length > 0;
        }
        catch (FormatException)
        {
            secret = "";
            return false;
        }
    }

    public void Remove(string connectionId)
    {
        if (ValidId(connectionId)) RemoveCore(connectionId);
    }

    // O cofre é síncrono e curto: não há contexto de sincronização a travar.
    protected ProcessResult Run(string executable, IReadOnlyList<string> arguments, string? standardInput = null) =>
        runner.RunAsync(new ProcessSpec(executable, arguments, Environment.CurrentDirectory, Timeout, standardInput), CancellationToken.None)
            .GetAwaiter().GetResult();

    private (bool Available, string? Hint) Probe() => _probed ??= ProbeCore();

    protected static bool ValidId(string id) => Id().IsMatch(id);

    protected static void RequireValidId(string id)
    {
        if (!ValidId(id)) throw new ArgumentException("Identificador de credencial inválido.", nameof(id));
    }

    /// <summary>
    /// Só o formato dos identificadores do motor (<c>conn-</c> e doze caracteres): nada que o comando leia como opção. O fim é
    /// <c>\z</c> e não <c>$</c>: o <c>$</c> do .NET aceita uma quebra de linha no fim, e no macOS uma quebra de linha dentro do
    /// identificador abriria uma segunda linha de comando na entrada do <c>security -i</c>.
    /// </summary>
    [GeneratedRegex(@"^conn-[0-9a-f]{12}\z")]
    private static partial Regex Id();
}
