using Mariliz.Core.Evals;

namespace Mariliz.Application.Validation;

internal static class EvalCatalog
{
    public static readonly IReadOnlyList<EvalSpec> Specs =
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
        new(EvalIds.Specification, "Estrutura de especificação operacional", "A estrutura de especificação é utilizável", EvalKind.Deterministic, true,
            "Instalação completa, hash igual ao do instalador, versão validada, Python disponível, e os scripts de validação respondem corretamente a entradas conhecidas."),
        new(EvalIds.Secrets, "Segredos", "Ausência de credenciais nos arquivos de configuração", EvalKind.Deterministic, true,
            "Nenhum arquivo gerenciado, .mcp.json, mariliz.yaml ou proposta contém formato conhecido de credencial."),
        new(EvalIds.Environment, "Ambiente de IA", "Compatibilidade com o ambiente selecionado", EvalKind.Deterministic, true,
            "O ambiente declarado é suportado e nenhum arquivo gerenciado está fora das pastas desse ambiente."),
        new(EvalIds.Requirements, "Requisitos do projeto", "Cumprimento dos requisitos identificados", EvalKind.Deterministic, true,
            "Cada requisito derivado da descoberta (stack, comandos, proteção por hook) tem o artefato correspondente. A estrutura de especificação é verificada à parte."),
        new(EvalIds.Idempotence, "Idempotência", "Repetir o fluxo não altera nada", EvalKind.Deterministic, true,
            "Um novo plano não contém criação, atualização, remoção, conflito nem comando."),
        new(EvalIds.Regression, "Regressão", "Nenhuma verificação que passava passou a falhar", EvalKind.Deterministic, false,
            "Todo Eval aprovado na execução anterior continua aprovado."),
        new(EvalIds.Judge, "Juiz por modelo", "Qualidade semântica do Harness", EvalKind.Model, false,
            "Avaliação por modelo com critérios explícitos; nunca aprova sozinha.")
    ];
}
