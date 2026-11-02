using Mariliz.Application.Discovery;
using Mariliz.Application.Ports;
using Mariliz.Core.Plan;
using YamlDotNet.Core;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace Mariliz.Application.Harness;

/// <summary>
/// <c>mariliz.yaml</c>: o que a pessoa declara. É criado uma vez e passa a ser dela; a aplicação o lê como
/// entrada do planejamento (a declaração vale mais que a inferência) e nunca o reescreve.
/// </summary>
public sealed class DeclaredConfig
{
    public const string FileName = "mariliz.yaml";

    public int Schema { get; set; } = 1;
    public ProjectSection Project { get; set; } = new();
    public string ImplementationModel { get; set; } = ImplementationModels.Mariliz;
    public string AiEnvironment { get; set; } = "";
    public ComponentsSection Components { get; set; } = new();
    public EvalsSection Evals { get; set; } = new();

    /// <summary>Fontes do índice de contexto. Opcional e nunca escrito pela aplicação: só existe se a pessoa declarar.</summary>
    [YamlMember(DefaultValuesHandling = DefaultValuesHandling.OmitNull)]
    public KnowledgeSection? Knowledge { get; set; }

    public sealed class ProjectSection { public string Name { get; set; } = ""; }
    public sealed class ComponentsSection { public List<string> Exclude { get; set; } = []; }
    public sealed class EvalsSection { public List<string> Required { get; set; } = []; }

    /// <summary>Padrões no estilo do .gitignore. Com <c>include</c>, só entra o que casar; <c>exclude</c> sempre tira.</summary>
    public sealed class KnowledgeSection
    {
        public List<string> Include { get; set; } = [];
        public List<string> Exclude { get; set; } = [];
    }

    private static readonly IDeserializer Reader = new DeserializerBuilder()
        .WithNamingConvention(CamelCaseNamingConvention.Instance)
        .IgnoreUnmatchedProperties()
        .Build();

    private static readonly ISerializer Writer = new SerializerBuilder()
        .WithNamingConvention(CamelCaseNamingConvention.Instance)
        .ConfigureDefaultValuesHandling(DefaultValuesHandling.Preserve)
        .Build();

    public string ToYaml() =>
        "# Declaração do Harness gerenciado pelo Mariliz Studio.\n"
        + "# Este arquivo é seu: edite à vontade. A aplicação o lê, mas não o reescreve.\n"
        + Writer.Serialize(this);

    public static (DeclaredConfig? Config, string? Error) TryRead(IProjectFiles files)
    {
        var text = files.ReadText(FileName);
        if (text is null) return (null, null);
        try
        {
            var config = Reader.Deserialize<DeclaredConfig>(text) ?? new DeclaredConfig();
            if (config.Schema != 1) return (null, $"mariliz.yaml: schema {config.Schema} não é suportado (esperado 1).");
            if (!string.Equals(config.ImplementationModel, ImplementationModels.Mariliz, StringComparison.OrdinalIgnoreCase))
                return (null, $"mariliz.yaml: implementationModel '{config.ImplementationModel}' não é suportado; esta fase usa somente '{ImplementationModels.Mariliz}'.");
            if (config.AiEnvironment.Length > 0 && !AiEnvironments.Known.Contains(config.AiEnvironment))
                return (null, $"mariliz.yaml: aiEnvironment '{config.AiEnvironment}' é desconhecido.");
            return (config, null);
        }
        catch (YamlException ex)
        {
            return (null, $"mariliz.yaml inválido: {ex.Message}");
        }
    }
}
