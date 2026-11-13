namespace Mariliz.Application.Integrations;

// O cofre guarda segredos por identificador com o formato de conexão; estes ficam reservados para segredos do próprio aplicativo e a criação de conexões nunca os gera.
public static class ReservedSecretIds
{
    public const string ModelKey = "conn-0000000000ac";
}
