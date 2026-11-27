namespace Mariliz.Application.Boot;

public sealed record CapabilityStatus(Capability Capability, CapabilityAvailability Availability, string? Reason);

/// <summary>
/// Junta a ficha do catálogo com os fatos de agora. "Disponível" quer dizer que o motor desta sessão roteia os métodos e que as
/// pré-condições valem; não quer dizer que a tela existe nem que alguém autorizou o uso (isso é do <see cref="PermissionEvaluator"/>).
/// </summary>
public sealed class CapabilityRegistry(IReadOnlyList<Capability>? catalog = null)
{
    private readonly IReadOnlyList<Capability> _catalog = Validated(catalog ?? CapabilityCatalog.All);

    public IReadOnlyList<Capability> All => _catalog;

    public Capability? Find(string id) => _catalog.FirstOrDefault(capability => capability.Id == id);

    public IReadOnlyList<CapabilityStatus> Resolve(BootFacts facts) => [.. _catalog.Select(capability => Resolve(capability, facts))];

    public CapabilityStatus Resolve(Capability capability, BootFacts facts)
    {
        if (capability.IsComposite)
        {
            var members = capability.Composes.Select(id => Resolve(Find(id)!, facts)).ToList();
            var blocking = members.FirstOrDefault(member => member.Availability is not (CapabilityAvailability.Available or CapabilityAvailability.Partial));
            return blocking is null
                ? new CapabilityStatus(capability, members.Any(member => member.Availability == CapabilityAvailability.Partial) ? CapabilityAvailability.Partial : CapabilityAvailability.Available, null)
                : new CapabilityStatus(capability, blocking.Availability, $"A etapa \"{blocking.Capability.Name}\" não está disponível: {blocking.Reason}");
        }

        var missing = capability.Methods.Where(method => !facts.RoutedMethods.Contains(method)).ToList();
        if (missing.Count == capability.Methods.Count && missing.Count > 0)
            return new CapabilityStatus(capability, CapabilityAvailability.Unavailable, "O motor desta sessão não oferece esta capacidade.");

        if (capability.Needs.HasFlag(CapabilityNeeds.Project) && !facts.ProjectAuthorized)
            return new CapabilityStatus(capability, CapabilityAvailability.BlockedByPermission,
                facts.ProjectId is null ? "Escolha um projeto: esta capacidade trabalha sobre um projeto autorizado." : "O projeto não foi autorizado neste computador.");

        if (capability.Needs.HasFlag(CapabilityNeeds.Model) && !facts.ModelConfigured)
            return new CapabilityStatus(capability, CapabilityAvailability.NotConfigured, "Falta configurar um provedor de IA em Configurações.");

        if (capability.Needs.HasFlag(CapabilityNeeds.ProjectIntegration) && facts.IntegrationLinks == 0)
            return new CapabilityStatus(capability, CapabilityAvailability.DependsOnIntegration, "O projeto não tem GitHub nem Jira vinculados: não há dados de gestão para ler.");

        if (capability.Needs.HasFlag(CapabilityNeeds.AnyIntegration) && facts.PortfolioIntegrationLinks == 0)
            return new CapabilityStatus(capability, CapabilityAvailability.DependsOnIntegration, "Nenhum projeto tem GitHub ou Jira vinculados: o portfólio não tem dados.");

        if (missing.Count > 0)
            return new CapabilityStatus(capability, CapabilityAvailability.Partial, "Parte dos recursos desta capacidade não está nesta sessão do motor.");

        return new CapabilityStatus(capability, CapabilityAvailability.Available, null);
    }

    // Um catálogo malformado é erro de montagem: falha ao subir o motor, não no primeiro pedido.
    private static IReadOnlyList<Capability> Validated(IReadOnlyList<Capability> catalog)
    {
        var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (var capability in catalog)
        {
            if (!ids.Add(capability.Id)) throw new InvalidOperationException($"Capacidade repetida: {capability.Id}.");
            if (!BootDomains.All.Contains(capability.Domain)) throw new InvalidOperationException($"Domínio desconhecido em {capability.Id}.");
            if (capability.Mode == ExecutionMode.Direct && capability.Operation > OperationClass.Preparation)
                throw new InvalidOperationException($"{capability.Id} é execução direta, mas altera mais do que a pasta de dados do Spock.");
            if (capability.Mode == ExecutionMode.Direct && (capability.Method is null || !capability.Methods.Contains(capability.Method)))
                throw new InvalidOperationException($"{capability.Id} não declara o método que executa.");
            if (capability.Mode != ExecutionMode.Composite && capability.Route is null)
                throw new InvalidOperationException($"{capability.Id} não tem tela de destino.");
        }
        foreach (var composite in catalog.Where(capability => capability.IsComposite))
            if (composite.Composes.Any(member => !ids.Contains(member) || catalog.First(c => c.Id == member).IsComposite))
                throw new InvalidOperationException($"{composite.Id} compõe uma capacidade que não existe ou que também é composta.");
        return catalog;
    }
}
