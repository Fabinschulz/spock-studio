using Mariliz.Application.Discovery;
using Mariliz.Application.Ports;
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
    public MethodSection Method { get; set; } = new();
    public string AiEnvironment { get; set; } = "";
    public ComponentsSection Components { get; set; } = new();
    public EvalsSection Evals { get; set; } = new();

    public sealed class ProjectSection { public string Name { get; set; } = ""; }
    public sealed class MethodSection { public string Sdd { get; set; } = "tlc"; }
    public sealed class ComponentsSection { public List<string> Exclude { get; set; } = []; }
    public sealed class EvalsSection { public List<string> Required { get; set; } = []; }

    private static readonly IDeserializer Reader = new DeserializerBuilder()
        .WithNamingConvention(CamelCaseNamingConvention.Instance)
        .IgnoreUnmatchedProperties()
        .Build();

    private static readonly ISerializer Writer = new SerializerBuilder()
        .WithNamingConvention(CamelCaseNamingConvention.Instance)
        .ConfigureDefaultValuesHandling(DefaultValuesHandling.Preserve)
        .Build();

    public string ToYaml() =>
        "# Declaração do Harness gerenciado pela Mariliz AI Engineering.\n"
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
            if (!string.Equals(config.Method.Sdd, "tlc", StringComparison.OrdinalIgnoreCase))
                return (null, $"mariliz.yaml: method.sdd '{config.Method.Sdd}' não é suportado; esta fase usa somente 'tlc'.");
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

/// <summary>
/// <c>mariliz.lock</c>: o que a aplicação gerencia, com o hash do que escreveu. Sem datas, para que repetir
/// o fluxo produza exatamente o mesmo arquivo.
/// </summary>
public sealed class ManagedLockFile
{
    public const string FileName = "mariliz.lock";

    public int Schema { get; set; } = 1;
    public string Engine { get; set; } = "";
    public string AiEnvironment { get; set; } = "";
    public SddSection Sdd { get; set; } = new();
    public List<ComponentEntry> Components { get; set; } = [];
    public List<ManagedFileEntry> ManagedFiles { get; set; } = [];

    public sealed class SddSection
    {
        public string Method { get; set; } = "tlc";
        public string Version { get; set; } = "";
        public string ContentHash { get; set; } = "";
    }

    public sealed class ComponentEntry
    {
        public string Id { get; set; } = "";
        public string Kind { get; set; } = "";
        public string Version { get; set; } = "";
    }

    /// <summary>Modo <c>file</c>: a aplicação é dona do arquivo inteiro (hash conferido). Modo <c>merge</c>: só de chaves dele.</summary>
    public sealed class ManagedFileEntry
    {
        public string Path { get; set; } = "";
        public string Component { get; set; } = "";
        public string Mode { get; set; } = "file";
        public string Sha256 { get; set; } = "";
    }

    private static readonly IDeserializer Reader = new DeserializerBuilder()
        .WithNamingConvention(CamelCaseNamingConvention.Instance)
        .IgnoreUnmatchedProperties()
        .Build();

    private static readonly ISerializer Writer = new SerializerBuilder()
        .WithNamingConvention(CamelCaseNamingConvention.Instance)
        .ConfigureDefaultValuesHandling(DefaultValuesHandling.Preserve)
        .Build();

    public string ToYaml()
    {
        Components = Components.OrderBy(c => c.Id, StringComparer.Ordinal).ToList();
        ManagedFiles = ManagedFiles.OrderBy(f => f.Path, StringComparer.Ordinal).ToList();
        return "# Gerado pela Mariliz AI Engineering. Não edite à mão: use a aplicação.\n" + Writer.Serialize(this);
    }

    public static ManagedLockFile? Read(IProjectFiles files)
    {
        var text = files.ReadText(FileName);
        if (text is null) return null;
        try { return Reader.Deserialize<ManagedLockFile>(text); }
        catch (YamlException) { return null; }
    }

    public static ManagedLockFile? TryParse(string text)
    {
        try { return Reader.Deserialize<ManagedLockFile>(text); }
        catch (YamlException) { return null; }
    }
}
