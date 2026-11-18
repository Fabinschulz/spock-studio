using System.Text.RegularExpressions;

namespace Mariliz.Application.Discovery;

// O único registro de tecnologia: detectores, plano do Harness, validação, perfil e leitura da arquitetura perguntam aqui.
// Uma stack nova entra com uma linha; o que muda de comportamento por stack (regra, área, categoria) fica nela.
public static class Technologies
{
    public static readonly Technology Nextjs = new("nextjs", "Next.js", "Framework web", RuleFamily.Javascript, "interface", Pattern(@"\bnext\.?js\b"));
    public static readonly Technology Angular = new("angular", "Angular", "Framework web", RuleFamily.Javascript, "interface", Pattern(@"\bangular\b"));
    public static readonly Technology React = new("react", "React", "Biblioteca de interface", RuleFamily.Javascript, "interface", Pattern(@"\breact\b"));
    public static readonly Technology Dotnet = new("dotnet", ".NET", "Plataforma e runtime", RuleFamily.Dotnet, "servico",
        Pattern(@"\.net\b|\basp\.net|\bc#|\bdotnet\b|\bblazor\b|\bentity framework\b"));
    public static readonly Technology Node = new("node", "Node.js", "Runtime", RuleFamily.Javascript, "interface",
        Pattern(@"\bnode(\.?js)?\b|\bnestjs\b|\bexpress\b|\btypescript\b|\bjavascript\b|\bvue(\.js)?\b|\bnuxt\b|\bsvelte\b"));
    public static readonly Technology Flutter = new("flutter", "Flutter", "Framework de aplicativo", RuleFamily.None, "interface", Pattern(@"\bflutter\b|\bdart\b"));
    public static readonly Technology Python = new("python", "Python", "Linguagem e runtime", RuleFamily.None, null, Pattern(@"\bpython\b|\bdjango\b|\bfastapi\b|\bflask\b"));
    public static readonly Technology Java = new("java", "Java", "Plataforma e runtime", RuleFamily.None, null, Pattern(@"\bjava\b|\bspring\b|\bkotlin\b"));
    public static readonly Technology Go = new("go", "Go", "Linguagem", RuleFamily.None, null, Pattern(@"\bgolang\b|\bgo\b"));
    public static readonly Technology Rust = new("rust", "Rust", "Linguagem", RuleFamily.None, null, Pattern(@"\brust\b"));

    // A ordem vale para ler a arquitetura: a primeira que casar vence ("Next.js com React" é Next.js, "ASP.NET com TypeScript" é .NET).
    public static readonly IReadOnlyList<Technology> All = [Nextjs, Angular, React, Dotnet, Node, Flutter, Python, Java, Go, Rust];

    public static Technology? Find(string? id) => id is null ? null : All.FirstOrDefault(technology => technology.Id == id);

    public static RuleFamily RulesOf(string? id) => Find(id)?.Rules ?? RuleFamily.None;

    // A tecnologia nomeada numa linha do documento de arquitetura, na ordem de All.
    public static Technology? Named(string text) => All.FirstOrDefault(technology => technology.Architecture?.IsMatch(text) == true);

    private static Regex Pattern(string pattern) => new(pattern, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
}
