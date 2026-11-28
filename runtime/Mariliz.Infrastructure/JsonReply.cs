using System.Text.Json;
using System.Text.RegularExpressions;

namespace Mariliz.Infrastructure;

// Um modelo às vezes devolve o JSON pedido com uma cerca de código em volta, ou com uma frase antes ou depois ("Aqui está o desenho:").
// O que vale é o objeto: quem lê a resposta recebe só ele. O que não é JSON em nenhuma leitura volta como veio, para o chamador recusar.
internal static partial class JsonReply
{
    public static string Extract(string text)
    {
        var trimmed = text.Trim();
        if (IsObject(trimmed)) return trimmed;

        var fenced = Fence().Match(trimmed);
        if (fenced.Success && IsObject(fenced.Groups["body"].Value.Trim())) return fenced.Groups["body"].Value.Trim();

        for (var start = trimmed.IndexOf('{'); start >= 0; start = trimmed.IndexOf('{', start + 1))
        {
            var end = BalancedEnd(trimmed, start);
            // Um objeto que não fecha é uma resposta cortada: um objeto menor de dentro dela não é o que o modelo quis dizer.
            if (end < 0) break;
            if (IsObject(trimmed[start..(end + 1)])) return trimmed[start..(end + 1)];
        }
        return fenced.Success ? fenced.Groups["body"].Value.Trim() : text;
    }

    // Fim do objeto que abre em start, contando chaves fora das strings; -1 se não fecha.
    private static int BalancedEnd(string text, int start)
    {
        var depth = 0;
        var inString = false;
        for (var i = start; i < text.Length; i++)
        {
            var c = text[i];
            if (inString)
            {
                if (c == '\\') i++;
                else if (c == '"') inString = false;
            }
            else if (c == '"') inString = true;
            else if (c == '{') depth++;
            else if (c == '}' && --depth == 0) return i;
        }
        return -1;
    }

    private static bool IsObject(string text)
    {
        if (text.Length == 0 || text[0] != '{') return false;
        try
        {
            using var document = JsonDocument.Parse(text);
            return document.RootElement.ValueKind == JsonValueKind.Object;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    [GeneratedRegex(@"```[a-zA-Z]*\s*(?<body>.*?)\s*```", RegexOptions.Singleline)]
    private static partial Regex Fence();
}
