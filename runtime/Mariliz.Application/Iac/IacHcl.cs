using System.Text;

namespace Mariliz.Application.Iac;

// Escreve HCL já no formato do `fmt`: dois espaços por nível e o "=" alinhado dentro de cada grupo de atributos de uma
// linha seguidos. Atributo de várias linhas, comentário e linha em branco fecham o grupo. Os valores são expressões HCL
// prontas (texto entre aspas vem com aspas): o construtor não interpreta nada.
public sealed class IacHcl
{
    private readonly StringBuilder _text = new();
    private readonly List<(string Name, string Value)> _group = [];
    private int _depth;

    public static string Quote(string value) => "\"" + value.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";

    public static string List(IEnumerable<string> values) => "[" + string.Join(", ", values) + "]";

    public static string QuotedList(IEnumerable<string> values) => List(values.Select(Quote));

    // # ------------------------- Título -------------------------------  (o cabeçalho de seção do modelo)
    public IacHcl Section(string title)
    {
        Flush();
        var dashes = Math.Max(3, 36 - title.Length);
        Line($"# ------------------------- {title} {new string('-', dashes)}");
        return this;
    }

    public IacHcl Comment(string text)
    {
        Flush();
        foreach (var line in text.Split('\n')) Line("# " + line);
        return this;
    }

    public IacHcl Attr(string name, string value)
    {
        _group.Add((name, value));
        return this;
    }

    public IacHcl Blank()
    {
        Flush();
        // Nunca duas linhas em branco seguidas, nem logo depois de abrir um bloco.
        if (_text.Length > 0 && !EndsWith("\n\n") && !EndsWith("{\n") && !EndsWith("[\n") && !EndsWith("({\n") && !EndsWith("([{\n")) _text.Append('\n');
        return this;
    }

    public IacHcl Block(string header, Action<IacHcl> body)
    {
        Flush();
        Line(header + " {");
        _depth++;
        body(this);
        Flush();
        TrimTrailingBlank();
        _depth--;
        Line("}");
        return this;
    }

    // name = { ... } com as chaves alinhadas, ou name = jsonencode({ ... }) com open/close.
    public IacHcl Object(string name, Action<IacHcl> body, string open = "{", string close = "}")
    {
        Flush();
        Line($"{name} = {open}");
        _depth++;
        body(this);
        Flush();
        TrimTrailingBlank();
        _depth--;
        Line(close);
        return this;
    }

    // Uma linha pronta (lista de várias linhas, por exemplo), sem alinhamento.
    public IacHcl Raw(string line)
    {
        Flush();
        Line(line);
        return this;
    }

    public override string ToString()
    {
        Flush();
        TrimTrailingBlank();
        return _text.ToString().TrimEnd('\n') + "\n";
    }

    private void Flush()
    {
        if (_group.Count == 0) return;
        var width = _group.Max(attribute => attribute.Name.Length);
        foreach (var (name, value) in _group) Line($"{name.PadRight(width)} = {value}");
        _group.Clear();
    }

    private void Line(string text) => _text.Append(new string(' ', _depth * 2)).Append(text).Append('\n');

    private bool EndsWith(string suffix) => _text.Length >= suffix.Length && _text.ToString(_text.Length - suffix.Length, suffix.Length) == suffix;

    private void TrimTrailingBlank()
    {
        while (EndsWith("\n\n")) _text.Length--;
    }
}
