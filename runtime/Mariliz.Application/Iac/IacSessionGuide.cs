namespace Mariliz.Application.Iac;

// O roteiro do agente da sessão para propor o desenho (modo Sessão): o Spock não chama modelo, entrega as regras e o
// formato, confere com iac_blueprint_check e lê a proposta gravada. Gerar os arquivos continua sendo decisão da pessoa.
public static class IacSessionGuide
{
    private const string AzureDesign = """
        Proponha a infraestrutura Azure mais simples que atende à necessidade, usando só estes componentes: um serviço em
        contêiner no Container Apps (HTTPS pelo domínio gerenciado, sem certificado próprio), um banco PostgreSQL Flexible
        Server (login do Entra ID, sem senha) e filas do Service Bus.
        Formato exato:
        {"blueprint": {"provider": "azure", "project": "nome-do-projeto", "region": "brazilsouth",
           "service": {"name": "api", "port": 8080, "healthPath": "/health", "cpu": 512, "memory": 1024, "desiredCount": 1, "publicIp": false} ou null,
           "database": {"engine": "postgres", "engineVersion": "16", "instanceClass": "B_Standard_B1ms", "storageGb": 32, "multiAz": false} ou null,
           "queues": ["nome-do-fluxo"]},
         "notes": {"services": [{"name": "Container Apps", "reason": "por que entra"}], "risks": ["..."], "alternatives": ["..."], "assumptions": ["..."]}}
        CPU de 256 a 2048 em passos de 256, com memória o dobro da CPU. SKU do banco B_, GP_ ou MO_Standard_...; armazenamento
        de 32, 64, 128, 256, 512 ou 1024 GB; multiAz só fora da camada B_.
        """;

    private const string GoogleDesign = """
        Proponha a infraestrutura Google Cloud mais simples que atende à necessidade, usando só estes componentes: um serviço
        em contêiner no Cloud Run (HTTPS pelo endereço gerenciado, sem certificado próprio), um banco Cloud SQL (postgres ou
        mysql, só IP privado) e tópicos do Pub/Sub.
        Formato exato:
        {"blueprint": {"provider": "gcp", "project": "nome-do-projeto", "region": "southamerica-east1",
           "service": {"name": "api", "port": 8080, "healthPath": "/health", "cpu": 1024, "memory": 512, "desiredCount": 1, "publicIp": false} ou null,
           "database": {"engine": "postgres", "engineVersion": "16", "instanceClass": "db-f1-micro", "storageGb": 10, "multiAz": false} ou null,
           "queues": ["nome-do-fluxo"]},
         "notes": {"services": [{"name": "Cloud Run", "reason": "por que entra"}], "risks": ["..."], "alternatives": ["..."], "assumptions": ["..."]}}
        CPU de 1024, 2048 ou 4096, com memória em múltiplos de 256 (ex.: 1024/512, 2048/4096). Tier do banco db-f1-micro,
        db-g1-small ou db-custom-<vcpu>-<mb>; armazenamento entre 10 e 1000 GB.
        """;

    public static string For(string provider, string proposalPath) => $$"""
        {{Design(provider)}}
        Regras:
        - Fatos da conta (conta, subscription ou projeto, rede, subnets, certificado, e-mail de alarme, bucket de state) vêm da
          pessoa ou da conexão cloud do projeto: pergunte, ou leia com cloud_connections se a conexão estiver liberada para
          agentes. Nunca invente ids.
        - Não invente preços: o iac_blueprint_check lista o que é cobrado.
        - Nunca escreva senhas, chaves ou tokens.
        Passos:
        1. Se a necessidade não estiver clara, pergunte à pessoa o que o sistema precisa.
        2. Monte o desenho e chame iac_blueprint_check com {"blueprint": ...}. Ajuste até passar nas checagens do
           desenho; as de fatos da conta pedem o dado à pessoa e podem ficar para ela completar na tela.
        3. Grave o objeto completo, com "blueprint" e "notes", em {{proposalPath}}. Não grave nenhum outro arquivo do projeto.
        4. Diga à pessoa para abrir Infraestrutura no Spock e usar o desenho proposto: lá ela completa os fatos, revisa o
           diff e grava. Você não escreve os arquivos .tf nem roda plano ou apply.
        """;

    private static string Design(string provider) => provider switch
    {
        "aws" => IacArchitectPrompts.AwsDesign,
        "azure" => AzureDesign,
        "gcp" => GoogleDesign,
        _ => throw new ArgumentException($"Nuvem fora da geração: {provider}.", nameof(provider))
    };
}
