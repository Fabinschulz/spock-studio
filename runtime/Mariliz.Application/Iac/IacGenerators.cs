namespace Mariliz.Application.Iac;

// Um gerador por nuvem, todos no mesmo modelo de pasta; quem chama não precisa saber qual.
public static class IacGenerators
{
    public static IReadOnlyList<IacGeneratedFile> Generate(IacBlueprint b, string root) => IacProviders.Require(b.Provider).Generate(b, root);

    public static IReadOnlyList<IacCostItem> Costs(IacBlueprint b) => IacProviders.Require(b.Provider).Costs(b);
}
