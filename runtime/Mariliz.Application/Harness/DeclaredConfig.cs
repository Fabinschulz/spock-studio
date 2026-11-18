using Mariliz.Application.Discovery;
using Mariliz.Application.Ports;
using Mariliz.Core.Plan;
using Mariliz.Core.Text;
using YamlDotNet.Core;
using YamlDotNet.Serialization;

namespace Mariliz.Application.Harness;

// É criado uma vez e passa a ser da pessoa: a declaração vale mais que a inferência e a aplicação nunca o reescreve. A única edição é pontual, em components.exclude, quando a pessoa pede na tela e aprova no plano.
public sealed class DeclaredConfig
{
    public int Schema { get; set; } = 1;
    public ProjectSection Project { get; set; } = new();
    public string ImplementationModel { get; set; } = ImplementationModels.Mariliz;
    public string AiEnvironment { get; set; } = "";

    // C1: um projeto pode usar mais de uma IDE. Sem aiEnvironment, o principal é o primeiro da lista.
    [YamlMember(DefaultValuesHandling = DefaultValuesHandling.OmitNull)]
    public List<string>? AiEnvironments { get; set; }

    public string PrimaryEnvironment => AiEnvironment.Length > 0 ? AiEnvironment : AiEnvironments?.FirstOrDefault() ?? "";

    public IReadOnlyList<string> Environments =>
        (AiEnvironment.Length > 0 ? new[] { AiEnvironment } : []).Concat(AiEnvironments ?? []).Distinct(StringComparer.Ordinal).ToList();
    public ComponentsSection Components { get; set; } = new();
    public EvalsSection Evals { get; set; } = new();

    // Nunca escrito pela aplicação: só existe se a pessoa declarar.
    [YamlMember(DefaultValuesHandling = DefaultValuesHandling.OmitNull)]
    public KnowledgeSection? Knowledge { get; set; }

    public sealed class ProjectSection
    {
        // F1: gravado uma vez, por plano aprovado; vai para o Git com o resto da declaração.
        [YamlMember(DefaultValuesHandling = DefaultValuesHandling.OmitNull)]
        public string? Id { get; set; }

        public string Name { get; set; } = "";
    }
    public sealed class ComponentsSection { public List<string> Exclude { get; set; } = []; }
    public sealed class EvalsSection { public List<string> Required { get; set; } = []; }

    // Com include, só entra o que casar; exclude sempre tira.
    public sealed class KnowledgeSection
    {
        public List<string> Include { get; set; } = [];
        public List<string> Exclude { get; set; } = [];
    }

    public string ToYaml() =>
        "# Declaração do Harness gerenciado pelo Spock Studio.\n"
        + "# Este arquivo é seu: edite à vontade. A aplicação o lê, mas não o reescreve.\n"
        + YamlFiles.Writer.Serialize(this);

    // Nulo quando o arquivo não existe, não lê ou não tem um id válido.
    public static string? StableIdIn(string? text)
    {
        if (text is null) return null;
        try
        {
            var id = YamlFiles.Reader.Deserialize<DeclaredConfig>(text)?.Project?.Id;
            return StableProjectId.IsValid(id) ? id : null;
        }
        catch (YamlException)
        {
            return null;
        }
    }

    public static (DeclaredConfig? Config, string? Error) TryRead(IProjectFiles files)
    {
        var name = files.Naming().Declaration;
        var text = files.ReadText(name);
        if (text is null) return (null, null);
        try
        {
            var config = YamlFiles.Reader.Deserialize<DeclaredConfig>(text) ?? new DeclaredConfig();
            if (config.Schema != 1) return (null, $"{name}: schema {config.Schema} não é suportado (esperado 1).");
            if (!string.Equals(config.ImplementationModel, ImplementationModels.Mariliz, StringComparison.OrdinalIgnoreCase))
                return (null, $"{name}: implementationModel '{config.ImplementationModel}' não é suportado; esta fase usa somente '{ImplementationModels.Mariliz}'.");
            if (config.AiEnvironment.Length > 0 && !Discovery.AiEnvironments.Known.Contains(config.AiEnvironment))
                return (null, $"{name}: aiEnvironment '{config.AiEnvironment}' é desconhecido.");
            if (config.AiEnvironments?.FirstOrDefault(environment => !Discovery.AiEnvironments.Known.Contains(environment)) is { } unknown)
                return (null, $"{name}: '{unknown}' em aiEnvironments é desconhecido.");
            if (config is { AiEnvironment.Length: > 0, AiEnvironments.Count: > 0 } && !config.AiEnvironments.Contains(config.AiEnvironment))
                return (null, $"{name}: aiEnvironment '{config.AiEnvironment}' precisa estar também em aiEnvironments.");
            if (config.Project?.Id is { } id && !StableProjectId.IsValid(id))
                return (null, $"{name}: project.id '{id}' não é um id do Spock ({StableProjectId.Prefix} e 26 caracteres). Apague a linha e planeje de novo para gerar um.");
            return (config, null);
        }
        catch (YamlException ex)
        {
            return (null, $"{name} inválido: {ex.Message}");
        }
    }
}
