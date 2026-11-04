using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace Mariliz.Application.Harness;

/// <summary>Leitura e escrita de <c>mariliz.yaml</c> e <c>mariliz.lock</c>: o mesmo formato (camelCase, campos extras ignorados).</summary>
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
