using System.Text;

namespace Mariliz.Application.Mcp;

internal static class McpText
{
    // Corta em fim de linha e avisa: a IDE paga cada caractere em tokens e o modelo precisa saber que faltou parte.
    public static string Fit(IEnumerable<string> blocks, int maxChars, string hint)
    {
        var text = new StringBuilder();
        foreach (var block in blocks)
        {
            var separator = text.Length == 0 ? "" : "\n\n";
            if (text.Length + separator.Length + block.Length <= maxChars)
            {
                text.Append(separator).Append(block);
                continue;
            }

            var room = maxChars - text.Length - separator.Length;
            var partial = room > 0 ? block[..room] : "";
            var lastLine = partial.LastIndexOf('\n');
            if (lastLine > 0) text.Append(separator).Append(partial[..lastLine]);
            text.Append($"\n\n[Cortado para caber em {maxChars} caracteres. {hint}]");
            break;
        }
        return text.ToString();
    }
}
