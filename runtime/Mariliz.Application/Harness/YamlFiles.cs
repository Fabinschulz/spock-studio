using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace Mariliz.Application.Harness;

// Campos extras ignorados.
internal static class YamlFiles
{
    public static readonly IDeserializer Reader = new DeserializerBuilder()
        .WithNamingConvention(CamelCaseNamingConvention.Instance)
        .IgnoreUnmatchedProperties()
        .Build();

    public static readonly ISerializer Writer = new SerializerBuilder()
        .WithNamingConvention(CamelCaseNamingConvention.Instance)
        .ConfigureDefaultValuesHandling(DefaultValuesHandling.Preserve)
        .Build();
}
