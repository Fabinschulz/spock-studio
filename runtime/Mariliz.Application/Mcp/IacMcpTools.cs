using System.Text;
using System.Text.Json.Nodes;
using Mariliz.Application.Iac;
using Mariliz.Application.Workspace;

namespace Mariliz.Application.Mcp;

// Agentes só consultam: se há Terraform, qual versão o projeto usa e o que os .tf exigem. Instalar, adotar, fixar e
// remover não existem aqui; são decisões da pessoa na interface.
public sealed class IacMcpTools(Func<WorkspaceAuthorizer> workspaces, Func<WorkspaceAuthorizer, TerraformDependencyService> terraform,
    string workingDirectory, IReadOnlySet<string>? scope = null)
{
    public const string StatusTool = "terraform_status";
    private const int MaxArgument = 200;

    public IReadOnlyList<McpTool> Tools() => [new McpTool(Status, StatusAsync)];

    private static readonly McpToolInfo Status = new(
        StatusTool, "Terraform do projeto",
        "Se o Terraform está disponível nesta máquina, a versão que o projeto usa (fixada ou automática), o que os arquivos .tf " +
        "exigem em required_version e o que falta. Só leitura: não instala nem executa nada.",
        new JsonObject
        {
            ["type"] = "object",
            ["properties"] = new JsonObject
            {
                ["project"] = new JsonObject
                {
                    ["type"] = "string",
                    ["description"] = "Nome ou id do projeto, como em list_projects. Sem ele, vale o projeto da pasta atual ou o único do escopo."
                }
            },
            ["additionalProperties"] = false
        });

    private async Task<string> StatusAsync(McpCall call)
    {
        var authorizer = workspaces();
        var project = new McpProjectResolver(authorizer, workingDirectory, scope).Resolve(McpArguments.Text(call.Arguments, "project", MaxArgument));
        var status = await terraform(authorizer).StatusAsync(project.ProjectId, call.Ct);
        var text = new StringBuilder($"Terraform em {project.DisplayName}: {status.Message}");
        if (status.Project is { } details)
        {
            text.Append(details.Selected is { } selected
                ? $"\nVersão do projeto: {selected.Version} ({(details.Automatic ? "automática, ainda não fixada" : "fixada")}, {(selected.Source == TerraformSource.Managed ? "gerenciada pelo Spock" : "instalada fora do Spock")})."
                : "\nVersão do projeto: nenhuma disponível.");
            if (details.Problem is { } problem) text.Append($"\nPendência: {problem}");
            text.Append(details.Requirements.Count == 0
                ? "\nNenhum required_version nos arquivos .tf do projeto."
                : "\nExigências: " + string.Join("; ", details.Requirements.Select(r => $"{r.File}: {r.Constraint}" + (r.Valid ? "" : " (não reconhecida)"))));
        }
        text.Append($"\nMínimo do Spock: {status.MinimumVersion}. Instalar, atualizar ou trocar a versão só pela tela Infraestrutura do Spock.");
        return text.ToString();
    }
}
