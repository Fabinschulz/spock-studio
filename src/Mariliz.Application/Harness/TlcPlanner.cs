using Mariliz.Application.Discovery;
using Mariliz.Application.Ports;
using Mariliz.Core.Discovery;
using Mariliz.Core.Plan;

namespace Mariliz.Application.Harness;

/// <summary>Decide o que fazer com o TLC: manter, instalar, atualizar ou bloquear.</summary>
internal static class TlcPlanner
{
    /// <summary>Motivo pelo qual o instalador oficial não pode rodar com segurança; <c>Decision</c> é a frase da decisão, <c>Reason</c> a da limitação.</summary>
    private sealed record InstallerBlock(string Decision, string Reason);

    private static InstallerBlock? InstallerBlocker(DiscoveryReport report, IProjectFiles files)
    {
        if (report.Tools.Node is null)
            return new("Node.js não encontrado; o instalador oficial do TLC exige npx.", "Node.js (22 ou superior) não foi encontrado.");

        // O instalador oficial grava na pasta do package.json mais próximo, subindo a partir da pasta de trabalho.
        // Sem package.json no projeto, mas com um acima dele, a instalação cairia fora do projeto: nunca arriscamos isso.
        if (!files.FileExists("package.json") && files.NearestAncestorFolderWith("package.json") is { } outside)
            return new($"O instalador oficial instalaria o TLC na pasta '{outside}', acima do projeto.",
                $"o instalador oficial grava na pasta do package.json mais próximo, e aqui seria '{outside}', fora do projeto. " +
                "Crie um package.json na raiz do projeto, ou instale o TLC você mesmo na pasta do projeto, e planeje de novo.");

        return null;
    }

    private static void KeepInstalledTlc(TlcStatus tlc, List<ComponentDecision> decisions, List<Limitation> limitations)
    {
        decisions.Add(new(ComponentKind.Sdd, Catalog.SddTlc, ComponentDecisions.Keep, $"TLC {tlc.Version} instalado e íntegro (hash conferido com o lockfile do instalador).", DecisionOrigins.Existing, [TlcManifest.SkillDirectory]));
        if (!tlc.VersionTested)
            limitations.Add(new(LimitationCodes.TlcUntested, $"A versão {tlc.Version ?? "desconhecida"} do TLC não foi validada por esta fase; o funcionamento não é garantido."));
    }

    internal static void Plan(DiscoveryReport report, IProjectFiles files, PlanOptions options, List<ComponentDecision> decisions,
        List<PlannedCommand> commands, List<Limitation> limitations, List<string> risks)
    {
        var tlc = report.Tlc;
        var evidence = tlc.Problems.Take(3).ToList();
        var installed = tlc.State == TlcState.Installed;
        var outdated = installed && TlcManifest.IsOlderThanLatest(tlc.Version);

        // Instalação íntegra e atual: nada a fazer, e o plano continua idempotente.
        if (installed && !outdated && !options.UpdateTlc)
        {
            KeepInstalledTlc(tlc, decisions, limitations);
            return;
        }

        var blocker = InstallerBlocker(report, files);
        if (blocker is not null)
        {
            if (installed)
            {
                // O TLC atual continua utilizável: o impedimento só barra a atualização, e a pessoa fica sabendo.
                KeepInstalledTlc(tlc, decisions, limitations);
                limitations.Add(new(LimitationCodes.TlcOutdated,
                    (outdated ? $"O TLC {tlc.Version} é anterior à versão mais recente conhecida ({TlcManifest.LatestKnownVersion}). " : "A atualização do TLC foi pedida. ")
                    + "Ela não foi planejada: " + blocker.Reason));
            }
            else
            {
                decisions.Add(new(ComponentKind.Sdd, Catalog.SddTlc, ComponentDecisions.Blocked, blocker.Decision, DecisionOrigins.Baseline, []));
                limitations.Add(new(LimitationCodes.TlcBlocked, "O TLC não pôde ser instalado: " + blocker.Reason));
            }
            return;
        }

        var needsForce = tlc.State is TlcState.Drifted or TlcState.Invalid;
        if (needsForce && !options.AllowTlcForceReinstall)
        {
            decisions.Add(new(ComponentKind.Sdd, Catalog.SddTlc, ComponentDecisions.Blocked,
                tlc.State == TlcState.Drifted ? "A instalação do TLC foi alterada localmente; reinstalar sobrescreveria essas edições." : "A instalação do TLC está incompleta.",
                DecisionOrigins.Baseline, evidence));
            limitations.Add(new(LimitationCodes.TlcNeedsForce,
                "Reinstalar o TLC com --force sobrescreve as edições locais dele. Revise a divergência e, se for o caso, autorize a reinstalação ao planejar de novo."));
            return;
        }

        var args = needsForce ? TlcManifest.ForceInstallArguments : TlcManifest.InstallArguments;
        if (installed)
        {
            // O instalador substitui a pasta da skill inteira (remove arquivos abolidos pela versão nova). Como a instalação
            // está íntegra, não há edição local a perder.
            decisions.Add(new(ComponentKind.Sdd, Catalog.SddTlc, ComponentDecisions.Enable,
                outdated
                    ? $"O TLC {tlc.Version} é anterior à versão mais recente conhecida ({TlcManifest.LatestKnownVersion}): atualização pelo instalador oficial."
                    : "Atualização do TLC pedida: o instalador oficial traz a versão mais recente disponível.",
                DecisionOrigins.Baseline, [TlcManifest.SkillDirectory]));
            commands.Add(new PlannedCommand(
                "tlc-update", "Atualizar o TLC para a versão mais recente (instalador oficial)",
                "npx", args, [CommandEffect.Process, CommandEffect.Network, CommandEffect.WriteLocal], false,
                [TlcManifest.SkillDirectory, ".agents"]));
            risks.Add("A atualização do TLC baixa a versão mais recente da rede e substitui a pasta da skill; o resultado é validado (arquivos, hash e scripts) e, se falhar, a versão atual é restaurada.");
            return;
        }

        decisions.Add(new(ComponentKind.Sdd, Catalog.SddTlc, ComponentDecisions.Enable,
            needsForce ? "Reinstalação autorizada do TLC pelo instalador oficial." : "TLC ausente: instalação pelo instalador oficial (@tech-leads-club/agent-skills), sempre na versão mais recente.",
            DecisionOrigins.Baseline, evidence));
        commands.Add(new PlannedCommand(
            "tlc-install",
            needsForce ? "Reinstalar o TLC (sobrescreve edições locais da skill)" : "Instalar o TLC pelo instalador oficial",
            "npx", args, [CommandEffect.Process, CommandEffect.Network, CommandEffect.WriteLocal], needsForce,
            [TlcManifest.SkillDirectory, ".agents"]));
        risks.Add("A instalação do TLC baixa conteúdo da rede e executa o pacote npm oficial; o resultado é validado (arquivos, hash e scripts) antes de a configuração ser dada como pronta.");
    }
}
