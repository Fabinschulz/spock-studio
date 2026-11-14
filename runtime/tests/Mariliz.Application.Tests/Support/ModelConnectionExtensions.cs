using Mariliz.Application.Models;

namespace Mariliz.Application.Tests.Support;

internal static class ModelConnectionExtensions
{
    public static ProviderConnection Default(this ModelConnection connection) => connection.Providers.Single(provider => provider.IsDefault);

    public static ProviderConnection Of(this ModelConnection connection, string provider) => connection.Providers.Single(entry => entry.Id == provider);
}
