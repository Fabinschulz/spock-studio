using System.Globalization;
using static Mariliz.Application.Iac.IacHcl;

namespace Mariliz.Application.Iac;

// O mesmo modelo de pasta da AWS (TERRAFORM_PROJECT_LAYOUT.md) com os serviços equivalentes do Google Cloud: Cloud Run no
// lugar do ECS com load balancer, Artifact Registry, Secret Manager, Cloud SQL só com IP privado e login IAM (sem senha),
// Pub/Sub com tópico de mensagens mortas e alertas do Cloud Monitoring. As APIs usadas são ligadas pela própria raiz.
// Os módulos são texto fixo; o desenho entra pela raiz.
public static class IacGoogleGenerator
{
    public static IReadOnlyList<IacAwsGenerator.File> Generate(IacBlueprint b, string root)
    {
        string P(string relative) => root.Length == 0 ? relative : $"{root}/{relative}";
        var files = new List<IacAwsGenerator.File>
        {
            new(P("providers.tf"), Providers(b)),
            new(P("variables.tf"), RootVariables(b)),
            new(P("resource.tf"), RootResources(b)),
            new(P("output.tf"), RootOutputs(b)),
            new(P("terraform.tfvars.example"), TfvarsExample(b)),
            new(P("README.md"), Readme(b, root)),
            new(P("modules/monitoring/resource.tf"), MonitoringResource),
            new(P("modules/monitoring/variables.tf"), Variables(v => v
                .Required("project_id", "string", "Projeto do Google Cloud")
                .Nullable("alarm_email", "string", "E-mail que recebe os alertas; null cria os alertas sem canal")
                .Default("flows", "list(string)", "Fluxos cuja assinatura de mensagens mortas é vigiada", "[]")
                .Default("watch_service", "bool", "Se há serviço do Cloud Run para vigiar", "false")
                .Nullable("service_name", "string", "Serviço do Cloud Run vigiado")
                .Default("watch_database", "bool", "Se há instância do Cloud SQL para vigiar", "false")
                .Nullable("database_instance", "string", "Instância do Cloud SQL vigiada")))
        };

        if (b.Queues.Count > 0)
        {
            files.Add(new(P("modules/pubsub/resource.tf"), PubSubResource));
            files.Add(new(P("modules/pubsub/variables.tf"), Variables(v => v
                .Required("flows", "list(string)", "Fluxos de mensagens; cada um vira tópico, assinatura e tópico de mensagens mortas. Renomear destrói o fluxo"))));
            files.Add(new(P("modules/pubsub/outputs.tf"), IacAwsGenerator.Outputs(
                ("topics", "Tópico de cada fluxo", "{ for flow, topic in google_pubsub_topic.main : flow => topic.id }"),
                ("subscriptions", "Assinatura de cada fluxo", "{ for flow, subscription in google_pubsub_subscription.main : flow => subscription.id }"),
                ("dead_letter_subscriptions", "Nomes das assinaturas de mensagens mortas", "[for subscription in google_pubsub_subscription.dead_letter : subscription.name]"))));
        }

        if (b.Service is not null)
        {
            files.Add(new(P("modules/artifact/resource.tf"), ArtifactResource));
            files.Add(new(P("modules/artifact/variables.tf"), Variables(v => v.Required("region", "string", "Região do repositório"))));
            files.Add(new(P("modules/artifact/outputs.tf"), IacAwsGenerator.Outputs(
                ("repository_id", "Repositório de imagens", "google_artifact_registry_repository.this.repository_id"),
                ("repository_name", "Nome completo do repositório", "google_artifact_registry_repository.this.name"),
                ("registry_path", "Caminho onde o CI publica a imagem com a tag do commit",
                    "\"${google_artifact_registry_repository.this.location}-docker.pkg.dev/${google_artifact_registry_repository.this.project}/${google_artifact_registry_repository.this.repository_id}\""))));
            files.Add(new(P("modules/secrets/resource.tf"), SecretsResource));
            files.Add(new(P("modules/secrets/variables.tf"), Variables(_ => { })));
            files.Add(new(P("modules/secrets/outputs.tf"), IacAwsGenerator.Outputs(("secret_id", "Segredo da aplicação, criado sem versão (sem valor)", "google_secret_manager_secret.app.id"))));
            files.Add(new(P("modules/cloudrun/resource.tf"), CloudRunResource));
            files.Add(new(P("modules/cloudrun/variables.tf"), Variables(v => v
                .Required("project_id", "string", "Projeto do Google Cloud")
                .Required("region", "string", "Região do serviço")
                .Required("service_name", "string", "Nome do serviço; também é o nome da imagem")
                .Required("registry_path", "string", "Caminho do repositório de imagens")
                .Required("repository_name", "string", "Repositório de onde a imagem é puxada")
                .Required("image_tag", "string", "Tag da imagem inicial; depois o CI é o dono da revisão")
                .Required("port", "number", "Porta do contêiner")
                .Required("health_path", "string", "Caminho do health check")
                .Required("cpu", "string", "vCPU do contêiner")
                .Required("memory", "string", "Memória do contêiner")
                .Required("min_instances", "number", "Instâncias sempre ligadas")
                .Required("max_instances", "number", "Teto de instâncias")
                .Required("secret_id", "string", "Segredo que a aplicação lê")
                .Default("topics", "map(string)", "Tópicos em que a aplicação publica, por fluxo", "{}")
                .Default("subscriptions", "map(string)", "Assinaturas que a aplicação consome, por fluxo", "{}")
                .Nullable("network_id", "string", "Rede VPC para alcançar o banco por IP privado")
                .Default("grant_database_access", "bool", "Se a conta de serviço entra no Cloud SQL", "false")
                .Default("environment", "map(string)", "Variáveis de ambiente do contêiner (nunca segredo)", "{}"))));
            files.Add(new(P("modules/cloudrun/outputs.tf"), IacAwsGenerator.Outputs(
                ("service_name", "Serviço do Cloud Run", "google_cloud_run_v2_service.this.name"),
                ("uri", "Endereço HTTPS público do serviço", "google_cloud_run_v2_service.this.uri"),
                ("service_account_email", "Conta de serviço da aplicação", "google_service_account.app.email"))));
        }

        if (b.Database is { } database)
        {
            files.Add(new(P("modules/cloudsql/resource.tf"), CloudSqlResource(database.Engine)));
            files.Add(new(P("modules/cloudsql/variables.tf"), Variables(v => v
                .Required("region", "string", "Região da instância")
                .Required("database_version", "string", "Versão do banco (ex.: POSTGRES_16, MYSQL_8_0)")
                .Required("tier", "string", "Tier da instância")
                .Required("storage_gb", "number", "Armazenamento inicial em GB")
                .Required("regional", "bool", "Réplica em outra zona (alta disponibilidade)")
                .Required("database_name", "string", "Nome do banco criado")
                .Required("network_id", "string", "Rede VPC com acesso privado a serviços")
                .Nullable("iam_user", "string", "Conta de serviço que entra no banco pelo login IAM")
                .Default("create_iam_user", "bool", "Se cria o usuário IAM da aplicação", "false"))));
            files.Add(new(P("modules/cloudsql/outputs.tf"), IacAwsGenerator.Outputs(
                ("instance_name", "Instância do Cloud SQL", "google_sql_database_instance.this.name"),
                ("connection_name", "Nome de conexão (projeto:região:instância)", "google_sql_database_instance.this.connection_name"),
                ("private_ip", "IP privado do banco", "google_sql_database_instance.this.private_ip_address"),
                ("database_name", "Nome do banco criado", "google_sql_database.app.name"))));
        }
        return files;
    }

    public static IReadOnlyList<IacCostItem> Costs(IacBlueprint b)
    {
        var items = new List<IacCostItem>();
        if (b.Service is { } s)
        {
            items.Add(new($"Cloud Run ({s.DesiredCount} instância(s) mínima(s), {Cpu(s.Cpu)} vCPU, {Memory(s.Memory)})", "Por vCPU-segundo e GiB-segundo, por requisição; instâncias mínimas cobram também ociosas."));
            items.Add(new("Artifact Registry", "Armazenamento das imagens por GB-mês; a limpeza guarda as 20 mais recentes."));
            items.Add(new("Secret Manager", "Por versão de segredo ativa e por acesso."));
            items.Add(new("Cloud Logging", "Por GB ingerido além da franquia mensal."));
        }
        if (b.Database is { } d)
            items.Add(new($"Cloud SQL {d.Engine} {d.InstanceClass}{(d.MultiAz ? " regional" : "")}", $"Instância por hora, {d.StorageGb} GB de SSD por mês e backup além do tamanho do banco."));
        if (b.Queues.Count > 0) items.Add(new($"Pub/Sub ({b.Queues.Count} fluxo(s))", "Por TiB de mensagens e armazenamento retido."));
        items.Add(new("Cloud Monitoring (alertas)", "Por condição de alerta e por série consultada, com franquia mensal."));
        items.Add(new("Transferência de dados", "Saída para a internet por GB."));
        return items;
    }

    private static string Cpu(int units) => Math.Max(1, units / 1024).ToString(CultureInfo.InvariantCulture);

    private static string Memory(int megabytes) => megabytes % 1024 == 0 ? $"{megabytes / 1024}Gi" : $"{megabytes}Mi";

    private static string DatabaseVersion(IacDatabaseSpec d) =>
        d.Engine == "mysql" ? "MYSQL_" + d.EngineVersion.Replace('.', '_') : "POSTGRES_" + d.EngineVersion;

    // name_prefix e labels em todo módulo; no Google os rótulos (labels) fazem o papel das tags.
    private static string Variables(Action<IacAwsGenerator.Variables> extra)
    {
        var v = new IacAwsGenerator.Variables();
        v.Validated("name_prefix", "string", "Prefixo dos nomes: <projeto>-<ambiente>", "can(regex(\"^[a-z0-9-]+$\", var.name_prefix))",
            "O prefixo deve conter apenas letras minúsculas, dígitos e hífens.", "Environment");
        extra(v);
        v.Default("labels", "map(string)", "Rótulos aplicados aos recursos", "{}", "Labels");
        return v.ToString();
    }

    private static string Providers(IacBlueprint b)
    {
        var h = new IacHcl();
        h.Block("terraform", t =>
        {
            t.Attr("required_version", Quote(">= 1.11"));
            t.Blank();
            t.Block("required_providers", r => r.Object("google", o => o.Attr("source", Quote("hashicorp/google")).Attr("version", Quote(IacBootstrapTemplates.GoogleVersion))));
            t.Blank();
            if (b.Backend is { } backend)
            {
                t.Comment($"O state de cada workspace fica em {b.Project}/<workspace>.tfstate no bucket, com trava nativa do GCS.");
                t.Block("backend \"gcs\"", s => s.Attr("bucket", Quote(backend.Bucket)).Attr("prefix", Quote(b.Project)));
            }
            else
            {
                t.Comment("Sem backend remoto ainda: crie o bucket com o bootstrap (tela Infraestrutura, State) e cole aqui o bloco backend \"gcs\".");
            }
        });
        h.Blank();
        h.Block("provider \"google\"", p => p.Attr("project", "var.project_id").Attr("region", "var.region"));
        return h.ToString();
    }

    private static string RootVariables(IacBlueprint b)
    {
        var v = new IacAwsGenerator.Variables();
        v.Validated("project_id", "string", "Projeto do Google Cloud dos recursos", "can(regex(\"^[a-z][a-z0-9-]{4,28}[a-z0-9]$\", var.project_id))",
            "O projeto deve ter de 6 a 30 caracteres: minúsculas, dígitos e hífen.", "Project");
        v.Default("region", "string", "Região do Google Cloud onde os recursos são criados", Quote(b.Region), "Region");
        if (b.Database is not null)
            v.Validated("network_id", "string", "Rede VPC existente com acesso privado a serviços (o Terraform não cria rede)",
                "can(regex(\"^projects/[^/]+/global/networks/[^/]+$\", var.network_id))", "Informe a rede no formato projects/<projeto>/global/networks/<rede>.",
                "Rede existente");
        if (b.Service is not null)
            v.Validated("image_tag", "string", "Tag da imagem inicial; só vale para o primeiro apply, porque depois o CI é o dono da revisão",
                "can(regex(\"^[A-Za-z0-9_.-]{1,128}$\", var.image_tag)) && var.image_tag != \"latest\"",
                "Use uma tag imutável (o SHA do commit), nunca latest.", "Imagem");
        v.Default("alarm_email", "string", "E-mail que recebe os alertas; null cria os alertas sem canal",
            b.AlarmEmail is null ? "null" : Quote(b.AlarmEmail), "Alarmes");
        return v.ToString();
    }

    private static string RootResources(IacBlueprint b)
    {
        var h = new IacHcl();
        var apis = new List<string> { "monitoring.googleapis.com" };
        if (b.Queues.Count > 0) apis.Add("pubsub.googleapis.com");
        if (b.Service is not null) apis.AddRange(["run.googleapis.com", "artifactregistry.googleapis.com", "secretmanager.googleapis.com"]);
        if (b.Database is not null) apis.AddRange(["sqladmin.googleapis.com", "servicenetworking.googleapis.com"]);

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
        h.Blank();
        h.Block("locals", l =>
        {
            l.Attr("name_prefix", Quote($"{b.Project}-${{terraform.workspace}}"));
            l.Blank();
            l.Object("labels", t => t.Attr("project", Quote(b.Project)).Attr("environment", "terraform.workspace").Attr("managed-by", Quote("terraform")));
        });
        h.Blank();
        h.Section("APIs");
        h.Comment("Ligar uma API não tem custo; desligá-la no destroy derrubaria o que outros usam, por isso fica ligada.");
        h.Block("resource \"google_project_service\" \"apis\"", r =>
        {
            r.Attr("for_each", "toset(" + QuotedList(apis) + ")");
            r.Blank();
            r.Attr("project", "var.project_id").Attr("service", "each.key").Attr("disable_on_destroy", "false");
        });

        void Module(string name, Action<IacHcl> body)
        {
            h.Blank();
            h.Section(name.ToUpperInvariant());
            h.Block($"module \"{name}\"", m =>
            {
                m.Attr("source", Quote($"./modules/{name}"));
                m.Blank();
                body(m);
                m.Blank();
                m.Attr("depends_on", "[google_project_service.apis]");
            });
        }

        if (b.Queues.Count > 0)
            Module("pubsub", m => m.Attr("name_prefix", "local.name_prefix").Attr("flows", QuotedList(b.Queues)).Attr("labels", "local.labels"));
        if (b.Service is { } service)
        {
            Module("artifact", m => m.Attr("name_prefix", "local.name_prefix").Attr("region", "var.region").Attr("labels", "local.labels"));
            Module("secrets", m => m.Attr("name_prefix", "local.name_prefix").Attr("labels", "local.labels"));
            Module("cloudrun", m =>
            {
                m.Attr("name_prefix", "local.name_prefix").Attr("project_id", "var.project_id").Attr("region", "var.region").Attr("service_name", Quote(service.Name))
                    .Attr("registry_path", "module.artifact.registry_path").Attr("repository_name", "module.artifact.repository_name").Attr("image_tag", "var.image_tag")
                    .Attr("port", service.Port.ToString(CultureInfo.InvariantCulture)).Attr("health_path", Quote(service.HealthPath))
                    .Attr("cpu", Quote(Cpu(service.Cpu))).Attr("memory", Quote(Memory(service.Memory)))
                    .Attr("min_instances", service.DesiredCount.ToString(CultureInfo.InvariantCulture))
                    .Attr("max_instances", (service.DesiredCount * 2).ToString(CultureInfo.InvariantCulture)).Attr("secret_id", "module.secrets.secret_id");
                if (b.Queues.Count > 0) m.Attr("topics", "module.pubsub.topics").Attr("subscriptions", "module.pubsub.subscriptions");
                if (b.Database is not null) m.Attr("network_id", "var.network_id").Attr("grant_database_access", "true");
                m.Blank();
                m.Object("environment", e =>
                {
                    e.Attr("APP_SECRET", "module.secrets.secret_id").Attr("GOOGLE_CLOUD_PROJECT", "var.project_id");
                    if (b.Database is not null) e.Attr("DB_HOST", "module.cloudsql.private_ip").Attr("DB_NAME", "module.cloudsql.database_name");
                    foreach (var queue in b.Queues) e.Attr($"TOPIC_{queue.ToUpperInvariant().Replace('-', '_')}", $"module.pubsub.topics[{Quote(queue)}]");
                });
                m.Blank();
                m.Attr("labels", "local.labels");
            });
        }
        if (b.Database is { } database)
            Module("cloudsql", m =>
            {
                m.Attr("name_prefix", "local.name_prefix").Attr("region", "var.region").Attr("database_version", Quote(DatabaseVersion(database)))
                    .Attr("tier", Quote(database.InstanceClass)).Attr("storage_gb", database.StorageGb.ToString(CultureInfo.InvariantCulture))
                    .Attr("regional", database.MultiAz ? "true" : "false").Attr("database_name", Quote(b.Project.Replace('-', '_')))
                    .Attr("network_id", "var.network_id");
                if (b.Service is not null) m.Attr("create_iam_user", "true").Attr("iam_user", "module.cloudrun.service_account_email");
                m.Attr("labels", "local.labels");
            });
        Module("monitoring", m =>
        {
            m.Attr("name_prefix", "local.name_prefix").Attr("project_id", "var.project_id").Attr("alarm_email", "var.alarm_email");
            if (b.Queues.Count > 0) m.Attr("flows", QuotedList(b.Queues));
            if (b.Service is not null) m.Attr("watch_service", "true").Attr("service_name", "module.cloudrun.service_name");
            if (b.Database is not null) m.Attr("watch_database", "true").Attr("database_instance", "module.cloudsql.instance_name");
            m.Attr("labels", "local.labels");
        });
        return h.ToString();
    }

    private static string RootOutputs(IacBlueprint b)
    {
        var outputs = new List<(string, string, string)>();
        if (b.Queues.Count > 0) outputs.Add(("pubsub_topics", "Tópico de cada fluxo", "module.pubsub.topics"));
        if (b.Service is not null)
        {
            outputs.Add(("registry_path", "Caminho onde o CI publica a imagem com a tag do commit", "module.artifact.registry_path"));
            outputs.Add(("service_uri", "Endereço HTTPS público do serviço", "module.cloudrun.uri"));
        }
        if (b.Database is not null) outputs.Add(("database_connection_name", "Nome de conexão do Cloud SQL", "module.cloudsql.connection_name"));
        outputs.Add(("project_id", "Projeto do Google Cloud dos recursos", "var.project_id"));
        return IacAwsGenerator.Outputs([.. outputs]);
    }

    private static string TfvarsExample(IacBlueprint b)
    {
        var h = new IacHcl();
        h.Comment("Copie para terraform.tfvars (no .gitignore) e troque cada valor pelo real do projeto.");
        h.Blank();
        h.Attr("project_id", Quote(b.Account ?? "meu-projeto-123"));
        if (b.Database is not null)
        {
            h.Blank();
            h.Section("Rede existente");
            h.Comment("Rede com acesso privado a serviços (service networking) já configurado; o Terraform não cria rede.");
            h.Attr("network_id", Quote(b.Network.VpcId.Length > 0 ? b.Network.VpcId : $"projects/{b.Account ?? "meu-projeto-123"}/global/networks/default"));
        }
        if (b.Service is not null)
        {
            h.Blank();
            h.Section("Imagem");
            h.Comment("SHA do commit da primeira imagem publicada no Artifact Registry; só vale para o primeiro apply.");
            h.Attr("image_tag", Quote("0000000000000000000000000000000000000000"));
        }
        return h.ToString();
    }

    private static string Readme(IacBlueprint b, string root)
    {
        var dir = root.Length == 0 ? "." : root;
        var rows = new List<(string, string)>
        {
            ("`providers.tf`, `variables.tf`, `resource.tf`, `output.tf`", "Configuração raiz e as APIs usadas. O **workspace selecionado é o ambiente**" +
                (b.Backend is null ? "; sem backend remoto ainda (crie com o bootstrap)" : "; state remoto no GCS com trava nativa")),
            ("`terraform.tfvars.example`", "Formato dos valores que não ficam no repositório; copie para `terraform.tfvars`, que está no .gitignore"),
            ("`modules/monitoring/`", "Canal de e-mail e alertas: mensagem morta, serviço sem instância, CPU do banco")
        };
        if (b.Queues.Count > 0) rows.Add(("`modules/pubsub/`", $"Tópico, assinatura e tópico de mensagens mortas por fluxo ({string.Join(", ", b.Queues.Select(q => $"`{q}`"))}), com 5 entregas antes de desistir"));
        if (b.Service is { } s)
        {
            rows.Add(("`modules/artifact/`", "Repositório Docker com tags imutáveis; a limpeza guarda as 20 imagens mais recentes"));
            rows.Add(("`modules/secrets/`", "Segredo da aplicação no Secret Manager, criado **sem valor**"));
            rows.Add(("`modules/cloudrun/`", $"Conta de serviço própria e serviço `{s.Name}` com HTTPS gerenciado, porta {s.Port} e probe `GET {s.HealthPath}`"));
        }
        if (b.Database is { } d) rows.Add(("`modules/cloudsql/`", $"Cloud SQL {d.Engine} só com IP privado, login IAM (sem senha), backups e proteção contra exclusão"));

        var text = new System.Text.StringBuilder();
        text.Append($"# Infraestrutura (OpenTofu/Terraform)\n\nRecursos Google Cloud de `{b.Project}`, gerados pelo Spock a partir do desenho aprovado, no formato do modelo de projeto.\n\n");
        text.Append("| Caminho | O que é |\n| ------- | ------- |\n");
        foreach (var (path, what) in rows) text.Append($"| {path} | {what} |\n");
        text.Append($"\nRegião `{b.Region}`, projeto `{b.Account}`.\n");
        text.Append("\n## Pré-requisitos\n\n- OpenTofu **≥ 1.11** (ou Terraform ≥ 1.11). O Spock instala e verifica a ferramenta.\n");
        text.Append($"- Provider google fixado em `{IacBootstrapTemplates.GoogleVersion}`.\n- Application Default Credentials (`gcloud auth application-default login`).\n");
        text.Append($"\n## Plan e apply de um ambiente\n\nPela tela Infraestrutura do Spock (validar, gerar plano, aprovar) ou no terminal:\n\n");
        text.Append($"```bash\ntofu -chdir={dir} init\n```\n\n```bash\ntofu -chdir={dir} workspace select dev\n```\n\n```bash\ntofu -chdir={dir} plan -out=dev.tfplan\n```\n\n```bash\ntofu -chdir={dir} apply dev.tfplan\n```\n\n");
        text.Append("- No workspace `default` o plan falha de propósito.\n- Depois de um apply, `plan -detailed-exitcode` deve informar `No changes`.\n");
        if (b.Service is not null)
            text.Append("\n## Primeiro deploy\n\n1. Aplique as APIs, o repositório e o segredo (`-target=google_project_service.apis -target=module.artifact -target=module.secrets`).\n" +
                        "2. Grave a primeira versão do segredo fora do Terraform (console ou gcloud), a partir de um arquivo fora do repositório.\n" +
                        "3. Publique a primeira imagem no Artifact Registry com a tag do commit e use essa tag em `image_tag`.\n" +
                        "4. Aplique o restante. Depois disso o CI é dono da imagem do serviço (`ignore_changes`).\n");
        if (b.Database is not null)
            text.Append("\n## Banco\n\nSem senha: a conta de serviço da aplicação entra pelo login IAM do Cloud SQL. A rede precisa ter o acesso privado a serviços configurado antes do apply.\n");
        text.Append("\n## Verificações\n\n```bash\ntofu fmt -check -recursive " + dir + "\n```\n\n");
        text.Append("Rode `tofu validate` em cada pasta com `.tf` depois de `tofu init -backend=false`.\n");
        return text.ToString();
    }

    private const string MonitoringResource = """
        # ------------------------- Notification channel ----------------------
        resource "google_monitoring_notification_channel" "email" {
          count = var.alarm_email == null ? 0 : 1

          display_name = "${var.name_prefix} alarmes"
          type         = "email"
          user_labels  = var.labels

          labels = {
            email_address = var.alarm_email
          }
        }

        # ------------------------- Dead-letter alerts ------------------------
        # Uma mensagem no tópico de mensagens mortas é uma falha que se repetiu 5 vezes.
        resource "google_monitoring_alert_policy" "dead_letters" {
          for_each = toset(var.flows)

          display_name          = "${var.name_prefix}-${each.key}: mensagens mortas"
          combiner              = "OR"
          notification_channels = google_monitoring_notification_channel.email[*].id
          user_labels           = var.labels

          conditions {
            display_name = "Mensagens na assinatura ${var.name_prefix}-${each.key}-dlq"

            condition_threshold {
              filter          = "resource.type = \"pubsub_subscription\" AND resource.labels.subscription_id = \"${var.name_prefix}-${each.key}-dlq\" AND metric.type = \"pubsub.googleapis.com/subscription/num_undelivered_messages\""
              comparison      = "COMPARISON_GT"
              threshold_value = 0
              duration        = "300s"

              aggregations {
                alignment_period   = "300s"
                per_series_aligner = "ALIGN_MAX"
              }
            }
          }
        }

        # ------------------------- Service alert -----------------------------
        # Serviço sem nenhuma instância: processo caindo, imagem que não puxa ou segredo ausente.
        resource "google_monitoring_alert_policy" "no_instances" {
          count = var.watch_service ? 1 : 0

          display_name          = "${var.name_prefix}: serviço sem instância"
          combiner              = "OR"
          notification_channels = google_monitoring_notification_channel.email[*].id
          user_labels           = var.labels

          conditions {
            display_name = "Cloud Run sem instância"

            condition_threshold {
              filter          = "resource.type = \"cloud_run_revision\" AND resource.labels.service_name = \"${var.service_name}\" AND metric.type = \"run.googleapis.com/container/instance_count\""
              comparison      = "COMPARISON_LT"
              threshold_value = 1
              duration        = "600s"

              aggregations {
                alignment_period     = "300s"
                per_series_aligner   = "ALIGN_MAX"
                cross_series_reducer = "REDUCE_SUM"
              }
            }
          }
        }

        # ------------------------- Database alert ----------------------------
        resource "google_monitoring_alert_policy" "database_cpu" {
          count = var.watch_database ? 1 : 0

          display_name          = "${var.name_prefix}: CPU do banco"
          combiner              = "OR"
          notification_channels = google_monitoring_notification_channel.email[*].id
          user_labels           = var.labels

          conditions {
            display_name = "CPU acima de 80% por 15 minutos"

            condition_threshold {
              filter          = "resource.type = \"cloudsql_database\" AND resource.labels.database_id = \"${var.project_id}:${var.database_instance}\" AND metric.type = \"cloudsql.googleapis.com/database/cpu/utilization\""
              comparison      = "COMPARISON_GT"
              threshold_value = 0.8
              duration        = "900s"

              aggregations {
                alignment_period   = "300s"
                per_series_aligner = "ALIGN_MEAN"
              }
            }
          }
        }

        """;

    private const string PubSubResource = """
        data "google_project" "this" {}

        locals {
          pubsub_agent = "serviceAccount:service-${data.google_project.this.number}@gcp-sa-pubsub.iam.gserviceaccount.com"
        }

        # ------------------------- Dead-letter topics ------------------------
        resource "google_pubsub_topic" "dead_letter" {
          for_each = toset(var.flows)

          name   = "${var.name_prefix}-${each.key}-dlq"
          labels = var.labels
        }

        resource "google_pubsub_subscription" "dead_letter" {
          for_each = toset(var.flows)

          name                       = "${var.name_prefix}-${each.key}-dlq"
          topic                      = google_pubsub_topic.dead_letter[each.key].id
          message_retention_duration = "1209600s"
          labels                     = var.labels
        }

        # ------------------------- Topics and subscriptions ------------------
        # Depois de 5 entregas a mensagem vai para o tópico de mensagens mortas do fluxo.
        resource "google_pubsub_topic" "main" {
          for_each = toset(var.flows)

          name   = "${var.name_prefix}-${each.key}"
          labels = var.labels
        }

        resource "google_pubsub_subscription" "main" {
          for_each = toset(var.flows)

          name                       = "${var.name_prefix}-${each.key}"
          topic                      = google_pubsub_topic.main[each.key].id
          ack_deadline_seconds       = 180
          message_retention_duration = "345600s"
          labels                     = var.labels

          dead_letter_policy {
            dead_letter_topic     = google_pubsub_topic.dead_letter[each.key].id
            max_delivery_attempts = 5
          }
        }

        # O agente do Pub/Sub precisa publicar no tópico de mensagens mortas e confirmar na assinatura original.
        resource "google_pubsub_topic_iam_member" "dead_letter_publisher" {
          for_each = toset(var.flows)

          topic  = google_pubsub_topic.dead_letter[each.key].id
          role   = "roles/pubsub.publisher"
          member = local.pubsub_agent
        }

        resource "google_pubsub_subscription_iam_member" "dead_letter_subscriber" {
          for_each = toset(var.flows)

          subscription = google_pubsub_subscription.main[each.key].id
          role         = "roles/pubsub.subscriber"
          member       = local.pubsub_agent
        }

        """;

    private const string ArtifactResource = """
        # ------------------------- Repository --------------------------------
        resource "google_artifact_registry_repository" "this" {
          location      = var.region
          repository_id = var.name_prefix
          format        = "DOCKER"
          labels        = var.labels

          docker_config {
            immutable_tags = true
          }

          cleanup_policies {
            id     = "keep-recent"
            action = "KEEP"

            most_recent_versions {
              keep_count = 20
            }
          }

          cleanup_policies {
            id     = "delete-old"
            action = "DELETE"

            condition {
              tag_state  = "ANY"
              older_than = "2592000s"
            }
          }
        }

        """;

    private const string SecretsResource = """
        # ------------------------- Application secret ------------------------
        # Criado sem versão: o valor é gravado fora do Terraform e nunca entra no state.
        resource "google_secret_manager_secret" "app" {
          secret_id = "${var.name_prefix}-app"
          labels    = var.labels

          replication {
            auto {}
          }
        }

        """;

    private const string CloudRunResource = """
        # ------------------------- Service account ---------------------------
        # A aplicação usa só esta conta: lê o segredo, puxa a imagem, usa as filas e entra no banco, sem chave.
        resource "google_service_account" "app" {
          account_id   = substr("${var.name_prefix}-${var.service_name}", 0, 30)
          display_name = "${var.name_prefix} ${var.service_name}"
        }

        resource "google_secret_manager_secret_iam_member" "app" {
          secret_id = var.secret_id
          role      = "roles/secretmanager.secretAccessor"
          member    = "serviceAccount:${google_service_account.app.email}"
        }

        resource "google_artifact_registry_repository_iam_member" "app" {
          repository = var.repository_name
          location   = var.region
          role       = "roles/artifactregistry.reader"
          member     = "serviceAccount:${google_service_account.app.email}"
        }

        resource "google_pubsub_topic_iam_member" "publisher" {
          for_each = var.topics

          topic  = each.value
          role   = "roles/pubsub.publisher"
          member = "serviceAccount:${google_service_account.app.email}"
        }

        resource "google_pubsub_subscription_iam_member" "subscriber" {
          for_each = var.subscriptions

          subscription = each.value
          role         = "roles/pubsub.subscriber"
          member       = "serviceAccount:${google_service_account.app.email}"
        }

        resource "google_project_iam_member" "database" {
          for_each = var.grant_database_access ? toset(["roles/cloudsql.client", "roles/cloudsql.instanceUser"]) : toset([])

          project = var.project_id
          role    = each.key
          member  = "serviceAccount:${google_service_account.app.email}"
        }

        # ------------------------- Service -----------------------------------
        resource "google_cloud_run_v2_service" "this" {
          name     = "${var.name_prefix}-${var.service_name}"
          location = var.region
          ingress  = "INGRESS_TRAFFIC_ALL"
          labels   = var.labels

          template {
            service_account = google_service_account.app.email

            scaling {
              min_instance_count = var.min_instances
              max_instance_count = var.max_instances
            }

            dynamic "vpc_access" {
              for_each = var.network_id == null ? [] : [var.network_id]

              content {
                egress = "PRIVATE_RANGES_ONLY"

                network_interfaces {
                  network = vpc_access.value
                }
              }
            }

            containers {
              image = "${var.registry_path}/${var.service_name}:${var.image_tag}"

              ports {
                container_port = var.port
              }

              resources {
                limits = {
                  cpu    = var.cpu
                  memory = var.memory
                }
              }

              dynamic "env" {
                for_each = var.environment

                content {
                  name  = env.key
                  value = env.value
                }
              }

              liveness_probe {
                http_get {
                  path = var.health_path
                }
              }
            }
          }

          # Depois do primeiro apply o CI é o dono da imagem.
          lifecycle {
            ignore_changes = [template[0].containers[0].image, client, client_version]
          }

          depends_on = [google_secret_manager_secret_iam_member.app, google_artifact_registry_repository_iam_member.app]
        }

        # API pública por HTTPS, como o load balancer público do modelo.
        resource "google_cloud_run_v2_service_iam_member" "public" {
          name     = google_cloud_run_v2_service.this.name
          location = var.region
          role     = "roles/run.invoker"
          member   = "allUsers"
        }

        """;

    private static string CloudSqlResource(string engine)
    {
        var postgres = engine != "mysql";
        var flag = postgres ? "cloudsql.iam_authentication" : "cloudsql_iam_authentication";
        // O fmt alinha o "=" pelo atributo mais longo do bloco: os dois motores pedem recuos diferentes.
        var backup = postgres
            ? "enabled                        = true" + Environment.NewLine.Replace("\r", "") + "      point_in_time_recovery_enabled = true"
            : "enabled            = true" + Environment.NewLine.Replace("\r", "") + "      binary_log_enabled = true";
        var userName = postgres ? "trimsuffix(var.iam_user, \".gserviceaccount.com\")" : "split(\"@\", var.iam_user)[0]";
        return $$"""
            # ------------------------- Instance ----------------------------------
            # Só IP privado e login IAM: o banco não fica na internet e não existe senha para vazar.
            resource "google_sql_database_instance" "this" {
              name                = "${var.name_prefix}-db"
              region              = var.region
              database_version    = var.database_version
              deletion_protection = true

              settings {
                tier              = var.tier
                availability_type = var.regional ? "REGIONAL" : "ZONAL"
                disk_size         = var.storage_gb
                disk_type         = "PD_SSD"
                disk_autoresize   = true
                user_labels       = var.labels

                ip_configuration {
                  ipv4_enabled    = false
                  private_network = var.network_id
                  ssl_mode        = "ENCRYPTED_ONLY"
                }

                backup_configuration {
                  {{backup}}
                }

                database_flags {
                  name  = "{{flag}}"
                  value = "on"
                }
              }
            }

            resource "google_sql_database" "app" {
              name     = var.database_name
              instance = google_sql_database_instance.this.name
            }

            # ------------------------- IAM user ----------------------------------
            resource "google_sql_user" "app" {
              count = var.create_iam_user ? 1 : 0

              name     = {{userName}}
              instance = google_sql_database_instance.this.name
              type     = "CLOUD_IAM_SERVICE_ACCOUNT"
            }

            """;
    }
}
