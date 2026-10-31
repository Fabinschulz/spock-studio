using Mariliz.Application.Workspace;
using Mariliz.Infrastructure;

namespace Mariliz.Application.Tests;

/// <summary>Cobre a implementação real da porta, que os testes com sistema de arquivos falso não alcançam.</summary>
public class LocalWorkspaceFileSystemTests : IDisposable
{
    private readonly string _temp = Directory.CreateTempSubdirectory("mariliz-ws-").FullName;
    private readonly WorkspaceAuthorizer _auth = new(new LocalWorkspaceFileSystem());

    public void Dispose() => Directory.Delete(_temp, recursive: true);

    [Fact]
    public void Raiz_da_unidade_e_recusada_como_nao_permitida()
    {
        var root = Path.GetPathRoot(_temp)!;

        Assert.Equal(WorkspaceError.NotAllowed, _auth.Authorize(root).Error);
    }

    [Fact]
    public void Pasta_existente_e_autorizada()
    {
        var result = _auth.Authorize(_temp);

        Assert.NotNull(result.Project);
        Assert.Equal(Path.GetFileName(_temp), result.Project!.DisplayName);
    }

    [Fact]
    public void Pasta_inexistente_e_arquivo_comum_nao_sao_projetos()
    {
        var file = Path.Combine(_temp, "arquivo.txt");
        File.WriteAllText(file, "x");

        Assert.Equal(WorkspaceError.NotFound, _auth.Authorize(Path.Combine(_temp, "nao-existe")).Error);
        Assert.Equal(WorkspaceError.NotFound, _auth.Authorize(file).Error);
    }

    [Fact]
    public void Caminho_com_pontos_e_normalizado_para_a_mesma_pasta()
    {
        Directory.CreateDirectory(Path.Combine(_temp, "a"));
        var direct = _auth.Authorize(Path.Combine(_temp, "a")).Project!;
        var indirect = _auth.Authorize(Path.Combine(_temp, "a", "..", "a")).Project!;

        Assert.Equal(direct.ProjectId, indirect.ProjectId);
    }
}
