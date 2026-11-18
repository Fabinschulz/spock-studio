namespace Mariliz.Application.Iac;

// Um gerador por nuvem, todos no mesmo modelo de pasta; quem chama não precisa saber qual.
public static class IacGenerators
{
    public static IReadOnlyList<IacGeneratedFile> Generate(IacBlueprint b, string root) => b.Provider switch
    {
        "azure" => IacAzureGenerator.Generate(b, root),
        "gcp" => IacGoogleGenerator.Generate(b, root),
        _ => IacAwsGenerator.Generate(b, root)
    };

    public static IReadOnlyList<IacCostItem> Costs(IacBlueprint b) => b.Provider switch
    {
        "azure" => IacAzureGenerator.Costs(b),
        "gcp" => IacGoogleGenerator.Costs(b),
        _ => IacAwsGenerator.Costs(b)
    };
}
