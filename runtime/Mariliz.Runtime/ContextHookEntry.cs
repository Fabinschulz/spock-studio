using System.Text.Json;
using System.Text.Json.Nodes;
using Mariliz.Application.Context;
using Mariliz.Application.Mcp;
using Mariliz.Application.Ports;
using Mariliz.Application.Workspace;
using Mariliz.Infrastructure;
using Mariliz.Infrastructure.Integrations;

namespace Mariliz.Runtime;

// C2: o que os hooks da IDE chamam. "spock context session" no começo da sessão e "spock context prompt" a cada prompt. A saída
// padrão vira contexto da sessão, então só sai por ali o que é para a IDE. Nunca falha a sessão nem o prompt: o código é sempre 0.
internal static class ContextHookEntry
{
    public const string Command = "context";
    public const string AuditAction = "ide.bootstrap";
    public const string PromptAuditAction = "ide.prompt";

    public const string Usage = """
        Uso: spock context session [--project-dir <pasta>] [--budget <tokens>]
             spock context prompt [--project-dir <pasta>]

        session escreve na saída padrão o contexto de abertura do projeto da pasta (padrão: CLAUDE_PROJECT_DIR, o cwd que a IDE
        manda na entrada ou a pasta atual). prompt escreve um aviso curto só quando o contexto mudou desde o começo da sessão, uma
        fonte ficou velha ou o assunto pede uma ferramenta do Spock; no resto, nada. Os dois só leem o que o Spock Studio gravou.
        """;

    public static int Run(string[] args)
    {
        if (args is not [("session" or "prompt") and var mode, .. var options]) return UsageError("Use 'context session' ou 'context prompt'.");
        string? directory = null;
        int? budget = null;
        for (var index = 0; index < options.Length; index++)
        {
            var value = index + 1 < options.Length ? options[index + 1] : null;
            switch (options[index])
            {
                case "--project-dir" when !string.IsNullOrWhiteSpace(value):
                    directory = value;
                    break;
                case "--budget" when mode == "session" && int.TryParse(value, out var tokens):
                    budget = tokens;
                    break;
                default:
                    return UsageError($"Argumento desconhecido ou sem valor: {options[index]}.");
            }
            index++;
        }

        var input = HookInput.Read();
        directory ??= Environment.GetEnvironmentVariable("CLAUDE_PROJECT_DIR") ?? input.Cwd ?? Environment.CurrentDirectory;
        try
        {
            Console.Out.Write(mode == "session" ? Session(directory, budget, input) : Prompt(directory, input));
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            // Na abertura a mensagem vai para a IDE no lugar do contexto; no prompt, um erro não pode virar texto a cada pergunta.
            if (mode == "session")
                Console.Out.WriteLine($"Spock: o contexto do projeto não pôde ser montado agora ({ex.GetType().Name}). Chame spock_context_bootstrap pelo MCP.");
            else
                Console.Error.WriteLine($"Spock: o aviso do prompt não pôde ser montado ({ex.GetType().Name}).");
        }
        return 0;
    }

    private static string Session(string directory, int? budget, HookInput input)
    {
        var scope = Scope.Of(directory);
        if (scope.Project is not { } project)
            return "Spock: esta pasta não está aberta no Spock Studio. Abra-a no aplicativo para a IDE receber o contexto do projeto.\n";

        var package = scope.Compose(project, new TiktokenTokenizers(), budget);
        scope.Audit.Record(AuditAction, new JsonObject
        {
            ["projectId"] = project.ProjectId,
            ["contextVersion"] = package.ContextVersion,
            ["items"] = package.Items.Count,
            ["usedTokens"] = package.UsedTokens
        });
        // É a referência do hook por prompt: o que mudar depois disso vira aviso.
        if (input.SessionId is { } session) new FileIdeSessionStore(scope.DataRoot).Write(project.ProjectId, session, PromptSignalRules.Opened(package));
        return package.Text;
    }

    // Sem o id da sessão não há como saber o que já foi dito, e o aviso se repetiria a cada prompt: fica em silêncio. Pasta fora do
    // Spock também, porque o hook de sessão já avisou.
    private static string Prompt(string directory, HookInput input)
    {
        if (input.SessionId is not { } session) return "";
        var scope = Scope.Of(directory);
        if (scope.Project is not { } project) return "";

        // Todos os itens entram (orçamento máximo): o aviso olha a versão e o frescor, não o texto do pacote.
        var package = scope.Compose(project, new CharacterTokenizers(), ContextEngine.MaxBudget);
        var store = new FileIdeSessionStore(scope.DataRoot);
        var result = PromptSignalRules.Evaluate(package, input.Prompt, store.Read(project.ProjectId, session));
        store.Write(project.ProjectId, session, result.State);
        if (result.Lines.Count > 0)
            scope.Audit.Record(PromptAuditAction, new JsonObject
            {
                ["projectId"] = project.ProjectId,
                ["contextVersion"] = package.ContextVersion,
                ["signals"] = new JsonArray([.. result.Signals.Select(signal => (JsonNode)signal)])
            });
        return result.Text;
    }

    private sealed record Scope(string DataRoot, SystemClock Clock, WorkspaceAuthorizer Workspace, AuthorizedProject? Project, FileAuditLog Audit)
    {
        public static Scope Of(string directory)
        {
            var dataRoot = FileRunStore.DefaultDataRoot();
            var clock = new SystemClock();
            var workspace = new WorkspaceAuthorizer(new LocalWorkspaceFileSystem(), new FileWorkspaceRegistry(dataRoot));
            var all = workspace.Projects().Select(project => project.ProjectId).ToHashSet(StringComparer.Ordinal);
            // A pasta da IDE escolhe a cópia, como no MCP com --project-dir; nunca autoriza pasta nova.
            var project = new McpProjectResolver(workspace, Path.GetFullPath(directory), all).Current();
            return new Scope(dataRoot, clock, workspace, project, new FileAuditLog(dataRoot, clock));
        }

        public ContextPackage Compose(AuthorizedProject project, ITokenizers tokenizers, int? budget)
        {
            using var transport = new HttpsConnectorTransport();
            return ContextComposition.Engine(Workspace, DataRoot, Clock, Audit, transport, tokenizers)
                .Compose(project.ProjectId, ContextIntents.Bootstrap, budget);
        }
    }

    // Carregar a tabela do tiktoken triplicava o tempo do hook (medido em 2026-11-18), e o prompt não usa a contagem.
    private sealed class CharacterTokenizers : ITokenizers
    {
        public TokenCounter For(string? provider, string? model) => new(text => text.Length / 3, false, "caracteres/3");
    }

    // O JSON que a IDE manda na entrada do hook (Claude Code e Codex usam os mesmos nomes). Rodado à mão, sem entrada, fica vazio.
    private sealed record HookInput(string? SessionId, string? Prompt, string? Cwd)
    {
        private const int MaxSessionId = 200;
        private const int MaxPrompt = 20_000;
        private static readonly HookInput Empty = new(null, null, null);

        public static HookInput Read()
        {
            if (!Console.IsInputRedirected) return Empty;
            var reading = Task.Run(Console.In.ReadToEnd);
            if (!reading.Wait(TimeSpan.FromSeconds(2))) return Empty;
            try
            {
                // O pipe do PowerShell 5.1 manda um BOM antes do JSON (o mesmo caso do MCP, ADR-0009).
                if (JsonNode.Parse(reading.Result.TrimStart('﻿')) is not JsonObject root) return Empty;
                var session = Text(root, "session_id");
                var prompt = Text(root, "prompt");
                return new HookInput(
                    session is { Length: > 0 and <= MaxSessionId } ? session : null,
                    prompt is { Length: > MaxPrompt } ? prompt[..MaxPrompt] : prompt,
                    Text(root, "cwd") is { Length: > 0 } cwd ? cwd : null);
            }
            catch (JsonException)
            {
                return Empty;
            }
        }

        private static string? Text(JsonObject root, string name) =>
            root[name] is JsonValue value && value.TryGetValue<string>(out var text) ? text : null;
    }

    private static int UsageError(string message)
    {
        Console.Error.WriteLine(message);
        Console.Error.WriteLine(Usage);
        return 2;
    }
}
