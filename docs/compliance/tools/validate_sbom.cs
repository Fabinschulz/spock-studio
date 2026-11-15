#:package JsonSchema.Net@9.5.0
using System.Text.Json;
using Json.Schema;

// Valida os SBOMs contra os schemas oficiais. Baixe para uma pasta fora do repositório:
//   https://raw.githubusercontent.com/spdx/spdx-spec/v2.3/schemas/spdx-schema.json
//   https://raw.githubusercontent.com/CycloneDX/specification/1.6/schema/bom-1.6.schema.json
//   https://raw.githubusercontent.com/CycloneDX/specification/1.6/schema/spdx.schema.json
//   https://raw.githubusercontent.com/CycloneDX/specification/1.6/schema/jsf-0.82.schema.json
// uso: dotnet run docs/compliance/tools/validate_sbom.cs -- <pasta-dos-schemas> docs/compliance/SBOM/*.json
var schemas = args[0];
foreach (var dep in new[] { "spdx.schema.json", "jsf-0.82.schema.json" })
    JsonSchema.FromText(File.ReadAllText(Path.Combine(schemas, dep)));
var cdx = JsonSchema.FromText(File.ReadAllText(Path.Combine(schemas, "bom-1.6.schema.json")));
var spdx = JsonSchema.FromText(File.ReadAllText(Path.Combine(schemas, "spdx-schema.json")));
var options = new EvaluationOptions { OutputFormat = OutputFormat.List, RequireFormatValidation = true };
var failed = 0;
foreach (var file in args.Skip(1))
{
    using var doc = JsonDocument.Parse(File.ReadAllText(file));
    var isCdx = doc.RootElement.TryGetProperty("bomFormat", out _);
    var result = (isCdx ? cdx : spdx).Evaluate(doc.RootElement, options);
    Console.WriteLine($"{(result.IsValid ? "VÁLIDO  " : "INVÁLIDO")} {(isCdx ? "CycloneDX 1.6" : "SPDX 2.3")} {Path.GetFileName(file)}");
    if (!result.IsValid)
    {
        failed++;
        foreach (var d in (result.Details ?? []).Where(d => d.Errors is { Count: > 0 }).Take(15))
            Console.WriteLine($"   {d.InstanceLocation} {d.EvaluationPath}: {string.Join("; ", d.Errors!.Values)}");
    }
}
return failed;
