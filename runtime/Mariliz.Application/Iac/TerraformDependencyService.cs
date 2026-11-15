using System.Text.Json;
using System.Text.Json.Nodes;
using Mariliz.Application.Execution;
using Mariliz.Application.Integrations;
using Mariliz.Application.Ports;
using Mariliz.Application.Workspace;
using Mariliz.Core.Text;

namespace Mariliz.Application.Iac;

// O Terraform é uma dependência que o Spock detecta, oferece e gerencia, sempre por decisão da pessoa: nada é instalado,
// atualizado, removido nem adotado sem um pedido explícito vindo da interface, e nenhum projeto muda de versão sozinho.
public sealed class TerraformDependencyService(
    ITerraformToolchain toolchain,
    ITerraformSettingsStore store,
    ITerraformConfigReader config,
    IProcessRunner runner,
    WorkspaceAuthorizer workspace,
    IClock clock,
    IAuditLog audit)
{
    // Blocos import e saída JSON estável de plan e show, que as próximas etapas usam.
    public static readonly TerraformVersion Minimum = new(1, 5, 0, null);

    // Desliga a consulta de atualização que o binário faria à HashiCorp a cada execução.
    public static readonly IReadOnlyDictionary<string, string> QuietEnvironment = new Dictionary<string, string>
    {
        ["CHECKPOINT_DISABLE"] = "1",
        ["TF_IN_AUTOMATION"] = "1",
        ["TF_INPUT"] = "0"
    };

    private static readonly TimeSpan ProbeTimeout = TimeSpan.FromSeconds(20);

    public async Task<TerraformStatus> StatusAsync(string? projectId, CancellationToken ct)
    {
        var root = projectId is null ? null : RequireRoot(projectId);
        var settings = store.Read();
        var managed = Managed();
        var external = await ExternalAsync(settings, ct);
        var installations = managed.Concat(external is null ? [] : [external]).ToList();
        var usable = installations.Where(i => i.Compatible && i.Verified && (i.Source == TerraformSource.Managed || i.Adopted)).ToList();

        var project = projectId is null ? null : Project(projectId, root!, settings, usable);
        var selected = project is null ? usable.OrderByDescending(i => TerraformVersion.Parse(i.Version)).FirstOrDefault() : project.Selected;

        var state = selected is not null ? TerraformState.Ready
            : external is { Compatible: true, Verified: true, Adopted: false } ? TerraformState.ExternalAvailable
            : toolchain.Platform is null ? TerraformState.UnsupportedPlatform
            : settings.DeclinedAt is not null ? TerraformState.Declined
            : installations.Count > 0 ? TerraformState.Incompatible
            : TerraformState.Missing;
        return new TerraformStatus(state, toolchain.Platform, Minimum.ToString(), installations, selected, project, settings.DeclinedAt,
            Message(state, selected, external, settings.DeclinedAt, project));
    }

    public async Task<TerraformOffer> OfferAsync(string? version, string? projectId, CancellationToken ct) =>
        (await BuildOfferAsync(version, projectId, ct)).Offer;

    public async Task<TerraformStatus> InstallAsync(string offerId, string version, CancellationToken ct)
    {
        var (offer, release) = await BuildOfferAsync(version, null, ct);
        if (!string.Equals(offer.OfferId, offerId, StringComparison.Ordinal)) throw IacErrors.OfferStale();

        // Uma versão que já existe só é baixada de novo se falhou na verificação: é o caminho para consertar um binário alterado.
        if (toolchain.Managed().FirstOrDefault(m => m.Version == offer.Version) is not { } existing || !toolchain.Intact(existing))
        {
            ManagedTerraform installed;
            try
            {
                installed = await toolchain.InstallAsync(release, ct);
            }
            catch (EngineException ex)
            {
                audit.Record("iac.terraform.install_failed", new JsonObject { ["version"] = offer.Version, ["platform"] = offer.Platform.Key, ["code"] = ex.Code });
                throw;
            }
            audit.Record("iac.terraform.installed", new JsonObject
            {
                ["version"] = installed.Version,
                ["platform"] = offer.Platform.Key,
                ["source"] = installed.SourceUrl,
                ["archiveSha256"] = installed.ArchiveSha256,
                ["signedBy"] = installed.SignedBy,
                ["update"] = offer.IsUpdate
            });
        }

        var settings = store.Read();
        if (settings.DeclinedAt is not null) store.Write(settings with { DeclinedAt = null });
        return await StatusAsync(null, ct);
    }

    // "Não instalar agora": vale até a pessoa pedir a instalação; nada tenta de novo em segundo plano.
    public async Task<TerraformStatus> DeclineAsync(CancellationToken ct)
    {
        store.Write(store.Read() with { DeclinedAt = clock.UtcNow });
        audit.Record("iac.terraform.declined", new JsonObject());
        return await StatusAsync(null, ct);
    }

    public async Task<TerraformStatus> RemoveAsync(string version, bool confirmed, CancellationToken ct)
    {
        if (!confirmed) throw IacErrors.ConfirmationRequired();
        if (!toolchain.Managed().Any(m => m.Version == version)) throw IacErrors.NotInstalled(version);
        var pinnedBy = store.Read().Pins
            .Where(p => p.Value.Source == TerraformSource.Managed && p.Value.Version == version)
            .Select(p => DisplayName(p.Key))
            .ToList();
        if (pinnedBy.Count > 0) throw IacErrors.InUse(version, pinnedBy);

        toolchain.Remove(version);
        audit.Record("iac.terraform.removed", new JsonObject { ["version"] = version });
        return await StatusAsync(null, ct);
    }

    // Usar o Terraform que a pessoa já tem, em vez de instalar outro. Guarda o hash: se o binário mudar, a escolha cai.
    // A pessoa confirma a versão que viu na tela; se o binário encontrado agora responde outra, nada é adotado.
    public async Task<TerraformStatus> AdoptAsync(string expectedVersion, CancellationToken ct)
    {
        var found = toolchain.FindExternal() ?? throw IacErrors.ExternalNotFound();
        var version = await ProbeAsync(found, ct) ?? throw IacErrors.ExternalNotFound();
        if (version.ToString() != expectedVersion) throw IacErrors.ExternalChanged();
        if (version < Minimum) throw IacErrors.BelowMinimum(version.ToString(), Minimum.ToString());

        var settings = store.Read();
        var adopted = settings.Adopted.Where(a => !SamePath(a.Path, found)).ToList();
        adopted.Add(new AdoptedTerraform(found, version.ToString(), toolchain.HashFile(found), clock.UtcNow));
        store.Write(settings with { Adopted = adopted });
        audit.Record("iac.terraform.adopted", new JsonObject { ["version"] = version.ToString() });
        return await StatusAsync(null, ct);
    }

    // Fixa a versão do projeto; null volta para a escolha automática (a mais nova compatível).
    public async Task<TerraformStatus> PinAsync(string projectId, string? version, TerraformSource? source, CancellationToken ct)
    {
        RequireRoot(projectId);
        var settings = store.Read();
        if (version is null)
        {
            if (settings.Pins.Remove(projectId))
            {
                store.Write(settings);
                audit.Record("iac.terraform.unpinned", new JsonObject { ["projectId"] = projectId });
            }
            return await StatusAsync(projectId, ct);
        }

        var status = await StatusAsync(projectId, ct);
        var candidate = status.Installations
            .Where(i => i.Version == version && i.Verified && (i.Source == TerraformSource.Managed || i.Adopted))
            .Where(i => source is null || i.Source == source)
            .OrderBy(i => i.Source)
            .FirstOrDefault() ?? throw IacErrors.NotAvailable($"O Terraform {version} não está instalado nem adotado nesta máquina.");
        var parsed = TerraformVersion.Parse(version);
        if (parsed < Minimum) throw IacErrors.BelowMinimum(version, Minimum.ToString());
        if (Unmet(status.Project!.Requirements, parsed) is { } constraint) throw IacErrors.ProjectIncompatible(version, constraint);

        Pin(projectId, settings, candidate, automatic: false);
        return await StatusAsync(projectId, ct);
    }

    // A porta das próximas etapas (validate, plan, apply): devolve o binário que o projeto usa e confere a integridade dele
    // de novo. Na primeira vez a escolha automática vira versão fixada, para que uma instalação nova não troque a versão
    // de um projeto que já rodou.
    public async Task<TerraformInstallation> ResolveForUseAsync(string projectId, CancellationToken ct)
    {
        var status = await StatusAsync(projectId, ct);
        var selected = status.Project?.Selected ?? throw IacErrors.NotAvailable(status.Project?.Problem ?? status.Message);
        if (selected.Source == TerraformSource.Managed)
        {
            var managed = toolchain.Managed().First(m => m.Version == selected.Version);
            if (!toolchain.Intact(managed)) throw IacErrors.NotAvailable($"O binário do Terraform {selected.Version} não confere com o que foi instalado. Instale a versão de novo pela tela Infraestrutura.");
        }
        else if (store.Read().Adopted.FirstOrDefault(a => SamePath(a.Path, selected.Path)) is not { } adopted
                 || toolchain.HashFile(selected.Path) != adopted.BinarySha256)
        {
            throw IacErrors.ExternalChanged();
        }

        if (status.Project!.Automatic) Pin(projectId, store.Read(), selected, automatic: true);
        return selected;
    }

    private async Task<(TerraformOffer Offer, TerraformRelease Release)> BuildOfferAsync(string? version, string? projectId, CancellationToken ct)
    {
        var platform = toolchain.Platform ?? throw IacErrors.UnsupportedPlatform();
        var requirements = projectId is null ? [] : config.Requirements(RequireRoot(projectId));
        var wanted = version is null ? null : TerraformVersion.TryParse(version) ?? throw IacErrors.VersionInvalid(version);
        if (wanted is { IsPrerelease: true }) throw IacErrors.VersionNotPublished(version!);
        if (wanted is not null && wanted < Minimum) throw IacErrors.BelowMinimum(wanted.ToString(), Minimum.ToString());

        TerraformRelease? release;
        try
        {
            release = wanted is not null
                ? await toolchain.ReleaseAsync(wanted.ToString(), ct) ?? throw IacErrors.VersionNotPublished(wanted.ToString())
                : (await toolchain.ReleasesAsync(ct))
                    .Where(r => TerraformVersion.TryParse(r.Version) is { IsPrerelease: false } v && v >= Minimum && Unmet(requirements, v) is null)
                    .OrderByDescending(r => TerraformVersion.Parse(r.Version))
                    .FirstOrDefault() ?? throw IacErrors.NoCompatibleRelease();
        }
        catch (Exception ex) when ((ex is HttpRequestException or IOException or JsonException || ex is OperationCanceledException) && !ct.IsCancellationRequested)
        {
            throw IacErrors.DownloadFailed();
        }

        var chosen = TerraformVersion.Parse(release.Version);
        var newest = toolchain.Managed().Select(m => TerraformVersion.Parse(m.Version)).OrderDescending().FirstOrDefault();
        var isUpdate = newest is not null && chosen > newest;
        var impacts = Impacts(chosen, newest, requirements).ToList();
        if (newest is null && await ExternalAsync(store.Read(), ct) is { Compatible: true, Verified: true } external && Unmet(requirements, TerraformVersion.Parse(external.Version)) is null)
            impacts.Insert(0, $"Já existe o Terraform {external.Version} compatível nesta máquina, instalado fora do Spock. Usá-lo dispensa esta instalação.");
        var offerId = Hashing.Sha256(string.Join('|', release.Version, platform.Key, release.ArchiveUrl, release.ShasumsUrl, release.SignatureUrl, toolchain.SigningKey))[..24];
        return (new TerraformOffer(offerId, release.Version, platform, release.ArchiveUrl, release.ShasumsUrl, release.SignatureUrl, toolchain.SigningKey,
            release.PublishedAt, release.ChangelogUrl, newest?.ToString(), isUpdate, impacts), release);
    }

    private IEnumerable<string> Impacts(TerraformVersion chosen, TerraformVersion? newest, IReadOnlyList<TerraformRequirement> requirements)
    {
        if (toolchain.Managed().Any(m => TerraformVersion.Parse(m.Version).CompareTo(chosen) == 0 && toolchain.Intact(m)))
        {
            yield return $"O Terraform {chosen} já está instalado pelo Spock; nada será baixado.";
            yield break;
        }
        yield return "A instalação fica na pasta de dados do Spock, sem mexer no PATH da máquina, ao lado das versões que já existirem.";
        if (newest is not null && chosen > newest)
        {
            yield return $"A versão atual ({newest}) continua instalada. Nenhum projeto troca de versão sozinho: cada um passa para {chosen} só quando você fixar.";
            if (chosen.Major != newest.Major || chosen.Minor != newest.Minor)
                yield return $"Muda a versão menor ({newest.Major}.{newest.Minor} para {chosen.Major}.{chosen.Minor}): leia o changelog antes de fixar a versão nova num projeto.";
            foreach (var group in store.Read().Pins.GroupBy(p => p.Value.Version).OrderBy(g => g.Key, StringComparer.Ordinal))
                yield return $"Fixados no Terraform {group.Key}: {string.Join(", ", group.Select(p => DisplayName(p.Key)))}.";
        }
        if (Unmet(requirements, chosen) is { } constraint)
            yield return $"O projeto exige Terraform {constraint}; esta versão não poderá ser fixada nele.";
    }

    private ProjectTerraform Project(string projectId, string root, TerraformSettings settings, IReadOnlyList<TerraformInstallation> usable)
    {
        var requirements = config.Requirements(root);
        var invalid = requirements.FirstOrDefault(r => !r.Valid);
        var compatible = usable.Where(i => Unmet(requirements, TerraformVersion.Parse(i.Version)) is null)
            .OrderByDescending(i => TerraformVersion.Parse(i.Version))
            .ThenBy(i => i.Source)
            .ToList();

        if (settings.Pins.TryGetValue(projectId, out var pin))
        {
            var match = usable.FirstOrDefault(i => i.Version == pin.Version && i.Source == pin.Source);
            var problem = match is null
                ? $"O projeto está fixado no Terraform {pin.Version}, que não está disponível nesta máquina."
                : Unmet(requirements, TerraformVersion.Parse(pin.Version)) is { } constraint
                    ? $"O projeto exige Terraform {constraint}, mas está fixado em {pin.Version}."
                    : null;
            return new ProjectTerraform(projectId, requirements, pin, problem is null ? match : null, false, problem);
        }

        var selected = compatible.FirstOrDefault();
        var reason = selected is not null ? null
            : invalid is not null ? $"Não foi possível ler a exigência de versão em {invalid.File}: {invalid.Constraint}."
            : requirements.Count > 0 && usable.Count > 0 ? $"Nenhuma instalação atende ao que o projeto exige ({string.Join("; ", requirements.Select(r => r.Constraint))})."
            : null;
        return new ProjectTerraform(projectId, requirements, null, selected, selected is not null, reason);
    }

    private List<TerraformInstallation> Managed() =>
        toolchain.Managed()
            .Select(m =>
            {
                var version = TerraformVersion.TryParse(m.Version);
                var intact = toolchain.Intact(m);
                var compatible = version is not null && version >= Minimum;
                var problem = !intact ? "O binário não confere com o hash registrado na instalação."
                    : !compatible ? $"Abaixo do mínimo do Spock ({Minimum})." : null;
                return new TerraformInstallation(TerraformSource.Managed, m.Version, m.Path, compatible, intact, false, problem, m.InstalledAt);
            })
            .OrderByDescending(i => TerraformVersion.TryParse(i.Version) ?? new TerraformVersion(0, 0, 0, null))
            .ToList();

    private async Task<TerraformInstallation?> ExternalAsync(TerraformSettings settings, CancellationToken ct)
    {
        var path = toolchain.FindExternal();
        if (path is null) return null;
        var version = await ProbeAsync(path, ct);
        if (version is null)
            return new TerraformInstallation(TerraformSource.External, "desconhecida", path, false, false, false, "Não respondeu a terraform version.", null);

        var compatible = version >= Minimum;
        var adoption = settings.Adopted.FirstOrDefault(a => SamePath(a.Path, path));
        var adopted = adoption is not null && adoption.Version == version.ToString() && toolchain.HashFile(path) == adoption.BinarySha256;
        var problem = !compatible ? $"Abaixo do mínimo do Spock ({Minimum})."
            : adoption is not null && !adopted ? "O binário mudou desde que você escolheu usá-lo. Confirme de novo."
            : null;
        return new TerraformInstallation(TerraformSource.External, version.ToString(), path, compatible, true, adopted, problem, adoption?.AdoptedAt);
    }

    // `terraform version -json` só imprime a versão e a plataforma; com CHECKPOINT_DISABLE não fala com a rede.
    private async Task<TerraformVersion?> ProbeAsync(string path, CancellationToken ct)
    {
        var result = await runner.RunAsync(new ProcessSpec(path, ["version", "-json"], Path.GetTempPath(), ProbeTimeout,
            Environment: QuietEnvironment), ct);
        if (result.NotFound || result.TimedOut || result.ExitCode != 0) return null;
        try
        {
            return JsonNode.Parse(result.StandardOutput)?["terraform_version"]?.GetValue<string>() is { } text ? TerraformVersion.TryParse(text) : null;
        }
        catch (Exception ex) when (ex is JsonException or InvalidOperationException or FormatException)
        {
            return null;
        }
    }

    private void Pin(string projectId, TerraformSettings settings, TerraformInstallation installation, bool automatic)
    {
        settings.Pins[projectId] = new ProjectTerraformPin(installation.Version, installation.Source, clock.UtcNow);
        store.Write(settings);
        audit.Record("iac.terraform.pinned", new JsonObject
        {
            ["projectId"] = projectId,
            ["version"] = installation.Version,
            ["source"] = installation.Source == TerraformSource.Managed ? "managed" : "external",
            ["automatic"] = automatic
        });
    }

    private static string? Unmet(IReadOnlyList<TerraformRequirement> requirements, TerraformVersion version) =>
        requirements.Where(r => r.Valid).Select(r => VersionConstraint.TryParse(r.Constraint))
            .FirstOrDefault(c => c is not null && !c.IsSatisfiedBy(version))?.Text;

    private static string Message(TerraformState state, TerraformInstallation? selected, TerraformInstallation? external, DateTimeOffset? declinedAt,
        ProjectTerraform? project) => state switch
    {
        TerraformState.Ready => $"Terraform {selected!.Version} pronto, " + (selected.Source == TerraformSource.Managed ? "gerenciado pelo Spock." : "instalado fora do Spock e escolhido por você."),
        TerraformState.ExternalAvailable => $"Há um Terraform {external!.Version} instalado nesta máquina. O Spock pode usá-lo sem instalar outro, se você confirmar.",
        TerraformState.Declined => $"Instalação recusada em {declinedAt:dd/MM/yyyy}. A gestão de infraestrutura como código fica indisponível até você pedir a instalação.",
        TerraformState.Incompatible => project?.Problem ?? $"As instalações encontradas não atendem ao mínimo do Spock ({Minimum}).",
        TerraformState.UnsupportedPlatform => "A HashiCorp não publica o Terraform para o sistema e a arquitetura desta máquina.",
        _ => "O Terraform não está instalado. O Spock pode instalá-lo com a sua autorização."
    };

    private string RequireRoot(string projectId) =>
        workspace.TryGetRoot(projectId, out var root) ? root : throw EngineErrors.ProjectNotAuthorized();

    private string DisplayName(string projectId) =>
        workspace.Projects().FirstOrDefault(p => p.ProjectId == projectId)?.DisplayName ?? projectId;

    private static bool SamePath(string left, string right) =>
        string.Equals(Path.GetFullPath(left), Path.GetFullPath(right), OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal);
}
