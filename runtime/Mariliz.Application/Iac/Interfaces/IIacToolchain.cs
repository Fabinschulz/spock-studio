namespace Mariliz.Application.Iac;

// Uma ferramenta (OpenTofu ou Terraform): as instalações que o Spock gerencia, numa pasta própria dos dados do
// aplicativo, e a distribuição oficial de onde vêm. Nenhuma implementação mexe no PATH da máquina.
public interface IIacToolchain
{
    IacEngine Engine { get; }

    string Name { get; }

    // Licença da distribuição, como a pessoa vê na proposta.
    string License { get; }

    // Null quando a distribuição não publica pacote para este sistema e arquitetura.
    IacPlatform? Platform { get; }

    // A impressão digital da chave que assina as listas de checksums.
    string SigningKey { get; }

    IReadOnlyList<ManagedRuntime> Managed();

    // Refaz o hash do binário e compara com o manifesto.
    bool Intact(ManagedRuntime installation);

    // Versões estáveis publicadas para a plataforma. Fala com a rede.
    Task<IReadOnlyList<IacRelease>> ReleasesAsync(CancellationToken ct);

    // Uma versão pelo número, inclusive as antigas; null quando não existe como estável.
    Task<IacRelease?> ReleaseAsync(string version, CancellationToken ct);

    // Baixa, confere a assinatura da lista de checksums e o hash do pacote, extrai só o binário e grava o manifesto.
    // Qualquer falha de verificação descarta o que foi baixado.
    Task<ManagedRuntime> InstallAsync(IacRelease release, CancellationToken ct);

    void Remove(string version);

    // A instalação que a pessoa fez fora do Spock (PATH e pastas usuais), ou null.
    string? FindExternal();

    string HashFile(string path);
}

public interface IIacSettingsStore
{
    IacRuntimeSettings Read();

    void Write(IacRuntimeSettings settings);
}

// O que os arquivos .tf e .tofu do projeto exigem em required_version. Só lê; nunca executa nada do projeto.
public interface IIacConfigReader
{
    IReadOnlyList<IacRequirement> Requirements(string projectRoot);

    // As pastas (relativas, com "/") que têm .tf ou .tofu, ignorando .terraform/, node_modules/ e afins.
    IReadOnlyList<string> Folders(string projectRoot);
}
