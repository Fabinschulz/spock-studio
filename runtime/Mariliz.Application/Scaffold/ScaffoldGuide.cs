using System.Text;

namespace Mariliz.Application.Scaffold;

// O roteiro que o agente da sessão segue para criar o esqueleto: o plano inteiro, com as versões e os motivos, e as regras
// que mantêm o que ele cria igual ao que a pessoa vê na tela.
internal static class ScaffoldGuide
{
    public static string For(ScaffoldPlan plan, string root)
    {
        var text = new StringBuilder();
        text.Append($"Raiz do projeto: {root}\n\n## Versões (política do Spock, catálogo embutido)\n\n");
        foreach (var technology in plan.Technologies)
            text.Append($"- {technology.Name} {technology.Version} (linha {technology.Cycle}, {technology.Channel}{(technology.Eol is null ? "" : $", suporte até {technology.Eol}")}): {technology.Reason}\n");
        if (plan.Notes.Count > 0)
        {
            text.Append("\n## Notas\n\n");
            foreach (var note in plan.Notes) text.Append($"- {note}\n");
        }

        text.Append("\n## Passos, em ordem\n\n");
        foreach (var step in plan.Steps) Step(text, step);
        text.Append("\n## Verificação, depois dos passos\n\n");
        foreach (var step in plan.Verification) Step(text, step);

        text.Append($"""

            ## Regras

            - Rode cada comando na pasta indicada, a partir da raiz do projeto. Não troque versão, template nem nome.
            - Grave os arquivos com o conteúdo exato, sem acrescentar nada. O {plan.RecordPath} é do Spock: não edite.
            - Se um comando falhar ou pedir confirmação que não está aqui (por exemplo, instalar e iniciar o projeto agora), não
              improvise: recuse a pergunta ou pare e conte à pessoa o que aconteceu.
            - Este passo cria só o esqueleto. Não implemente funcionalidade: a especificação, a arquitetura e o backlog em
              .spock/arquitetura guiam o que vem depois, com a pessoa.
            - No fim, rode a verificação, confira com {Mcp.ProjectScaffoldTools.CheckTool} e conte à pessoa o resultado do build e dos testes.
            """);
        return text.ToString();
    }

    private static void Step(StringBuilder text, ScaffoldStep step)
    {
        text.Append($"{step.Id}. {step.Title} (pasta {step.Directory})\n");
        if (step.Command is not null) text.Append($"   {step.Command}\n");
        else text.Append($"   Grave {step.File} com exatamente:\n```\n{step.Content}```\n");
    }
}
