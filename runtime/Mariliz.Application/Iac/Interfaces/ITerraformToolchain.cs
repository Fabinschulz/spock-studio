namespace Mariliz.Application.Iac;

// As instalações que o Spock gerencia, numa pasta própria dos dados do aplicativo, e a distribuição oficial de onde vêm.
// Nenhuma implementação mexe no PATH da máquina.
public interface ITerraformToolchain
{
    // Null quando a HashiCorp não publica pacote para este sistema e arquitetura.
    TerraformPlatform? Platform { get; }

    // A impressão digital da chave que assina as listas de checksums.
    string SigningKey { get; }

    IReadOnlyList<ManagedTerraform> Managed();

    // Refaz o hash do binário e compara com o manifesto.
    bool Intact(ManagedTerraform installation);

    // Versões estáveis publicadas para a plataforma, da mais nova para a mais velha. Fala com a rede.
    Task<IReadOnlyList<TerraformRelease>> ReleasesAsync(CancellationToken ct);

    // Uma versão pelo número, inclusive as antigas que não aparecem na lista; null quando não existe como estável.
    Task<TerraformRelease?> ReleaseAsync(string version, CancellationToken ct);

    // Baixa, confere a assinatura da lista de checksums e o hash do pacote, extrai só o binário e grava o manifesto.
    // Qualquer falha de verificação descarta o que foi baixado.
    Task<ManagedTerraform> InstallAsync(TerraformRelease release, CancellationToken ct);

    void Remove(string version);

    // O Terraform que a pessoa instalou fora do Spock (PATH e pastas usuais), ou null.
    string? FindExternal();

    string HashFile(string path);
}

public interface ITerraformSettingsStore
{
    TerraformSettings Read();

    void Write(TerraformSettings settings);
}

// O que os arquivos .tf do projeto exigem em required_version. Só lê; nunca executa nada do projeto.
public interface ITerraformConfigReader
{
    IReadOnlyList<TerraformRequirement> Requirements(string projectRoot);
}
