using Mariliz.Application.Models;

namespace Mariliz.Application.Integrations;

// O cofre guarda segredos por identificador com o formato de conexão; estes ficam reservados para segredos do próprio aplicativo e a criação de conexões nunca os gera.
public static class ReservedSecretIds
{
    // De quando só havia uma chave para o aplicativo todo: hoje só a migração a lê.
    public const string ModelKey = "conn-0000000000ac";

    // Um provedor novo precisa de um identificador novo aqui; trocar um existente deixaria a chave guardada fora de alcance.
    private static readonly IReadOnlyDictionary<string, string> ModelKeys = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        [ModelProviders.OpenAi.Id] = "conn-0000000000a1",
        [ModelProviders.Anthropic.Id] = "conn-0000000000a2",
        [ModelProviders.Google.Id] = "conn-0000000000a3"
    };

    public static string ModelKeyOf(string provider) => ModelKeys[provider];

    public static bool IsReserved(string id) => id == ModelKey || ModelKeys.Values.Contains(id);
}
