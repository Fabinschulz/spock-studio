using Mariliz.Application.Harness;
using Mariliz.Application.Ports;
using Mariliz.Core.Discovery;
using Mariliz.Core.Plan;
using Mariliz.Core.Policy;

namespace Mariliz.Application.Specification;

/// <summary>Decide o que fazer com a estrutura de especificação: manter, instalar, atualizar ou bloquear.</summary>
internal static class SpecificationPlanner
{
    /// <summary>Motivo pelo qual o instalador oficial não pode rodar com segurança; <c>Decision</c> é a frase da decisão, <c>Reason</c> a da limitação.</summary>
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

    private static void KeepInstalled(SpecificationStatus specification, List<ComponentDecision> decisions, List<Limitation> limitations)
    {
        decisions.Add(new(ComponentKind.Specification, Catalog.SpecificationStructure, ComponentDecisions.Keep, $"Estrutura de especificação {specification.Version} instalada e íntegra (hash conferido com o lockfile do instalador).", DecisionOrigins.Existing, [SpecificationManifest.SkillDirectory]));
        if (!specification.VersionTested)
            limitations.Add(new(LimitationCodes.SpecificationUntested, $"A versão {specification.Version ?? "desconhecida"} da estrutura de especificação não foi validada por esta fase; o funcionamento não é garantido."));
    }

    internal static void Plan(DiscoveryReport report, IProjectFiles files, PlanOptions options, List<ComponentDecision> decisions,
        List<PlannedCommand> commands, List<Limitation> limitations, List<string> risks)
    {
        var specification = report.Specification;
        var evidence = specification.Problems.Take(3).ToList();
        var installed = specification.State == SpecificationState.Installed;
        var outdated = installed && SpecificationManifest.IsOlderThanLatest(specification.Version);

        // Instalação íntegra e atual: nada a fazer, e o plano continua idempotente.
        if (installed && !outdated && !options.UpdateSpecification)
        {
            KeepInstalled(specification, decisions, limitations);
            return;
        }

        var blocker = InstallerBlocker(report, files);
        if (blocker is not null)
        {
            if (installed)
            {
                // A estrutura atual continua utilizável: o impedimento só barra a atualização, e a pessoa fica sabendo.
                KeepInstalled(specification, decisions, limitations);
                limitations.Add(new(LimitationCodes.SpecificationOutdated,
                    (outdated ? $"A estrutura de especificação {specification.Version} é anterior à versão mais recente conhecida ({SpecificationManifest.LatestKnownVersion}). " : "A atualização da estrutura de especificação foi pedida. ")
                    + "Ela não foi planejada: " + blocker.Reason));
            }
            else
            {
                decisions.Add(new(ComponentKind.Specification, Catalog.SpecificationStructure, ComponentDecisions.Blocked, blocker.Decision, DecisionOrigins.Baseline, []));
                limitations.Add(new(LimitationCodes.SpecificationBlocked, "A estrutura de especificação não pôde ser instalada: " + blocker.Reason));
            }
            return;
        }

        var needsForce = specification.State is SpecificationState.Drifted or SpecificationState.Invalid;
        if (needsForce && !options.AllowSpecificationReinstall)
        {
            decisions.Add(new(ComponentKind.Specification, Catalog.SpecificationStructure, ComponentDecisions.Blocked,
                specification.State == SpecificationState.Drifted ? "A estrutura de especificação foi alterada localmente; reinstalar sobrescreveria essas edições." : "A estrutura de especificação está incompleta.",
                DecisionOrigins.Baseline, evidence));
            limitations.Add(new(LimitationCodes.SpecificationNeedsReinstall,
                "Reinstalar a estrutura de especificação sobrescreve as edições locais dela. Revise a divergência e, se for o caso, autorize a reinstalação ao planejar de novo."));
            return;
        }

        var args = needsForce ? SpecificationManifest.ForceInstallArguments : SpecificationManifest.InstallArguments;
        if (installed)
        {
            // O instalador substitui a pasta da skill inteira (remove arquivos abolidos pela versão nova). Como a instalação
            // está íntegra, não há edição local a perder.
            decisions.Add(new(ComponentKind.Specification, Catalog.SpecificationStructure, ComponentDecisions.Enable,
                outdated
                    ? $"A estrutura de especificação {specification.Version} é anterior à versão mais recente conhecida ({SpecificationManifest.LatestKnownVersion}): atualização pelo instalador oficial."
                    : "Atualização da estrutura de especificação pedida: o instalador oficial traz a versão mais recente disponível.",
                DecisionOrigins.Baseline, [SpecificationManifest.SkillDirectory]));
            commands.Add(new PlannedCommand(
                "specification-update", "Atualizar a estrutura de especificação para a versão mais recente (instalador oficial)",
                "npx", args, [CommandEffect.Process, CommandEffect.Network, CommandEffect.WriteLocal], false,
                [SpecificationManifest.SkillDirectory, ManagedScope.SkillsLockDirectory]));
            risks.Add("A atualização da estrutura de especificação baixa a versão mais recente da rede e substitui a pasta da skill; o resultado é validado (arquivos, hash e scripts) e, se falhar, a versão atual é restaurada.");
            return;
        }

        decisions.Add(new(ComponentKind.Specification, Catalog.SpecificationStructure, ComponentDecisions.Enable,
            needsForce ? "Reinstalação autorizada da estrutura de especificação pelo instalador oficial." : "Estrutura de especificação ausente: instalação pelo instalador oficial, sempre na versão mais recente.",
            DecisionOrigins.Baseline, evidence));
        commands.Add(new PlannedCommand(
            "specification-install",
            needsForce ? "Reinstalar a estrutura de especificação (sobrescreve edições locais)" : "Instalar a estrutura de especificação pelo instalador oficial",
            "npx", args, [CommandEffect.Process, CommandEffect.Network, CommandEffect.WriteLocal], needsForce,
            [SpecificationManifest.SkillDirectory, ManagedScope.SkillsLockDirectory]));
        risks.Add("A instalação da estrutura de especificação baixa conteúdo da rede e executa o pacote npm oficial; o resultado é validado (arquivos, hash e scripts) antes de a configuração ser dada como pronta.");
    }
}
