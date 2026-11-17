using Mariliz.Application.Ports;
using YamlDotNet.Core;
using YamlDotNet.Serialization;

namespace Mariliz.Application.Harness;

// Sem datas, para que repetir o fluxo produza exatamente o mesmo arquivo.
public sealed class ManagedLockFile
{
    public const string FileName = "mariliz.lock";

    public int Schema { get; set; } = 1;
    public string Engine { get; set; } = "";
    public string AiEnvironment { get; set; } = "";
    public SpecificationSection Specification { get; set; } = new();
    public List<ComponentEntry> Components { get; set; } = [];
    public List<ManagedFileEntry> ManagedFiles { get; set; } = [];

    public sealed class SpecificationSection
    {
        public string Version { get; set; } = "";
        public string ContentHash { get; set; } = "";
    }

    public sealed class ComponentEntry
    {
        public string Id { get; set; } = "";
        public string Kind { get; set; } = "";
        public string Version { get; set; } = "";
    }

    public sealed class ManagedFileEntry
    {
        public string Path { get; set; } = "";
        public string Component { get; set; } = "";
        public string Mode { get; set; } = "file";

        // Só nos modos de trecho (ManagedRegion): o Sha256 é o do trecho, não o do arquivo.
        [YamlMember(DefaultValuesHandling = DefaultValuesHandling.OmitNull)]
        public string? Region { get; set; }

        public string Sha256 { get; set; } = "";
    }

    public string ToYaml()
    {
        Components = Components.OrderBy(c => c.Id, StringComparer.Ordinal).ToList();
        ManagedFiles = ManagedFiles.OrderBy(f => f.Path, StringComparer.Ordinal).ToList();
        return "# Gerado pelo Mariliz Studio. Não edite à mão: use a aplicação.\n" + YamlFiles.Writer.Serialize(this);
    }

    // Lock ilegível vale como ausente: o plano o recria em vez de confiar em conteúdo quebrado.
    public static ManagedLockFile? Read(IProjectFiles files)
    {
        var text = files.ReadText(FileName);
        if (text is null) return null;
        try { return YamlFiles.Reader.Deserialize<ManagedLockFile>(text); }
        catch (YamlException) { return null; }
    }
}
