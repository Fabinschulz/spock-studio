namespace Mariliz.Core.Text;

public static class Ellipsis
{
    public static string Truncate(string text, int max) => text.Length <= max ? text : text[..max] + "…";
}
