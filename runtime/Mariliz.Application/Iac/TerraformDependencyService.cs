using System.Text.Json;
using System.Text.Json.Nodes;
using Mariliz.Application.Execution;
using Mariliz.Application.Integrations;
using Mariliz.Application.Ports;
using Mariliz.Application.Workspace;
using Mariliz.Core.Text;

namespace Mariliz.Application.Iac;

// A ferramenta de infraestrutura como código (OpenTofu, o padrão, ou Terraform) é uma dependência que o Spock detecta,
// oferece e gerencia, sempre por decisão da pessoa: nada é instalado, atualizado, removido nem adotado sem um pedido
// explícito vindo da interface, e nenhum projeto muda de ferramenta ou de versão sozinho.
public sealed class TerraformDependencyService
{
    // O padrão quando a pessoa não escolhe: gratuito e de código aberto, sem licença a negociar.
    public const IacEngine PreferredEngine = IacEngine.OpenTofu;

    // Desliga a consulta de atualização que o Terraform faria à HashiCorp a cada execução (o OpenTofu não tem essa
    // consulta) e deixa as duas ferramentas sem pergunta interativa.
    public static readonly IReadOnlyDictionary<string, string> QuietEnvironment = new Dictionary<string, string>
    {
        ["CHECKPOINT_DISABLE"] = "1",
        ["TF_IN_AUTOMATION"] = "1",
        ["TF_INPUT"] = "0"
    };

    private static readonly TimeSpan ProbeTimeout = TimeSpan.FromSeconds(20);

    private readonly IReadOnlyDictionary<IacEngine, ITerraformToolchain> _toolchains;
    private readonly ITerraformSettingsStore _store;
    private readonly ITerraformConfigReader _config;
    private readonly IProcessRunner _runner;
    private readonly WorkspaceAuthorizer _workspace;
    private readonly IClock _clock;
    private readonly IAuditLog _audit;

    public TerraformDependencyService(IEnumerable<ITerraformToolchain> toolchains, ITerraformSettingsStore store, ITerraformConfigReader config,
        IProcessRunner runner, WorkspaceAuthorizer workspace, IClock clock, IAuditLog audit)
    {
        _toolchains = toolchains.ToDictionary(t => t.Engine);
        _store = store;
        _config = config;
        _runner = runner;
        _workspace = workspace;
        _clock = clock;
        _audit = audit;
    }

    // Blocos import e saída JSON estável de plan e show, que as próximas etapas usam. O OpenTofu nasceu na 1.6.
    public static TerraformVersion MinimumFor(IacEngine engine) => engine == IacEngine.OpenTofu ? new(1, 6, 0, null) : new(1, 5, 0, null);

    public static string NameOf(IacEngine engine) => engine == IacEngine.OpenTofu ? "OpenTofu" : "Terraform";

    public async Task<TerraformStatus> StatusAsync(string? projectId, CancellationToken ct)
    {
        var root = projectId is null ? null : RequireRoot(projectId);
        var settings = _store.Read();
        var installations = new List<TerraformInstallation>();
        foreach (var toolchain in Ordered())
        {
            installations.AddRange(Managed(toolchain));
            if (await ExternalAsync(toolchain, settings, ct) is { } external) installations.Add(external);
        }
        var usable = installations.Where(Usable).ToList();

        var project = projectId is null ? null : Project(projectId, root!, settings, usable);
        var selected = project is null ? Rank(usable).FirstOrDefault() : project.Selected;
        var waiting = installations.FirstOrDefault(i => i is { Source: TerraformSource.External, Compatible: true, Verified: true, Adopted: false });
        var platform = _toolchains.Values.Select(t => t.Platform).FirstOrDefault(p => p is not null);

        var state = selected is not null ? TerraformState.Ready
            : waiting is not null ? TerraformState.ExternalAvailable
            : platform is null ? TerraformState.UnsupportedPlatform
            : settings.DeclinedAt is not null ? TerraformState.Declined
            : installations.Count > 0 ? TerraformState.Incompatible
            : TerraformState.Missing;
        return new TerraformStatus(state, platform, Engines(), installations, selected, project, settings.DeclinedAt,
            Message(state, selected, waiting, settings.DeclinedAt, project));
    }

    public async Task<TerraformOffer> OfferAsync(string? version, string? projectId, IacEngine? engine, CancellationToken ct) =>
        (await BuildOfferAsync(engine ?? PreferredEngine, version, projectId, ct)).Offer;

    public async Task<TerraformStatus> InstallAsync(string offerId, string version, IacEngine engine, CancellationToken ct)
    {
        var toolchain = Toolchain(engine);
        var (offer, release) = await BuildOfferAsync(engine, version, null, ct);
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
                _audit.Record("iac.terraform.install_failed", new JsonObject
                {
                    ["engine"] = EngineId(engine), ["version"] = offer.Version, ["platform"] = offer.Platform.Key, ["code"] = ex.Code
                });
                throw;
            }
            _audit.Record("iac.terraform.installed", new JsonObject
            {
                ["engine"] = EngineId(engine),
                ["version"] = installed.Version,
                ["platform"] = offer.Platform.Key,
                ["source"] = installed.SourceUrl,
                ["archiveSha256"] = installed.ArchiveSha256,
                ["signedBy"] = installed.SignedBy,
                ["update"] = offer.IsUpdate
            });
        }

        var settings = _store.Read();
        if (settings.DeclinedAt is not null) _store.Write(settings with { DeclinedAt = null });
        return await StatusAsync(null, ct);
    }

    // "Não instalar agora": vale até a pessoa pedir a instalação; nada tenta de novo em segundo plano.
    public async Task<TerraformStatus> DeclineAsync(CancellationToken ct)
    {
        _store.Write(_store.Read() with { DeclinedAt = _clock.UtcNow });
        _audit.Record("iac.terraform.declined", new JsonObject());
        return await StatusAsync(null, ct);
    }

    public async Task<TerraformStatus> RemoveAsync(string version, IacEngine engine, bool confirmed, CancellationToken ct)
    {
        if (!confirmed) throw IacErrors.ConfirmationRequired();
        var toolchain = Toolchain(engine);
        if (!toolchain.Managed().Any(m => m.Version == version)) throw IacErrors.NotInstalled(NameOf(engine), version);
        var pinnedBy = _store.Read().Pins
            .Where(p => p.Value.Engine == engine && p.Value.Source == TerraformSource.Managed && p.Value.Version == version)
            .Select(p => DisplayName(p.Key))
            .ToList();
        if (pinnedBy.Count > 0) throw IacErrors.InUse(NameOf(engine), version, pinnedBy);

        toolchain.Remove(version);
        _audit.Record("iac.terraform.removed", new JsonObject { ["engine"] = EngineId(engine), ["version"] = version });
        return await StatusAsync(null, ct);
    }

    // Usar a instalação que a pessoa já tem, em vez de instalar outra. Guarda o hash: se o binário mudar, a escolha cai.
    // A pessoa confirma a versão que viu na tela; se o binário encontrado agora responde outra, nada é adotado.
    public async Task<TerraformStatus> AdoptAsync(string expectedVersion, IacEngine engine, CancellationToken ct)
    {
        var toolchain = Toolchain(engine);
        var found = toolchain.FindExternal() ?? throw IacErrors.ExternalNotFound(NameOf(engine));
        var version = await ProbeAsync(found, ct) ?? throw IacErrors.ExternalNotFound(NameOf(engine));
        if (version.ToString() != expectedVersion) throw IacErrors.ExternalChanged(NameOf(engine));
        if (version < MinimumFor(engine)) throw IacErrors.BelowMinimum(NameOf(engine), version.ToString(), MinimumFor(engine).ToString());

        var settings = _store.Read();
        var adopted = settings.Adopted.Where(a => !SamePath(a.Path, found)).ToList();
        adopted.Add(new AdoptedTerraform(found, version.ToString(), toolchain.HashFile(found), _clock.UtcNow, engine));
        _store.Write(settings with { Adopted = adopted });
        _audit.Record("iac.terraform.adopted", new JsonObject { ["engine"] = EngineId(engine), ["version"] = version.ToString() });
        return await StatusAsync(null, ct);
    }

    // Fixa ferramenta e versão do projeto; sem versão volta para a escolha automática (OpenTofu antes de Terraform, a mais
    // nova compatível).
    public async Task<TerraformStatus> PinAsync(string projectId, string? version, TerraformSource? source, IacEngine? engine, CancellationToken ct)
    {
        RequireRoot(projectId);
        var settings = _store.Read();
        if (version is null)
        {
            if (settings.Pins.Remove(projectId))
            {
                _store.Write(settings);
                _audit.Record("iac.terraform.unpinned", new JsonObject { ["projectId"] = projectId });
            }
            return await StatusAsync(projectId, ct);
        }

        var wanted = engine ?? PreferredEngine;
        var status = await StatusAsync(projectId, ct);
        var candidate = status.Installations
            .Where(i => i.Engine == wanted && i.Version == version && Usable(i))
            .Where(i => source is null || i.Source == source)
            .OrderBy(i => i.Source)
            .FirstOrDefault() ?? throw IacErrors.NotAvailable($"O {NameOf(wanted)} {version} não está instalado nem adotado nesta máquina.");
        if (Unmet(status.Project!.Requirements, TerraformVersion.Parse(version)) is { } constraint)
            throw IacErrors.ProjectIncompatible(NameOf(wanted), version, constraint);

        Pin(projectId, settings, candidate, automatic: false);
        return await StatusAsync(projectId, ct);
    }

    // A porta das próximas etapas (validate, plan, apply): devolve o binário que o projeto usa e confere a integridade dele
    // de novo. Na primeira vez a escolha automática vira fixa, para que uma instalação nova não troque a ferramenta nem a
    // versão de um projeto que já rodou.
    public async Task<TerraformInstallation> ResolveForUseAsync(string projectId, CancellationToken ct)
    {
        var status = await StatusAsync(projectId, ct);
        var selected = status.Project?.Selected ?? throw IacErrors.NotAvailable(status.Project?.Problem ?? status.Message);
        var toolchain = Toolchain(selected.Engine);
        if (selected.Source == TerraformSource.Managed)
        {
            var managed = toolchain.Managed().First(m => m.Version == selected.Version);
            if (!toolchain.Intact(managed))
                throw IacErrors.NotAvailable($"O binário do {NameOf(selected.Engine)} {selected.Version} não confere com o que foi instalado. Instale a versão de novo pela tela Infraestrutura.");
        }
        else if (_store.Read().Adopted.FirstOrDefault(a => a.Engine == selected.Engine && SamePath(a.Path, selected.Path)) is not { } adopted
                 || toolchain.HashFile(selected.Path) != adopted.BinarySha256)
        {
            throw IacErrors.ExternalChanged(NameOf(selected.Engine));
        }

        if (status.Project!.Automatic) Pin(projectId, _store.Read(), selected, automatic: true);
        return selected;
    }

    private async Task<(TerraformOffer Offer, TerraformRelease Release)> BuildOfferAsync(IacEngine engine, string? version, string? projectId,
        CancellationToken ct)
    {
        var toolchain = Toolchain(engine);
        var name = NameOf(engine);
        var minimum = MinimumFor(engine);
        var platform = toolchain.Platform ?? throw IacErrors.UnsupportedPlatform(name);
        var requirements = projectId is null ? [] : _config.Requirements(RequireRoot(projectId));
        var wanted = version is null ? null : TerraformVersion.TryParse(version) ?? throw IacErrors.VersionInvalid(name, version);
        if (wanted is { IsPrerelease: true }) throw IacErrors.VersionNotPublished(name, version!);
        if (wanted is not null && wanted < minimum) throw IacErrors.BelowMinimum(name, wanted.ToString(), minimum.ToString());

        TerraformRelease? release;
        try
        {
            release = wanted is not null
                ? await toolchain.ReleaseAsync(wanted.ToString(), ct) ?? throw IacErrors.VersionNotPublished(name, wanted.ToString())
                : (await toolchain.ReleasesAsync(ct))
                    .Where(r => TerraformVersion.TryParse(r.Version) is { IsPrerelease: false } v && v >= minimum && Unmet(requirements, v) is null)
                    .OrderByDescending(r => TerraformVersion.Parse(r.Version))
                    .FirstOrDefault() ?? throw IacErrors.NoCompatibleRelease(name);
        }
        catch (Exception ex) when ((ex is HttpRequestException or IOException or JsonException || ex is OperationCanceledException) && !ct.IsCancellationRequested)
        {
            throw IacErrors.DownloadFailed(name);
        }

        var chosen = TerraformVersion.Parse(release.Version);
        var newest = toolchain.Managed().Select(m => TerraformVersion.Parse(m.Version)).OrderDescending().FirstOrDefault();
        var isUpdate = newest is not null && chosen > newest;
        var impacts = Impacts(toolchain, chosen, newest, requirements).ToList();
        if (newest is null && await CompatibleElsewhereAsync(requirements, ct) is { } existing)
            impacts.Insert(0, $"Já existe o {NameOf(existing.Engine)} {existing.Version} compatível nesta máquina" +
                (existing.Source == TerraformSource.External ? ", instalado fora do Spock" : "") + ". Usá-lo dispensa esta instalação.");
        var offerId = Hashing.Sha256(string.Join('|', EngineId(engine), release.Version, platform.Key, release.ArchiveUrl, release.ShasumsUrl,
            release.SignatureUrl, toolchain.SigningKey))[..24];
        return (new TerraformOffer(offerId, engine, name, toolchain.License, release.Version, platform, release.ArchiveUrl, release.ShasumsUrl,
            release.SignatureUrl, toolchain.SigningKey, release.PublishedAt, release.ChangelogUrl, newest?.ToString(), isUpdate, impacts), release);
    }

    private IEnumerable<string> Impacts(ITerraformToolchain toolchain, TerraformVersion chosen, TerraformVersion? newest,
        IReadOnlyList<TerraformRequirement> requirements)
    {
        var name = NameOf(toolchain.Engine);
        if (toolchain.Managed().Any(m => TerraformVersion.Parse(m.Version).CompareTo(chosen) == 0 && toolchain.Intact(m)))
        {
            yield return $"O {name} {chosen} já está instalado pelo Spock; nada será baixado.";
            yield break;
        }
        yield return $"Licença {toolchain.License}." + (toolchain.Engine == IacEngine.OpenTofu
            ? " Gratuito e de código aberto, sem licença a contratar."
            : " Uso gratuito na sua própria infraestrutura; oferecer o Terraform como serviço a terceiros pede revisão da licença.");
        yield return "A instalação fica na pasta de dados do Spock, sem mexer no PATH da máquina, ao lado das versões que já existirem.";
        if (newest is not null && chosen > newest)
        {
            yield return $"A versão atual ({newest}) continua instalada. Nenhum projeto troca de versão sozinho: cada um passa para {chosen} só quando você fixar.";
            if (chosen.Major != newest.Major || chosen.Minor != newest.Minor)
                yield return $"Muda a versão menor ({newest.Major}.{newest.Minor} para {chosen.Major}.{chosen.Minor}): leia o changelog antes de fixar a versão nova num projeto.";
            foreach (var group in _store.Read().Pins.Where(p => p.Value.Engine == toolchain.Engine).GroupBy(p => p.Value.Version).OrderBy(g => g.Key, StringComparer.Ordinal))
                yield return $"Fixados no {name} {group.Key}: {string.Join(", ", group.Select(p => DisplayName(p.Key)))}.";
        }
        if (Unmet(requirements, chosen) is { } constraint)
            yield return $"O projeto exige versão {constraint}; esta não poderá ser fixada nele.";
    }

    // Qualquer instalação já utilizável ou à espera de adoção que atenda ao projeto: a instalação existente vence.
    private async Task<TerraformInstallation?> CompatibleElsewhereAsync(IReadOnlyList<TerraformRequirement> requirements, CancellationToken ct)
    {
        var settings = _store.Read();
        foreach (var toolchain in Ordered())
        {
            var candidates = Managed(toolchain).Where(Usable).ToList();
            if (await ExternalAsync(toolchain, settings, ct) is { Compatible: true, Verified: true } external) candidates.Add(external);
            if (candidates.FirstOrDefault(i => Unmet(requirements, TerraformVersion.Parse(i.Version)) is null) is { } found) return found;
        }
        return null;
    }

    private ProjectTerraform Project(string projectId, string root, TerraformSettings settings, IReadOnlyList<TerraformInstallation> usable)
    {
        var requirements = _config.Requirements(root);
        var invalid = requirements.FirstOrDefault(r => !r.Valid);
        var compatible = Rank(usable.Where(i => Unmet(requirements, TerraformVersion.Parse(i.Version)) is null)).ToList();

        if (settings.Pins.TryGetValue(projectId, out var pin))
        {
            var match = usable.FirstOrDefault(i => i.Engine == pin.Engine && i.Version == pin.Version && i.Source == pin.Source);
            var problem = match is null
                ? $"O projeto está fixado no {NameOf(pin.Engine)} {pin.Version}, que não está disponível nesta máquina."
                : Unmet(requirements, TerraformVersion.Parse(pin.Version)) is { } constraint
                    ? $"O projeto exige versão {constraint}, mas está fixado no {NameOf(pin.Engine)} {pin.Version}."
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

    // OpenTofu antes de Terraform; dentro da ferramenta, gerenciada antes de externa e a mais nova primeiro.
    private static IEnumerable<TerraformInstallation> Rank(IEnumerable<TerraformInstallation> installations) =>
        installations
            .OrderBy(i => i.Engine == PreferredEngine ? 0 : 1)
            .ThenByDescending(i => TerraformVersion.Parse(i.Version))
            .ThenBy(i => i.Source);

    private static bool Usable(TerraformInstallation i) =>
        i.Compatible && i.Verified && (i.Source == TerraformSource.Managed || i.Adopted);

    private List<IacEngineInfo> Engines() =>
        Ordered().Select(t => new IacEngineInfo(t.Engine, NameOf(t.Engine), t.License, MinimumFor(t.Engine).ToString(), t.Engine == PreferredEngine,
            t.Platform is not null)).ToList();

    private IEnumerable<ITerraformToolchain> Ordered() =>
        _toolchains.Values.OrderBy(t => t.Engine == PreferredEngine ? 0 : 1);

    private List<TerraformInstallation> Managed(ITerraformToolchain toolchain) =>
        toolchain.Managed()
            .Select(m =>
            {
                var version = TerraformVersion.TryParse(m.Version);
                var intact = toolchain.Intact(m);
                var minimum = MinimumFor(toolchain.Engine);
                var compatible = version is not null && version >= minimum;
                var problem = !intact ? "O binário não confere com o hash registrado na instalação."
                    : !compatible ? $"Abaixo do mínimo do Spock ({minimum})." : null;
                return new TerraformInstallation(toolchain.Engine, TerraformSource.Managed, m.Version, m.Path, compatible, intact, false, problem, m.InstalledAt);
            })
            .OrderByDescending(i => TerraformVersion.TryParse(i.Version) ?? new TerraformVersion(0, 0, 0, null))
            .ToList();

    private async Task<TerraformInstallation?> ExternalAsync(ITerraformToolchain toolchain, TerraformSettings settings, CancellationToken ct)
    {
        var path = toolchain.FindExternal();
        if (path is null) return null;
        var engine = toolchain.Engine;
        var version = await ProbeAsync(path, ct);
        if (version is null)
            return new TerraformInstallation(engine, TerraformSource.External, "desconhecida", path, false, false, false, "Não respondeu ao pedido de versão.", null);

        var minimum = MinimumFor(engine);
        var compatible = version >= minimum;
        var adoption = settings.Adopted.FirstOrDefault(a => a.Engine == engine && SamePath(a.Path, path));
        var adopted = adoption is not null && adoption.Version == version.ToString() && toolchain.HashFile(path) == adoption.BinarySha256;
        var problem = !compatible ? $"Abaixo do mínimo do Spock ({minimum})."
            : adoption is not null && !adopted ? "O binário mudou desde que você escolheu usá-lo. Confirme de novo."
            : null;
        return new TerraformInstallation(engine, TerraformSource.External, version.ToString(), path, compatible, true, adopted, problem, adoption?.AdoptedAt);
    }

    // `version -json` só imprime a versão e a plataforma. As duas ferramentas usam a chave terraform_version; com
    // CHECKPOINT_DISABLE o Terraform não fala com a rede.
    private async Task<TerraformVersion?> ProbeAsync(string path, CancellationToken ct)
    {
        var result = await _runner.RunAsync(new ProcessSpec(path, ["version", "-json"], Path.GetTempPath(), ProbeTimeout,
            Environment: QuietEnvironment), ct);
        if (result.NotFound || result.TimedOut || result.ExitCode != 0) return null;
        try
        {
            var json = JsonNode.Parse(result.StandardOutput);
            var text = json?["terraform_version"]?.GetValue<string>() ?? json?["opentofu_version"]?.GetValue<string>();
            return text is null ? null : TerraformVersion.TryParse(text);
        }
        catch (Exception ex) when (ex is JsonException or InvalidOperationException or FormatException)
        {
            return null;
        }
    }

    private void Pin(string projectId, TerraformSettings settings, TerraformInstallation installation, bool automatic)
    {
        settings.Pins[projectId] = new ProjectTerraformPin(installation.Version, installation.Source, _clock.UtcNow, installation.Engine);
        _store.Write(settings);
        _audit.Record("iac.terraform.pinned", new JsonObject
        {
            ["projectId"] = projectId,
            ["engine"] = EngineId(installation.Engine),
            ["version"] = installation.Version,
            ["source"] = installation.Source == TerraformSource.Managed ? "managed" : "external",
            ["automatic"] = automatic
        });
    }

    private ITerraformToolchain Toolchain(IacEngine engine) =>
        _toolchains.TryGetValue(engine, out var toolchain) ? toolchain : throw IacErrors.UnsupportedPlatform(NameOf(engine));

    public static string EngineId(IacEngine engine) => engine == IacEngine.OpenTofu ? "openTofu" : "terraform";

    private static string? Unmet(IReadOnlyList<TerraformRequirement> requirements, TerraformVersion version) =>
        requirements.Where(r => r.Valid).Select(r => VersionConstraint.TryParse(r.Constraint))
            .FirstOrDefault(c => c is not null && !c.IsSatisfiedBy(version))?.Text;

    private static string Message(TerraformState state, TerraformInstallation? selected, TerraformInstallation? waiting, DateTimeOffset? declinedAt,
        ProjectTerraform? project) => state switch
    {
        TerraformState.Ready => $"{NameOf(selected!.Engine)} {selected.Version} pronto, " +
            (selected.Source == TerraformSource.Managed ? "gerenciado pelo Spock." : "instalado fora do Spock e escolhido por você."),
        TerraformState.ExternalAvailable => $"Há um {NameOf(waiting!.Engine)} {waiting.Version} instalado nesta máquina. O Spock pode usá-lo sem instalar outro, se você confirmar.",
        TerraformState.Declined => $"Instalação recusada em {declinedAt:dd/MM/yyyy}. A gestão de infraestrutura como código fica indisponível até você pedir a instalação.",
        TerraformState.Incompatible => project?.Problem ?? "As instalações encontradas não atendem ao mínimo do Spock.",
        TerraformState.UnsupportedPlatform => "Não há pacote oficial do OpenTofu nem do Terraform para o sistema e a arquitetura desta máquina.",
        _ => "Nenhuma ferramenta de infraestrutura como código instalada. O Spock pode instalar o OpenTofu, gratuito, com a sua autorização."
    };

    private string RequireRoot(string projectId) =>
        _workspace.TryGetRoot(projectId, out var root) ? root : throw EngineErrors.ProjectNotAuthorized();

    private string DisplayName(string projectId) =>
        _workspace.Projects().FirstOrDefault(p => p.ProjectId == projectId)?.DisplayName ?? projectId;

    private static bool SamePath(string left, string right) =>
        string.Equals(Path.GetFullPath(left), Path.GetFullPath(right), OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal);
}
