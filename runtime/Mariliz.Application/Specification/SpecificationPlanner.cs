using Mariliz.Application.Harness;
using Mariliz.Application.Ports;
using Mariliz.Core.Discovery;
using Mariliz.Core.Plan;
using Mariliz.Core.Policy;

namespace Mariliz.Application.Specification;

// Toda saída do planejador tem uma decisão sobre a estrutura; comando, limitações e risco dependem do caminho.
internal sealed record SpecificationPlan(ComponentDecision Decision, PlannedCommand? Command, IReadOnlyList<Limitation> Limitations, string? Risk)
{
    public static SpecificationPlan Only(ComponentDecision decision, params Limitation[] limitations) => new(decision, null, limitations, null);
}

internal static class SpecificationPlanner
{
    private sealed record InstallerBlock(string Decision, string Reason);

    private static InstallerBlock? InstallerBlocker(DiscoveryReport report, IProjectFiles files)
    {
        if (report.Tools.Node is null)
            return new("Node.js não encontrado; o instalador oficial da estrutura de especificação exige npx.", "Node.js (22 ou superior) não foi encontrado.");

        // O instalador oficial grava na pasta do package.json mais próximo, subindo a partir da pasta de trabalho.
        // Sem package.json no projeto, mas com um acima dele, a instalação cairia fora do projeto: nunca arriscamos isso.
        if (!files.FileExists("package.json") && files.NearestAncestorFolderWith("package.json") is { } outside)
            return new($"O instalador oficial instalaria a estrutura de especificação na pasta '{outside}', acima do projeto.",
                $"o instalador oficial grava na pasta do package.json mais próximo, e aqui seria '{outside}', fora do projeto. " +
                "Crie um package.json na raiz do projeto, ou instale a estrutura de especificação você mesmo na pasta do projeto, e planeje de novo.");

        return null;
    }

    private static ComponentDecision KeepInstalled(SpecificationStatus specification) =>
        new(ComponentKind.Specification, Catalog.SpecificationStructure, ComponentDecisions.Keep, $"Estrutura de especificação {specification.Version} instalada e íntegra (hash conferido com o lockfile do instalador).", DecisionOrigins.Existing, [SpecificationManifest.SkillDirectory]);

    private static IEnumerable<Limitation> Untested(SpecificationStatus specification) => specification.VersionTested
        ? []
        : [new(LimitationCodes.SpecificationUntested, $"A versão {specification.Version ?? "desconhecida"} da estrutura de especificação não foi validada por esta fase; o funcionamento não é garantido.")];

    // Só a declaração escrita pela pessoa dispensa a estrutura: a tela não oferece isso (ComponentRequest.IsManageable).
    internal static bool IsDeclined(IReadOnlyList<string> exclude) => exclude.Contains(Catalog.SpecificationStructure, StringComparer.Ordinal);

    internal static SpecificationPlan Plan(DiscoveryReport report, IProjectFiles files, PlanOptions options, IReadOnlyList<string> exclude)
    {
        var specification = report.Specification;
        var evidence = specification.Problems.Take(3).ToList();
        var installed = specification.State == SpecificationState.Installed;
        var outdated = installed && SpecificationManifest.IsOlderThanLatest(specification.Version);

        // Projeto com processo de especificação próprio: nada a instalar, e o plano continua idempotente. Uma estrutura já
        // instalada não é removida, porque veio de um instalador externo e pode ter conteúdo da pessoa.
        if (!installed && IsDeclined(exclude))
            return SpecificationPlan.Only(new(ComponentKind.Specification, Catalog.SpecificationStructure, ComponentDecisions.Skip,
                $"Dispensada em {files.Naming().Declaration} (components.exclude): o projeto segue o próprio processo de especificação.", DecisionOrigins.Declared, [files.Naming().Declaration]));

        // Instalação íntegra e atual: nada a fazer, e o plano continua idempotente.
        if (installed && !outdated && !options.UpdateSpecification)
            return SpecificationPlan.Only(KeepInstalled(specification), [.. Untested(specification)]);

        var blocker = InstallerBlocker(report, files);
        if (blocker is not null)
        {
            // A estrutura atual continua utilizável: o impedimento só barra a atualização, e a pessoa fica sabendo.
            if (installed)
                return SpecificationPlan.Only(KeepInstalled(specification), [.. Untested(specification), new(LimitationCodes.SpecificationOutdated,
                    (outdated ? $"A estrutura de especificação {specification.Version} é anterior à versão mais recente conhecida ({SpecificationManifest.LatestKnownVersion}). " : "A atualização da estrutura de especificação foi pedida. ")
                    + "Ela não foi planejada: " + blocker.Reason)]);
            return SpecificationPlan.Only(new(ComponentKind.Specification, Catalog.SpecificationStructure, ComponentDecisions.Blocked, blocker.Decision, DecisionOrigins.Baseline, []),
                new Limitation(LimitationCodes.SpecificationBlocked, "A estrutura de especificação não pôde ser instalada: " + blocker.Reason));
        }

        var needsForce = specification.State is SpecificationState.Drifted or SpecificationState.Invalid;
        if (needsForce && !options.AllowSpecificationReinstall)
            return SpecificationPlan.Only(new(ComponentKind.Specification, Catalog.SpecificationStructure, ComponentDecisions.Blocked,
                specification.State == SpecificationState.Drifted ? "A estrutura de especificação foi alterada localmente; reinstalar sobrescreveria essas edições." : "A estrutura de especificação está incompleta.",
                DecisionOrigins.Baseline, evidence),
                new Limitation(LimitationCodes.SpecificationNeedsReinstall,
                    "Reinstalar a estrutura de especificação sobrescreve as edições locais dela. Revise a divergência e, se for o caso, autorize a reinstalação ao planejar de novo."));

        var args = needsForce ? SpecificationManifest.ForceInstallArguments : SpecificationManifest.InstallArguments;
        if (installed)
        {
            // O instalador substitui a pasta da skill inteira (remove arquivos abolidos pela versão nova). Como a instalação
            // está íntegra, não há edição local a perder.
            return new SpecificationPlan(
                new(ComponentKind.Specification, Catalog.SpecificationStructure, ComponentDecisions.Enable,
                    outdated
                        ? $"A estrutura de especificação {specification.Version} é anterior à versão mais recente conhecida ({SpecificationManifest.LatestKnownVersion}): atualização pelo instalador oficial."
                        : "Atualização da estrutura de especificação pedida: o instalador oficial traz a versão mais recente disponível.",
                    DecisionOrigins.Baseline, [SpecificationManifest.SkillDirectory]),
                new PlannedCommand(
                    "specification-update", "Atualizar a estrutura de especificação para a versão mais recente (instalador oficial)",
                    "npx", args, [CommandEffect.Process, CommandEffect.Network, CommandEffect.WriteLocal], false,
                    [SpecificationManifest.SkillDirectory, ManagedScope.SkillsLockDirectory]),
                [],
                "A atualização da estrutura de especificação baixa a versão mais recente da rede e substitui a pasta da skill; o resultado é validado (arquivos, hash e scripts) e, se falhar, a versão atual é restaurada.");
        }

        return new SpecificationPlan(
            new(ComponentKind.Specification, Catalog.SpecificationStructure, ComponentDecisions.Enable,
                needsForce ? "Reinstalação autorizada da estrutura de especificação pelo instalador oficial." : "Estrutura de especificação ausente: instalação pelo instalador oficial, sempre na versão mais recente.",
                DecisionOrigins.Baseline, evidence),
            new PlannedCommand(
                "specification-install",
                needsForce ? "Reinstalar a estrutura de especificação (sobrescreve edições locais)" : "Instalar a estrutura de especificação pelo instalador oficial",
                "npx", args, [CommandEffect.Process, CommandEffect.Network, CommandEffect.WriteLocal], needsForce,
                [SpecificationManifest.SkillDirectory, ManagedScope.SkillsLockDirectory]),
            [],
            "A instalação da estrutura de especificação baixa conteúdo da rede e executa o pacote npm oficial; o resultado é validado (arquivos, hash e scripts) antes de a configuração ser dada como pronta.");
    }
}
