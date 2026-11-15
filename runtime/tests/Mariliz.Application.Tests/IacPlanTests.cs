using System.Text.Json;
using System.Text.Json.Nodes;
using Mariliz.Application.Iac;
using Mariliz.Application.Ports;
using Mariliz.Application.Tests.Support;
using Mariliz.Infrastructure.Iac;

namespace Mariliz.Application.Tests;

// O leitor do plano e o adaptador de linha de comando, sem ferramenta de verdade: a saída de show -json e as respostas
// do processo são montadas aqui. A execução real está em IacOperationLiveTests.
public sealed class IacPlanTests
{
    private const string Secret = "senha-que-nao-pode-sair";

    private static JsonNode ShowJson() => JsonNode.Parse("""
        {
          "format_version": "1.2",
          "resource_changes": [
            {"address": "aws_sqs_queue.main[\"orders\"]", "type": "aws_sqs_queue", "change": {"actions": ["create"], "after": {"name": "x"}}},
            {"address": "aws_security_group.tasks", "type": "aws_security_group", "change": {"actions": ["update"], "after": {
              "ingress": [{"from_port": 22, "to_port": 22, "cidr_blocks": ["0.0.0.0/0"]}, {"from_port": 443, "to_port": 443, "cidr_blocks": ["0.0.0.0/0"]}]}}},
            {"address": "aws_iam_policy.ci", "type": "aws_iam_policy", "change": {"actions": ["create"], "after": {
              "policy": "{\"Statement\":[{\"Effect\":\"Allow\",\"Action\":\"*\",\"Resource\":[\"*\"]}]}"}}},
            {"address": "aws_ecr_repository.api", "type": "aws_ecr_repository", "change": {"actions": ["delete", "create"], "after": {"image_tag_mutability": "MUTABLE"}}},
            {"address": "aws_db_instance.old", "type": "aws_db_instance", "change": {"actions": ["delete"], "before": {"password": "SEGREDO"}, "after": null}},
            {"address": "aws_secretsmanager_secret_version.app", "type": "aws_secretsmanager_secret_version", "change": {"actions": ["create"], "after": {"secret_string": "SEGREDO"}}},
            {"address": "data.aws_caller_identity.me", "type": "aws_caller_identity", "change": {"actions": ["read"]}},
            {"address": "aws_sns_topic.alarms", "type": "aws_sns_topic", "change": {"actions": ["no-op"]}}
          ],
          "output_changes": {"queue_url": {"actions": ["create"]}, "same": {"actions": ["no-op"]}}
        }
        """.Replace("SEGREDO", Secret))!;

    [Fact]
    public void Resumo_conta_as_acoes_e_guarda_so_enderecos()
    {
        var (summary, _) = IacPlanReader.Read(ShowJson());

        Assert.Equal((3, 1, 1, 1, 1, 1), (summary.Create, summary.Update, summary.Replace, summary.Delete, summary.Read, summary.Unchanged));
        Assert.Equal(7, summary.Changes.Count);
        Assert.Contains(summary.Changes, c => c is { Address: "aws_ecr_repository.api", Action: "replace" });
        Assert.Equal(["queue_url"], summary.OutputsChanged);
        Assert.DoesNotContain(Secret, JsonSerializer.Serialize(summary));
    }

    [Fact]
    public void Achados_apontam_porta_aberta_curinga_de_IAM_destruicao_e_tag_mutavel_sem_valores()
    {
        var (_, findings) = IacPlanReader.Read(ShowJson());

        Assert.Contains(findings, f => f is { Rule: "open-ingress", Address: "aws_security_group.tasks", Severity: IacFindingSeverity.Danger } && f.Message.Contains("porta 22"));
        Assert.DoesNotContain(findings, f => f.Rule == "open-ingress" && f.Message.Contains("443"));
        Assert.Contains(findings, f => f is { Rule: "iam-wildcard", Address: "aws_iam_policy.ci" });
        Assert.Contains(findings, f => f is { Rule: "destroy", Address: "aws_db_instance.old" });
        Assert.Contains(findings, f => f is { Rule: "replace", Address: "aws_ecr_repository.api" });
        Assert.Contains(findings, f => f is { Rule: "ecr-mutable-tag", Severity: IacFindingSeverity.Warning });
        Assert.DoesNotContain(Secret, JsonSerializer.Serialize(findings));
    }

    [Fact]
    public async Task Adaptador_monta_argumentos_fixos_e_isola_o_diretorio_de_dados()
    {
        var calls = new List<ProcessSpec>();
        var runner = new InterceptingRunner((spec, _) =>
        {
            calls.Add(spec);
            var args = string.Join(' ', spec.Arguments);
            return Task.FromResult<ProcessResult?>(args switch
            {
                _ when args.StartsWith("init", StringComparison.Ordinal) => new ProcessResult(0, "", "", false, false),
                _ when args.StartsWith("workspace select", StringComparison.Ordinal) => new ProcessResult(0, "", "", false, false),
                _ when args.StartsWith("plan", StringComparison.Ordinal) => new ProcessResult(2, "", "", false, false),
                _ when args.StartsWith("show", StringComparison.Ordinal) => new ProcessResult(0, ShowJson().ToJsonString(), "", false, false),
                _ => new ProcessResult(1, "", "inesperado", false, false)
            });
        });
        var installation = new IacInstallation(IacEngine.OpenTofu, IacRuntimeSource.Managed, "1.13.9", "C:/spock/tofu.exe", true, true, false, null, null);
        var runtime = new CliIacRuntime(installation, runner, "C:/spock/cache");

        var run = await runtime.PlanAsync(new IacPlanSpec("C:/op/config/infra", "C:/op/data", "C:/op/plan.tfplan", "dev",
            new Dictionary<string, string> { ["AWS_PROFILE"] = "dev-sso" }), CancellationToken.None);

        Assert.True(run.Succeeded);
        Assert.True(run.HasChanges);
        Assert.Equal(["init", "workspace", "plan", "show"], calls.Select(c => c.Arguments[0]));
        Assert.All(calls, c => Assert.Equal("C:/spock/tofu.exe", c.Executable));
        Assert.All(calls, c => Assert.Equal("C:/op/data", c.Environment!["TF_DATA_DIR"]));
        Assert.All(calls, c => Assert.Equal("C:/spock/cache", c.Environment!["TF_PLUGIN_CACHE_DIR"]));
        Assert.All(calls, c => Assert.Equal("dev-sso", c.Environment!["AWS_PROFILE"]));
        Assert.Equal(["workspace", "select", "dev"], calls[1].Arguments);
        Assert.Contains("-out=C:/op/plan.tfplan", calls[2].Arguments);
        Assert.DoesNotContain(calls, c => c.Arguments.Contains("-auto-approve") || c.Arguments[0] == "apply");
    }

    [Fact]
    public async Task Workspace_inexistente_nao_e_criado_e_o_plano_para()
    {
        var runner = new InterceptingRunner((spec, _) => Task.FromResult<ProcessResult?>(spec.Arguments[0] switch
        {
            "init" => new ProcessResult(0, "", "", false, false),
            "workspace" => new ProcessResult(1, "", "Workspace \"prd\" doesn't exist.", false, false),
            _ => new ProcessResult(0, "", "", false, false)
        }));
        var runtime = new CliIacRuntime(new IacInstallation(IacEngine.Terraform, IacRuntimeSource.Managed, "1.17.2", "tf", true, true, false, null, null), runner);

        var run = await runtime.PlanAsync(new IacPlanSpec("d", "data", "p.tfplan", "prd", new Dictionary<string, string>()), CancellationToken.None);

        Assert.False(run.Succeeded);
        Assert.Contains("não existe", run.Error);
        Assert.DoesNotContain(runner.Calls, c => c.Arguments.Contains("new") || c.Arguments[0] == "plan");
    }

    [Fact]
    public async Task Validate_le_os_diagnosticos_em_JSON()
    {
        var runner = new InterceptingRunner((spec, _) => Task.FromResult<ProcessResult?>(spec.Arguments[0] switch
        {
            "init" => new ProcessResult(0, "", "", false, false),
            "validate" => new ProcessResult(1, """
                {"valid": false, "error_count": 1, "diagnostics": [{"severity": "error", "summary": "Unsupported argument",
                 "detail": "An argument named \"nome\" is not expected here.", "range": {"filename": "modules\\sqs\\resource.tf", "start": {"line": 12}}}]}
                """, "", false, false),
            _ => new ProcessResult(0, "", "", false, false)
        }));
        var runtime = new CliIacRuntime(new IacInstallation(IacEngine.OpenTofu, IacRuntimeSource.Managed, "1.13.9", "tofu", true, true, false, null, null), runner);

        var result = await runtime.ValidateAsync("d", "infra/modules/sqs", "data", CancellationToken.None);

        Assert.False(result.Valid);
        var diagnostic = Assert.Single(result.Diagnostics);
        Assert.Equal(("error", "Unsupported argument", "modules/sqs/resource.tf", 12), (diagnostic.Severity, diagnostic.Summary, diagnostic.File, diagnostic.Line));
        Assert.Equal(["init", "-backend=false", "-input=false", "-no-color"], runner.Calls[0].Arguments);
    }
}
