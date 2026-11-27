using System.Reflection;
using System.Text.RegularExpressions;
using Mariliz.Application.Boot;
using Mariliz.Application.Protocol;

namespace Mariliz.Application.Tests;

// O catálogo só vale se cada ficha aponta para algo que existe: método do motor, tela da interface e política de permissão.
public class BootCatalogTests
{
    private static IReadOnlySet<string> EngineMethods() => typeof(ProtocolMethods)
        .GetFields(BindingFlags.Public | BindingFlags.Static)
        .Where(field => field.IsLiteral && field.FieldType == typeof(string))
        .Select(field => (string)field.GetRawConstantValue()!)
        .ToHashSet(StringComparer.Ordinal);

    [Fact]
    public void Todo_metodo_citado_por_uma_capacidade_existe_no_protocolo_do_motor()
    {
        var methods = EngineMethods();
        var missing = CapabilityCatalog.All.SelectMany(capability => capability.Methods.Select(method => (capability.Id, method)))
            .Where(pair => !methods.Contains(pair.method))
            .Select(pair => $"{pair.Id} -> {pair.method}")
            .ToList();

        Assert.True(missing.Count == 0, "Métodos que não existem: " + string.Join(", ", missing));
    }

    [Fact]
    public void Toda_tela_de_destino_existe_na_interface()
    {
        var paths = File.ReadAllText(Path.Combine(ContractFiles.Root, "desktop", "ui", "src", "shared", "routing", "paths.ts"));
        var known = Regex.Matches(paths, @"'(/[a-z/-]*)'").Select(match => match.Groups[1].Value).ToHashSet(StringComparer.Ordinal);

        var missing = CapabilityCatalog.All.Where(capability => capability.Route is not null && !known.Contains(capability.Route))
            .Select(capability => $"{capability.Id} -> {capability.Route}").ToList();

        Assert.True(missing.Count == 0, "Telas que não existem: " + string.Join(", ", missing));
    }

    [Fact]
    public void O_que_o_Boot_executa_sozinho_nunca_altera_o_projeto_nem_a_nuvem()
    {
        var direct = CapabilityCatalog.All.Where(capability => capability.Mode == ExecutionMode.Direct).ToList();

        Assert.NotEmpty(direct);
        Assert.All(direct, capability => Assert.True(capability.Operation <= OperationClass.Preparation, capability.Id));
        // A preparação é só a análise, que grava o perfil na pasta de dados do Spock.
        Assert.Equal(["engineering.analyze_project"], direct.Where(capability => capability.Operation == OperationClass.Preparation).Select(capability => capability.Id));
    }

    [Fact]
    public void Os_metodos_de_escrita_so_aparecem_em_capacidades_que_abrem_a_tela()
    {
        string[] writers =
        [
            ProtocolMethods.Apply, ProtocolMethods.Revert, ProtocolMethods.WorkflowStart, ProtocolMethods.WorkflowResume, ProtocolMethods.SecurityFix,
            ProtocolMethods.IdeaSave, ProtocolMethods.IacApply, ProtocolMethods.IacStateUnlock, ProtocolMethods.IacBootstrapSave, ProtocolMethods.IacGenerateSave,
            ProtocolMethods.DecisionRecord, ProtocolMethods.CloudCreate, ProtocolMethods.ModelSave
        ];

        var offenders = CapabilityCatalog.All.Where(capability => capability.Mode == ExecutionMode.Direct && capability.Methods.Intersect(writers).Any())
            .Select(capability => capability.Id).ToList();

        Assert.Empty(offenders);
    }

    [Fact]
    public void Capacidade_destrutiva_exige_o_nivel_mais_alto_e_nunca_e_direta()
    {
        var apply = CapabilityCatalog.Find("infrastructure.apply")!;

        Assert.Equal(OperationClass.Destructive, apply.Operation);
        Assert.Equal(ExecutionMode.Handoff, apply.Mode);
        Assert.True(PermissionEvaluator.RequiredLevel(apply.Operation) > PermissionEvaluator.Ceiling);
    }

    [Fact]
    public void Cada_dominio_tem_ao_menos_uma_capacidade_e_os_ids_nao_se_repetem()
    {
        Assert.Equal(CapabilityCatalog.All.Count, CapabilityCatalog.All.Select(capability => capability.Id).Distinct().Count());
        Assert.All(BootDomains.All, domain => Assert.Contains(CapabilityCatalog.All, capability => capability.Domain == domain));
    }

    [Fact]
    public void Cada_capacidade_diz_o_resultado_esperado_e_o_que_conferir()
    {
        Assert.All(CapabilityCatalog.All, capability =>
        {
            Assert.False(string.IsNullOrWhiteSpace(capability.ExpectedResult), capability.Id);
            Assert.NotEmpty(capability.Validations);
            Assert.NotEmpty(capability.Goals);
        });
    }

    [Fact]
    public void Catalogo_malformado_falha_na_montagem()
    {
        var broken = CapabilityCatalog.All.Select(capability => capability.Id == "engineering.configure" ? capability with { Mode = ExecutionMode.Direct } : capability).ToList();

        Assert.Throws<InvalidOperationException>(() => new CapabilityRegistry(broken));
    }

    [Fact]
    public void Os_metodos_do_Boot_estao_no_protocolo_e_nao_sao_executaveis_por_ele_mesmo()
    {
        Assert.All(ProtocolMethods.Boot, method => Assert.Contains(method, EngineMethods()));
        Assert.DoesNotContain(CapabilityCatalog.All, capability => capability.Methods.Any(method => ProtocolMethods.Boot.Contains(method)));
    }
}
