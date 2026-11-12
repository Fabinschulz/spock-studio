namespace Mariliz.Application.Integrations;

/// <summary>
/// Identificadores do cofre que não pertencem a nenhuma conexão. O cofre guarda segredos por identificador com o formato
/// de conexão; estes ficam reservados para segredos do próprio aplicativo, e a criação de conexões nunca os gera.
/// </summary>
public static class ReservedSecretIds
{
    /// <summary>A chave do provedor de modelo. Antes ficava em texto puro em <c>model.json</c>.</summary>
    public const string ModelKey = "conn-0000000000ac";
}
