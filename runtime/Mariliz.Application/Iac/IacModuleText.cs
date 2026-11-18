using static Mariliz.Application.Iac.IacHcl;

namespace Mariliz.Application.Iac;

public sealed record IacGeneratedFile(string Path, string Content);

// O texto que os geradores das três nuvens montam do mesmo jeito: outputs, as variáveis comuns de todo módulo e o
// construtor de variables.tf.
internal static class IacModuleText
{
    internal static string Outputs(params (string Name, string Description, string Value)[] outputs)
    {
        var h = new IacHcl();
        foreach (var (name, description, value) in outputs)
        {
            h.Block($"output \"{name}\"", o => o.Attr("description", Quote(description)).Attr("value", value));
            h.Blank();
        }
        return h.ToString();
    }

    // name_prefix e tags em todo módulo, como environment e tags no modelo.
    internal static string ModuleVariables(Action<Variables> extra)
    {
        var v = new Variables();
        v.Validated("name_prefix", "string", "Prefixo dos nomes: <projeto>-<ambiente>", "can(regex(\"^[a-z0-9-]+$\", var.name_prefix))",
            "O prefixo deve conter apenas letras minúsculas, dígitos e hífens.", "Environment");
        extra(v);
        v.Default("tags", "map(string)", "Tags aplicadas aos recursos", "{}", "Tags");
        return v.ToString();
    }

    internal sealed class Variables
    {
        private readonly IacHcl _h = new();
        private bool _first = true;

        public Variables Required(string name, string type, string description, string? section = null) => Add(name, type, description, null, null, null, section);

        public Variables Nullable(string name, string type, string description) => Add(name, type, description, "null", null, null, null);

        public Variables Default(string name, string type, string description, string value, string? section = null) =>
            Add(name, type, description, value, null, null, section);

        public Variables Validated(string name, string type, string description, string condition, string message, string? section = null) =>
            Add(name, type, description, null, condition, message, section);

        private Variables Add(string name, string type, string description, string? value, string? condition, string? message, string? section)
        {
            if (!_first) _h.Blank();
            _first = false;
            if (section is not null) _h.Section(section);
            _h.Block($"variable \"{name}\"", v =>
            {
                v.Attr("description", Quote(description)).Attr("type", type);
                if (value is not null) v.Attr("default", value);
                if (condition is not null)
                {
                    v.Blank();
                    v.Block("validation", c => c.Attr("condition", condition).Attr("error_message", Quote(message!)));
                }
            });
            return this;
        }

        public override string ToString() => _h.ToString();
    }
}
