using System.Security.Cryptography;
using System.Text;
using Mariliz.Application.Execution;
using Mariliz.Application.Ports;
using Mariliz.Application.Workspace;
using Mariliz.Core.Policy;

namespace Mariliz.Application.Workflows;

/// <summary>
/// Executa <c>file.write</c> no projeto, e só isso. O alvo passa pela política de caminhos, o conteúdo é conferido contra o
/// hash aprovado e um arquivo diferente que já exista nunca é sobrescrito. Repetir a execução com o mesmo conteúdo não muda nada.
/// </summary>
public sealed class ProjectActionExecutor(WorkspaceAuthorizer workspace, IProjectFilesFactory filesFactory) : IActionExecutor
{
    public ActionOutcome Execute(string projectId, JournalAction action)
    {
        if (action.Kind != "file.write") return new ActionOutcome(JournalAction.Failed);
        if (!workspace.TryGetRoot(projectId, out var root)) throw EngineErrors.ProjectNotAuthorized();

        // O conteúdo aprovado é o do hash: um diário alterado depois da aprovação não vira escrita.
        var bytes = Encoding.UTF8.GetBytes(action.Content);
        if (Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant() != action.ContentSha256)
            return new ActionOutcome(JournalAction.Failed);
        if (!ActionGovernance.IsDecisionTarget(action.Target)
            || !PathGuard.TryNormalize(action.Target, out var path, out _) || !ManagedScope.IsWritable(path))
            return new ActionOutcome(JournalAction.Failed);

        var files = filesFactory.Open(root);
        if (files.ReadBytes(path) is { } existing)
        {
            // O mesmo conteúdo já está lá (execução repetida): nada a fazer. Outro conteúdo é de outra pessoa.
            return new ActionOutcome(existing.AsSpan().SequenceEqual(bytes) ? JournalAction.Executed : JournalAction.Conflict);
        }

        files.WriteAtomic(path, bytes);
        return new ActionOutcome(files.ReadBytes(path) is { } written && written.AsSpan().SequenceEqual(bytes) ? JournalAction.Executed : JournalAction.Failed);
    }
}
