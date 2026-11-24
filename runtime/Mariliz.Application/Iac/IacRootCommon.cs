using System.Text;
using static Mariliz.Application.Iac.IacHcl;
using static Mariliz.Application.Iac.IacModuleText;

namespace Mariliz.Application.Iac;

// As regras de produto que a raiz gerada repete em toda nuvem: versão mínima da ferramenta, o workspace como ambiente,
// o prefixo dos nomes, a tag de imagem imutável e o roteiro do README. O texto dos módulos é de cada nuvem.
internal static class IacRootCommon
{
    // O lock nativo do state e os recursos que o modelo usa exigem esta versão (NET-S09 decide se o mínimo aceito sobe junto).
    public const string MinimumToolVersion = "1.11";
    public const string RequiredVersion = ">= " + MinimumToolVersion;

    public static void RequiredVersionAttr(IacHcl terraform) => terraform.Attr("required_version", Quote(RequiredVersion));

    public static void EnvironmentGuard(IacHcl h)
    {
        h.Section("Environment guard");
        h.Comment("O ambiente é o workspace selecionado; no workspace default os recursos não teriam ambiente definido.");
        h.Block("resource \"terraform_data\" \"environment_guard\"", r =>
        {
            r.Attr("input", "terraform.workspace");
            r.Blank();
            r.Block("lifecycle", l => l.Block("precondition", p => p
                .Attr("condition", "terraform.workspace != \"default\"")
                .Attr("error_message", Quote("Selecione o workspace do ambiente antes do plan/apply (ex.: tofu workspace select dev)."))));
        });
    }

    public static void NamePrefix(IacHcl locals, string project) => locals.Attr("name_prefix", Quote($"{project}-${{terraform.workspace}}"));

    // A primeira imagem vem do commit; depois o CI é o dono da revisão, e latest nunca vale.
    public static void ImageTag(Variables v, string description) =>
        v.Validated("image_tag", "string", description,
            "can(regex(\"^[A-Za-z0-9_.-]{1,128}$\", var.image_tag)) && var.image_tag != \"latest\"",
            "Use uma tag imutável (o SHA do commit), nunca latest.", "Imagem");

    public static void ImageTagExample(IacHcl h) => h.Attr("image_tag", Quote("0000000000000000000000000000000000000000"));

    public static string ToolPrerequisite(string? reason = null) =>
        $"- OpenTofu **≥ {MinimumToolVersion}** (ou Terraform ≥ {MinimumToolVersion}){(reason is null ? "" : ": " + reason)}. O Spock instala e verifica a ferramenta.\n";

    /// <param name="notes">Linhas do provedor que vêm antes dos avisos do workspace default.</param>
    public static void ReadmeWorkflow(StringBuilder text, string dir, string notes = "")
    {
        text.Append("\n## Plan e apply de um ambiente\n\nPela tela Infraestrutura do Spock (validar, gerar plano, aprovar) ou no terminal:\n\n");
        text.Append($"```bash\ntofu -chdir={dir} init\n```\n\n```bash\ntofu -chdir={dir} workspace select dev\n```\n\n```bash\ntofu -chdir={dir} plan -out=dev.tfplan\n```\n\n```bash\ntofu -chdir={dir} apply dev.tfplan\n```\n\n");
        text.Append(notes);
        text.Append("- No workspace `default` o plan falha de propósito.\n- Depois de um apply, `plan -detailed-exitcode` deve informar `No changes`.\n");
    }

    public static void ReadmeChecks(StringBuilder text, string dir, string? proof = null)
    {
        text.Append("\n## Verificações\n\n```bash\ntofu fmt -check -recursive " + dir + "\n```\n\n");
        text.Append("Rode `tofu validate` em cada pasta com `.tf` depois de `tofu init -backend=false`." + (proof is null ? "" : " " + proof) + "\n");
    }
}
