using System.Globalization;
using static Mariliz.Application.Iac.IacHcl;

namespace Mariliz.Application.Iac;

// O mesmo modelo de pasta da AWS (TERRAFORM_PROJECT_LAYOUT.md) com os serviços equivalentes do Azure: Container Apps no
// lugar do ECS com load balancer, Azure Container Registry, Key Vault, PostgreSQL Flexible Server só com login do Entra ID
// (sem senha), Service Bus com fila de mensagens mortas embutida e alarmes do Azure Monitor. Um grupo de recursos por
// ambiente. Os módulos são texto fixo; o desenho entra pela raiz.
public static class IacAzureGenerator
{
    public static IReadOnlyList<IacGeneratedFile> Generate(IacBlueprint b, string root)
    {
        string P(string relative) => root.Length == 0 ? relative : $"{root}/{relative}";
        var files = new List<IacGeneratedFile>
        {
            new(P("providers.tf"), Providers(b)),
            new(P("variables.tf"), RootVariables(b)),
            new(P("resource.tf"), RootResources(b)),
            new(P("output.tf"), RootOutputs(b)),
            new(P("terraform.tfvars.example"), TfvarsExample(b)),
            new(P("README.md"), Readme(b, root)),
            new(P("modules/monitor/resource.tf"), MonitorResource),
            new(P("modules/monitor/variables.tf"), IacModuleText.ModuleVariables(v => v
                .Required("resource_group_name", "string", "Grupo de recursos do ambiente")
                .Nullable("alarm_email", "string", "E-mail que recebe os alarmes; null cria o grupo de ação sem destinatário")
                .Default("queues", "list(string)", "Filas com alarme de mensagens mortas", "[]")
                .Nullable("servicebus_namespace_id", "string", "Namespace do Service Bus das filas")
                .Default("watch_service", "bool", "Se há Container App para vigiar", "false")
                .Nullable("container_app_id", "string", "Container App vigiado")
                .Default("watch_database", "bool", "Se há banco para vigiar", "false")
                .Nullable("postgres_server_id", "string", "Servidor PostgreSQL vigiado"))),
            new(P("modules/monitor/outputs.tf"), IacModuleText.Outputs(("action_group_id", "Grupo de ação que recebe os alarmes", "azurerm_monitor_action_group.alarms.id")))
        };

        if (b.Queues.Count > 0)
        {
            files.Add(new(P("modules/servicebus/resource.tf"), ServiceBusResource));
            files.Add(new(P("modules/servicebus/variables.tf"), IacModuleText.ModuleVariables(v => v
                .Required("resource_group_name", "string", "Grupo de recursos do ambiente")
                .Required("location", "string", "Região do Azure")
                .Required("flows", "list(string)", "Fluxos de mensagens; cada um vira uma fila com fila de mensagens mortas. Renomear destrói a fila"))));
            files.Add(new(P("modules/servicebus/outputs.tf"), IacModuleText.Outputs(
                ("namespace_id", "Namespace do Service Bus", "azurerm_servicebus_namespace.this.id"),
                ("namespace_endpoint", "Endpoint do namespace, que a aplicação usa com a identidade gerenciada", "azurerm_servicebus_namespace.this.endpoint"),
                ("queues", "Id de cada fila, por fluxo", "{ for flow, queue in azurerm_servicebus_queue.main : flow => queue.id }"))));
        }

        if (b.Service is not null)
        {
            files.Add(new(P("modules/acr/resource.tf"), AcrResource));
            files.Add(new(P("modules/acr/variables.tf"), IacModuleText.ModuleVariables(v => v
                .Required("resource_group_name", "string", "Grupo de recursos do ambiente")
                .Required("location", "string", "Região do Azure"))));
            files.Add(new(P("modules/acr/outputs.tf"), IacModuleText.Outputs(
                ("registry_id", "Registro de imagens", "azurerm_container_registry.this.id"),
                ("login_server", "Servidor onde o CI publica a imagem com a tag do commit", "azurerm_container_registry.this.login_server"))));
            files.Add(new(P("modules/keyvault/resource.tf"), KeyVaultResource));
            files.Add(new(P("modules/keyvault/variables.tf"), IacModuleText.ModuleVariables(v => v
                .Required("resource_group_name", "string", "Grupo de recursos do ambiente")
                .Required("location", "string", "Região do Azure"))));
            files.Add(new(P("modules/keyvault/outputs.tf"), IacModuleText.Outputs(
                ("key_vault_id", "Key Vault da aplicação; os segredos são gravados fora do Terraform", "azurerm_key_vault.this.id"),
                ("key_vault_uri", "Endereço do Key Vault, que a aplicação lê com a identidade gerenciada", "azurerm_key_vault.this.vault_uri"))));
            files.Add(new(P("modules/containerapp/resource.tf"), ContainerAppResource));
            files.Add(new(P("modules/containerapp/variables.tf"), IacModuleText.ModuleVariables(v => v
                .Required("resource_group_name", "string", "Grupo de recursos do ambiente")
                .Required("location", "string", "Região do Azure")
                .Required("service_name", "string", "Nome do serviço; também é o nome da imagem")
                .Required("registry_id", "string", "Registro de onde a imagem é puxada")
                .Required("registry_server", "string", "Servidor do registro")
                .Required("image_tag", "string", "Tag da imagem inicial; depois o CI é o dono da revisão")
                .Required("port", "number", "Porta do contêiner")
                .Required("health_path", "string", "Caminho do health check")
                .Required("cpu", "number", "vCPU do contêiner (0.25 a 2)")
                .Required("memory", "string", "Memória do contêiner (o dobro da CPU, em Gi)")
                .Required("min_replicas", "number", "Réplicas sempre ligadas")
                .Required("max_replicas", "number", "Teto de réplicas")
                .Required("key_vault_id", "string", "Key Vault que a aplicação lê")
                .Default("grant_queue_access", "bool", "Se a identidade da aplicação envia e recebe nas filas", "false")
                .Nullable("servicebus_namespace_id", "string", "Namespace das filas")
                .Default("environment", "map(string)", "Variáveis de ambiente do contêiner (nunca segredo)", "{}"))));
            files.Add(new(P("modules/containerapp/outputs.tf"), IacModuleText.Outputs(
                ("container_app_id", "Container App do serviço", "azurerm_container_app.this.id"),
                ("fqdn", "Endereço HTTPS público do serviço", "azurerm_container_app.this.ingress[0].fqdn"),
                ("principal_id", "Identidade gerenciada da aplicação, para dar acesso ao banco", "azurerm_user_assigned_identity.app.principal_id"),
                ("identity_name", "Nome da identidade gerenciada (o usuário dela no banco)", "azurerm_user_assigned_identity.app.name"))));
        }

        if (b.Database is not null)
        {
            files.Add(new(P("modules/postgres/resource.tf"), PostgresResource));
            files.Add(new(P("modules/postgres/variables.tf"), IacModuleText.ModuleVariables(v => v
                .Required("resource_group_name", "string", "Grupo de recursos do ambiente")
                .Required("location", "string", "Região do Azure")
                .Required("engine_version", "string", "Versão principal do PostgreSQL")
                .Required("sku_name", "string", "SKU do Flexible Server")
                .Required("storage_gb", "number", "Armazenamento em GB")
                .Required("zone_redundant", "bool", "Réplica em outra zona (fora da camada Burstable)")
                .Required("database_name", "string", "Nome do banco criado")
                .Required("subnet_id", "string", "Subnet delegada ao PostgreSQL")
                .Required("private_dns_zone_id", "string", "Zona DNS privada do PostgreSQL")
                .Nullable("admin_object_id", "string", "Objeto do Entra ID (grupo de DBAs) que administra o banco; null deixa sem administrador")
                .Nullable("admin_name", "string", "Nome do administrador do Entra ID")
                .Default("admin_type", "string", "Tipo do administrador: Group, User ou ServicePrincipal", "\"Group\""))));
            files.Add(new(P("modules/postgres/outputs.tf"), IacModuleText.Outputs(
                ("server_id", "Servidor PostgreSQL", "azurerm_postgresql_flexible_server.this.id"),
                ("fqdn", "Endereço privado do banco", "azurerm_postgresql_flexible_server.this.fqdn"),
                ("database_name", "Nome do banco criado", "azurerm_postgresql_flexible_server_database.app.name"))));
        }
        return files;
    }

    public static IReadOnlyList<IacCostItem> Costs(IacBlueprint b)
    {
        var items = new List<IacCostItem>();
        if (b.Service is { } s)
        {
            items.Add(new($"Container Apps ({s.DesiredCount} réplica(s) mínima(s), {Cpu(s.Cpu)} vCPU, {Memory(s.Memory)})", "Por vCPU-segundo e GiB-segundo das réplicas ativas e por requisição, com uma franquia mensal por subscription."));
            items.Add(new("Azure Container Registry (Basic)", "Por dia, com armazenamento incluso até o limite do plano."));
            items.Add(new("Key Vault", "Por operação nos segredos."));
            items.Add(new("Log Analytics", "Por GB ingerido e retido (30 dias)."));
        }
        if (b.Database is { } d)
            items.Add(new($"PostgreSQL Flexible Server {d.InstanceClass}{(d.MultiAz ? " com redundância de zona" : "")}", $"Computação por hora, {d.StorageGb} GB de armazenamento por mês e backup além do tamanho do banco."));
        if (b.Queues.Count > 0) items.Add(new($"Service Bus Standard ({b.Queues.Count} fila(s))", "Valor base por hora do namespace e por milhão de operações."));
        items.Add(new("Azure Monitor (alertas e grupo de ação)", "Por regra de alerta por mês e por notificação."));
        items.Add(new("Transferência de dados", "Saída para a internet por GB."));
        return items;
    }

    private static string Cpu(int units) => (units / 1024m).ToString("0.##", CultureInfo.InvariantCulture);

    private static string Memory(int megabytes) => (megabytes / 1024m).ToString("0.##", CultureInfo.InvariantCulture) + "Gi";

    private static string Providers(IacBlueprint b)
    {
        var h = new IacHcl();
        h.Block("terraform", t =>
        {
            t.Attr("required_version", Quote(">= 1.11"));
            t.Blank();
            t.Block("required_providers", r => r.Object("azurerm", o => o.Attr("source", Quote("hashicorp/azurerm")).Attr("version", Quote(IacBootstrapTemplates.AzurermVersion))));
            t.Blank();
            if (b.Backend is { } backend)
            {
                t.Comment("Cada workspace ganha o próprio state no container (o azurerm acrescenta env:<workspace> à chave).");
                t.Block("backend \"azurerm\"", s => s.Attr("resource_group_name", Quote(backend.ResourceGroup ?? "")).Attr("storage_account_name", Quote(backend.Bucket))
                    .Attr("container_name", Quote(backend.Container ?? "")).Attr("key", Quote($"{b.Project}.tfstate")).Attr("use_azuread_auth", "true"));
            }
            else
            {
                t.Comment("Sem backend remoto ainda: crie a storage account com o bootstrap (tela Infraestrutura, State) e cole aqui o bloco backend \"azurerm\".");
            }
        });
        h.Blank();
        h.Block("provider \"azurerm\"", p =>
        {
            p.Raw("features {}");
            p.Blank();
            p.Attr("subscription_id", "var.subscription_id").Attr("tenant_id", "var.tenant_id");
        });
        return h.ToString();
    }

    private static string RootVariables(IacBlueprint b)
    {
        var v = new IacModuleText.Variables();
        v.Validated("subscription_id", "string", "Subscription dos recursos; o provider recusa operar em outra",
            "can(regex(\"^[0-9a-fA-F-]{36}$\", var.subscription_id))", "A subscription deve ser um GUID.", "Subscription");
        v.Default("tenant_id", "string", "Tenant do Entra ID; null usa o da credencial", b.Profile is null ? "null" : Quote(b.Profile));
        v.Default("location", "string", "Região do Azure onde os recursos são criados", Quote(b.Region), "Location");
        if (b.Database is not null)
        {
            v.Validated("postgres_subnet_id", "string", "Subnet existente delegada a Microsoft.DBforPostgreSQL/flexibleServers (o Terraform não cria rede)",
                "can(regex(\"/subnets/[^/]+$\", var.postgres_subnet_id))", "Informe o id completo da subnet delegada.", "Rede existente");
            v.Validated("postgres_private_dns_zone_id", "string", "Zona DNS privada *.postgres.database.azure.com ligada à VNet",
                "can(regex(\"privateDnsZones/[^/]+\\\\.postgres\\\\.database\\\\.azure\\\\.com$\", var.postgres_private_dns_zone_id))",
                "Informe o id completo da zona DNS privada do PostgreSQL.");
            v.Nullable("postgres_admin_object_id", "string", "Objeto do Entra ID (grupo de DBAs) administrador do banco; null deixa sem administrador");
            v.Nullable("postgres_admin_name", "string", "Nome desse administrador no Entra ID");
        }
        if (b.Service is not null)
            v.Validated("image_tag", "string", "Tag da imagem inicial; só vale para o primeiro apply, porque depois o CI é o dono da revisão",
                "can(regex(\"^[A-Za-z0-9_.-]{1,128}$\", var.image_tag)) && var.image_tag != \"latest\"",
                "Use uma tag imutável (o SHA do commit), nunca latest.", "Imagem");
        v.Default("alarm_email", "string", "E-mail que recebe os alarmes; null cria o grupo de ação sem destinatário",
            b.AlarmEmail is null ? "null" : Quote(b.AlarmEmail), "Alarmes");
        return v.ToString();
    }

    private static string RootResources(IacBlueprint b)
    {
        var h = new IacHcl();
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
            l.Object("tags", t => t.Attr("Project", Quote(b.Project)).Attr("Environment", "terraform.workspace").Attr("ManagedBy", Quote("terraform")));
        });
        h.Blank();
        h.Section("Resource group");
        h.Comment("Um grupo de recursos por ambiente: apagar o ambiente é apagar o grupo.");
        h.Block("resource \"azurerm_resource_group\" \"main\"", r => r.Attr("name", Quote("${local.name_prefix}-rg")).Attr("location", "var.location").Attr("tags", "local.tags"));

        void Module(string name, Action<IacHcl> body)
        {
            h.Blank();
            h.Section(name.ToUpperInvariant());
            h.Block($"module \"{name}\"", m =>
            {
                m.Attr("source", Quote($"./modules/{name}"));
                m.Blank();
                body(m);
            });
        }
        void Common(IacHcl m, bool location = true)
        {
            m.Attr("name_prefix", "local.name_prefix").Attr("resource_group_name", "azurerm_resource_group.main.name");
            if (location) m.Attr("location", "azurerm_resource_group.main.location");
        }

        if (b.Queues.Count > 0)
            Module("servicebus", m =>
            {
                Common(m);
                m.Attr("flows", QuotedList(b.Queues)).Attr("tags", "local.tags");
            });
        if (b.Service is { } service)
        {
            Module("acr", m =>
            {
                Common(m);
                m.Attr("tags", "local.tags");
            });
            Module("keyvault", m =>
            {
                Common(m);
                m.Attr("tags", "local.tags");
            });
            Module("containerapp", m =>
            {
                Common(m);
                m.Attr("service_name", Quote(service.Name)).Attr("registry_id", "module.acr.registry_id").Attr("registry_server", "module.acr.login_server")
                    .Attr("image_tag", "var.image_tag").Attr("port", service.Port.ToString(CultureInfo.InvariantCulture)).Attr("health_path", Quote(service.HealthPath))
                    .Attr("cpu", Cpu(service.Cpu)).Attr("memory", Quote(Memory(service.Memory)))
                    .Attr("min_replicas", service.DesiredCount.ToString(CultureInfo.InvariantCulture))
                    .Attr("max_replicas", (service.DesiredCount * 2).ToString(CultureInfo.InvariantCulture)).Attr("key_vault_id", "module.keyvault.key_vault_id");
                if (b.Queues.Count > 0) m.Attr("grant_queue_access", "true").Attr("servicebus_namespace_id", "module.servicebus.namespace_id");
                m.Blank();
                m.Object("environment", e =>
                {
                    e.Attr("PORT", Quote(service.Port.ToString(CultureInfo.InvariantCulture))).Attr("KEY_VAULT_URI", "module.keyvault.key_vault_uri");
                    if (b.Database is not null) e.Attr("DB_HOST", "module.postgres.fqdn").Attr("DB_NAME", "module.postgres.database_name");
                    if (b.Queues.Count > 0) e.Attr("SERVICEBUS_ENDPOINT", "module.servicebus.namespace_endpoint");
                });
                m.Blank();
                m.Attr("tags", "local.tags");
            });
        }
        if (b.Database is { } database)
            Module("postgres", m =>
            {
                Common(m);
                m.Attr("engine_version", Quote(database.EngineVersion)).Attr("sku_name", Quote(database.InstanceClass))
                    .Attr("storage_gb", database.StorageGb.ToString(CultureInfo.InvariantCulture)).Attr("zone_redundant", database.MultiAz ? "true" : "false")
                    .Attr("database_name", Quote(b.Project.Replace('-', '_'))).Attr("subnet_id", "var.postgres_subnet_id")
                    .Attr("private_dns_zone_id", "var.postgres_private_dns_zone_id").Attr("admin_object_id", "var.postgres_admin_object_id")
                    .Attr("admin_name", "var.postgres_admin_name").Attr("tags", "local.tags");
            });
        Module("monitor", m =>
        {
            Common(m, location: false);
            m.Attr("alarm_email", "var.alarm_email");
            if (b.Queues.Count > 0) m.Attr("queues", QuotedList(b.Queues)).Attr("servicebus_namespace_id", "module.servicebus.namespace_id");
            if (b.Service is not null) m.Attr("watch_service", "true").Attr("container_app_id", "module.containerapp.container_app_id");
            if (b.Database is not null) m.Attr("watch_database", "true").Attr("postgres_server_id", "module.postgres.server_id");
            m.Attr("tags", "local.tags");
        });
        return h.ToString();
    }

    private static string RootOutputs(IacBlueprint b)
    {
        var outputs = new List<(string, string, string)> { ("resource_group_name", "Grupo de recursos do ambiente", "azurerm_resource_group.main.name") };
        if (b.Queues.Count > 0) outputs.Add(("servicebus_queues", "Id de cada fila do Service Bus, por fluxo", "module.servicebus.queues"));
        if (b.Service is not null)
        {
            outputs.Add(("registry_login_server", "Registro onde o CI publica a imagem com a tag do commit", "module.acr.login_server"));
            outputs.Add(("service_fqdn", "Endereço HTTPS público do serviço", "module.containerapp.fqdn"));
        }
        if (b.Database is not null) outputs.Add(("database_fqdn", "Endereço privado do banco", "module.postgres.fqdn"));
        return IacModuleText.Outputs([.. outputs]);
    }

    private static string TfvarsExample(IacBlueprint b)
    {
        var h = new IacHcl();
        h.Comment("Copie para terraform.tfvars (no .gitignore) e troque cada valor pelo real da subscription.");
        h.Blank();
        h.Attr("subscription_id", Quote(b.Account ?? "00000000-0000-0000-0000-000000000000"));
        if (b.Database is not null)
        {
            h.Blank();
            h.Section("Rede existente");
            h.Comment("Subnet delegada ao PostgreSQL e zona DNS privada ligada à VNet; o Terraform não cria rede.");
            h.Attr("postgres_subnet_id", Quote(b.Network.PrivateSubnets.Count > 0 ? b.Network.PrivateSubnets[0]
                : "/subscriptions/00000000-0000-0000-0000-000000000000/resourceGroups/rede/providers/Microsoft.Network/virtualNetworks/vnet/subnets/postgres"));
            h.Attr("postgres_private_dns_zone_id", Quote(b.Network.PrivateDnsZone
                ?? "/subscriptions/00000000-0000-0000-0000-000000000000/resourceGroups/rede/providers/Microsoft.Network/privateDnsZones/loja.postgres.database.azure.com"));
        }
        if (b.Service is not null)
        {
            h.Blank();
            h.Section("Imagem");
            h.Comment("SHA do commit da primeira imagem publicada no registro; só vale para o primeiro apply.");
            h.Attr("image_tag", Quote("0000000000000000000000000000000000000000"));
        }
        return h.ToString();
    }

    private static string Readme(IacBlueprint b, string root)
    {
        var dir = root.Length == 0 ? "." : root;
        var rows = new List<(string, string)>
        {
            ("`providers.tf`, `variables.tf`, `resource.tf`, `output.tf`", "Configuração raiz e o grupo de recursos do ambiente. O **workspace selecionado é o ambiente**" +
                (b.Backend is null ? "; sem backend remoto ainda (crie com o bootstrap)" : "; state remoto no Blob Storage com lease e login do Entra ID")),
            ("`terraform.tfvars.example`", "Formato dos valores que não ficam no repositório; copie para `terraform.tfvars`, que está no .gitignore"),
            ("`modules/monitor/`", "Grupo de ação e alertas: mensagem morta nas filas, serviço sem réplica, CPU do banco")
        };
        if (b.Queues.Count > 0) rows.Add(("`modules/servicebus/`", $"Namespace Standard sem chave local e filas ({string.Join(", ", b.Queues.Select(q => $"`{q}`"))}), cada uma com 5 entregas antes da fila de mensagens mortas"));
        if (b.Service is { } s)
        {
            rows.Add(("`modules/acr/`", "Registro de imagens sem usuário administrador; a aplicação puxa com a identidade gerenciada"));
            rows.Add(("`modules/keyvault/`", "Key Vault com RBAC e proteção contra exclusão; segredos gravados **fora** do Terraform"));
            rows.Add(("`modules/containerapp/`", $"Log Analytics (30 dias), ambiente e Container App `{s.Name}` com HTTPS gerenciado, porta {s.Port} e probe `GET {s.HealthPath}`"));
        }
        if (b.Database is { } d) rows.Add(("`modules/postgres/`", $"PostgreSQL {d.EngineVersion} privado na subnet delegada, só login do Entra ID (sem senha), protegido contra destroy"));

        var text = new System.Text.StringBuilder();
        text.Append($"# Infraestrutura (OpenTofu/Terraform)\n\nRecursos Azure de `{b.Project}`, gerados pelo Spock a partir do desenho aprovado, no formato do modelo de projeto.\n\n");
        text.Append("| Caminho | O que é |\n| ------- | ------- |\n");
        foreach (var (path, what) in rows) text.Append($"| {path} | {what} |\n");
        text.Append($"\nRegião `{b.Region}`, subscription `{b.Account}`: o provider opera só nela.\n");
        text.Append("\n## Pré-requisitos\n\n- OpenTofu **≥ 1.11** (ou Terraform ≥ 1.11). O Spock instala e verifica a ferramenta.\n");
        text.Append($"- Provider azurerm fixado em `{IacBootstrapTemplates.AzurermVersion}`.\n- Login com `az login` (ou a credencial da máquina) na subscription.\n");
        text.Append($"\n## Plan e apply de um ambiente\n\nPela tela Infraestrutura do Spock (validar, gerar plano, aprovar) ou no terminal:\n\n");
        text.Append($"```bash\ntofu -chdir={dir} init\n```\n\n```bash\ntofu -chdir={dir} workspace select dev\n```\n\n```bash\ntofu -chdir={dir} plan -out=dev.tfplan\n```\n\n```bash\ntofu -chdir={dir} apply dev.tfplan\n```\n\n");
        text.Append("- No workspace `default` o plan falha de propósito.\n- Depois de um apply, `plan -detailed-exitcode` deve informar `No changes`.\n");
        text.Append("- Nomes globais (registro, Key Vault, namespace) saem do projeto e do ambiente; se já existirem em outra subscription, troque o projeto.\n");
        if (b.Service is not null)
            text.Append("\n## Primeiro deploy\n\n1. Aplique registro e Key Vault (`-target=module.acr -target=module.keyvault`).\n" +
                        "2. Grave os segredos da aplicação no Key Vault fora do Terraform.\n" +
                        "3. Publique a primeira imagem no registro com a tag do commit e use essa tag em `image_tag`.\n" +
                        "4. Aplique o restante. Depois disso o CI é dono da imagem do Container App (`ignore_changes`).\n");
        if (b.Database is not null)
            text.Append("\n## Banco\n\nSem senha: o acesso é pelo Entra ID. Defina `postgres_admin_object_id` (um grupo de DBAs) e, como administrador, crie o usuário da identidade da aplicação " +
                        "(`identity_name` do módulo containerapp) com `pgaadauth_create_principal`.\n");
        text.Append("\n## Verificações\n\n```bash\ntofu fmt -check -recursive " + dir + "\n```\n\n");
        text.Append("Rode `tofu validate` em cada pasta com `.tf` depois de `tofu init -backend=false`.\n");
        return text.ToString();
    }

    private const string MonitorResource = """
        # ------------------------- Action group ------------------------------
        resource "azurerm_monitor_action_group" "alarms" {
          name                = "${var.name_prefix}-alarms"
          resource_group_name = var.resource_group_name
          short_name          = "alarms"
          tags                = var.tags

          dynamic "email_receiver" {
            for_each = var.alarm_email == null ? [] : [var.alarm_email]

            content {
              name          = "email"
              email_address = email_receiver.value
            }
          }
        }

        # ------------------------- Dead-letter alarms ------------------------
        # Uma mensagem morta é uma falha que se repetiu 5 vezes.
        resource "azurerm_monitor_metric_alert" "dead_letters" {
          for_each = toset(var.queues)

          name                = "${var.name_prefix}-${each.key}-dlq"
          resource_group_name = var.resource_group_name
          scopes              = [var.servicebus_namespace_id]
          description         = "Mensagens na fila de mensagens mortas de ${each.key}"
          frequency           = "PT5M"
          window_size         = "PT5M"
          tags                = var.tags

          criteria {
            metric_namespace = "Microsoft.ServiceBus/namespaces"
            metric_name      = "DeadletteredMessages"
            aggregation      = "Maximum"
            operator         = "GreaterThan"
            threshold        = 0

            dimension {
              name     = "EntityName"
              operator = "Include"
              values   = [each.key]
            }
          }

          action {
            action_group_id = azurerm_monitor_action_group.alarms.id
          }
        }

        # ------------------------- Service alarm -----------------------------
        # Serviço sem nenhuma réplica: processo caindo, imagem que não puxa ou segredo ausente.
        resource "azurerm_monitor_metric_alert" "no_replicas" {
          count = var.watch_service ? 1 : 0

          name                = "${var.name_prefix}-no-replicas"
          resource_group_name = var.resource_group_name
          scopes              = [var.container_app_id]
          description         = "Container App sem réplica rodando"
          frequency           = "PT5M"
          window_size         = "PT15M"
          tags                = var.tags

          criteria {
            metric_namespace = "Microsoft.App/containerApps"
            metric_name      = "Replicas"
            aggregation      = "Maximum"
            operator         = "LessThan"
            threshold        = 1
          }

          action {
            action_group_id = azurerm_monitor_action_group.alarms.id
          }
        }

        # ------------------------- Database alarm ----------------------------
        resource "azurerm_monitor_metric_alert" "database_cpu" {
          count = var.watch_database ? 1 : 0

          name                = "${var.name_prefix}-db-cpu"
          resource_group_name = var.resource_group_name
          scopes              = [var.postgres_server_id]
          description         = "CPU do banco acima de 80% por 15 minutos"
          frequency           = "PT5M"
          window_size         = "PT15M"
          tags                = var.tags

          criteria {
            metric_namespace = "Microsoft.DBforPostgreSQL/flexibleServers"
            metric_name      = "cpu_percent"
            aggregation      = "Average"
            operator         = "GreaterThan"
            threshold        = 80
          }

          action {
            action_group_id = azurerm_monitor_action_group.alarms.id
          }
        }

        """;

    private const string ServiceBusResource = """
        # ------------------------- Namespace ---------------------------------
        # Sem chave de acesso local: só a identidade gerenciada da aplicação entra.
        resource "azurerm_servicebus_namespace" "this" {
          name                = "${var.name_prefix}-bus"
          location            = var.location
          resource_group_name = var.resource_group_name
          sku                 = "Standard"
          local_auth_enabled  = false
          minimum_tls_version = "1.2"
          tags                = var.tags
        }

        # ------------------------- Queues ------------------------------------
        # Cada fila tem a própria fila de mensagens mortas: depois de 5 entregas a mensagem vai para lá.
        resource "azurerm_servicebus_queue" "main" {
          for_each = toset(var.flows)

          name                                 = each.key
          namespace_id                         = azurerm_servicebus_namespace.this.id
          max_delivery_count                   = 5
          lock_duration                        = "PT3M"
          default_message_ttl                  = "P4D"
          dead_lettering_on_message_expiration = true
        }

        """;

    private const string AcrResource = """
        # ------------------------- Registry ----------------------------------
        # Sem usuário administrador: o Container App puxa a imagem com a identidade gerenciada.
        resource "azurerm_container_registry" "this" {
          name                = "${replace(var.name_prefix, "-", "")}acr"
          resource_group_name = var.resource_group_name
          location            = var.location
          sku                 = "Basic"
          admin_enabled       = false
          tags                = var.tags
        }

        """;

    private const string KeyVaultResource = """
        data "azurerm_client_config" "current" {}

        # ------------------------- Key Vault ---------------------------------
        # Acesso por RBAC; os segredos são gravados fora do Terraform e nunca entram no state.
        resource "azurerm_key_vault" "this" {
          name                       = "kv-${substr(replace(var.name_prefix, "-", ""), 0, 21)}"
          location                   = var.location
          resource_group_name        = var.resource_group_name
          tenant_id                  = data.azurerm_client_config.current.tenant_id
          sku_name                   = "standard"
          rbac_authorization_enabled = true
          purge_protection_enabled   = true
          soft_delete_retention_days = 90
          tags                       = var.tags
        }

        """;

    private const string ContainerAppResource = """
        # ------------------------- Logs --------------------------------------
        resource "azurerm_log_analytics_workspace" "this" {
          name                = "${var.name_prefix}-logs"
          location            = var.location
          resource_group_name = var.resource_group_name
          sku                 = "PerGB2018"
          retention_in_days   = 30
          tags                = var.tags
        }

        # ------------------------- Environment -------------------------------
        resource "azurerm_container_app_environment" "this" {
          name                       = "${var.name_prefix}-env"
          location                   = var.location
          resource_group_name        = var.resource_group_name
          log_analytics_workspace_id = azurerm_log_analytics_workspace.this.id
          tags                       = var.tags
        }

        # ------------------------- Identity ----------------------------------
        # A aplicação usa só esta identidade: puxa a imagem, lê o Key Vault e usa as filas, sem segredo no contêiner.
        resource "azurerm_user_assigned_identity" "app" {
          name                = "${var.name_prefix}-${var.service_name}"
          location            = var.location
          resource_group_name = var.resource_group_name
          tags                = var.tags
        }

        resource "azurerm_role_assignment" "acr_pull" {
          scope                = var.registry_id
          role_definition_name = "AcrPull"
          principal_id         = azurerm_user_assigned_identity.app.principal_id
        }

        resource "azurerm_role_assignment" "key_vault" {
          scope                = var.key_vault_id
          role_definition_name = "Key Vault Secrets User"
          principal_id         = azurerm_user_assigned_identity.app.principal_id
        }

        resource "azurerm_role_assignment" "queue_sender" {
          count = var.grant_queue_access ? 1 : 0

          scope                = var.servicebus_namespace_id
          role_definition_name = "Azure Service Bus Data Sender"
          principal_id         = azurerm_user_assigned_identity.app.principal_id
        }

        resource "azurerm_role_assignment" "queue_receiver" {
          count = var.grant_queue_access ? 1 : 0

          scope                = var.servicebus_namespace_id
          role_definition_name = "Azure Service Bus Data Receiver"
          principal_id         = azurerm_user_assigned_identity.app.principal_id
        }

        # ------------------------- Container App -----------------------------
        resource "azurerm_container_app" "this" {
          name                         = var.service_name
          container_app_environment_id = azurerm_container_app_environment.this.id
          resource_group_name          = var.resource_group_name
          revision_mode                = "Single"
          tags                         = var.tags

          identity {
            type         = "UserAssigned"
            identity_ids = [azurerm_user_assigned_identity.app.id]
          }

          registry {
            server   = var.registry_server
            identity = azurerm_user_assigned_identity.app.id
          }

          # Só HTTPS, no domínio gerenciado do ambiente.
          ingress {
            external_enabled           = true
            target_port                = var.port
            allow_insecure_connections = false

            traffic_weight {
              latest_revision = true
              percentage      = 100
            }
          }

          template {
            min_replicas = var.min_replicas
            max_replicas = var.max_replicas

            container {
              name   = var.service_name
              image  = "${var.registry_server}/${var.service_name}:${var.image_tag}"
              cpu    = var.cpu
              memory = var.memory

              dynamic "env" {
                for_each = var.environment

                content {
                  name  = env.key
                  value = env.value
                }
              }

              liveness_probe {
                transport = "HTTP"
                port      = var.port
                path      = var.health_path
              }
            }
          }

          # Depois do primeiro apply o CI é o dono da imagem.
          lifecycle {
            ignore_changes = [template[0].container[0].image]
          }

          depends_on = [azurerm_role_assignment.acr_pull]
        }

        """;

    private const string PostgresResource = """
        data "azurerm_client_config" "current" {}

        # ------------------------- Server ------------------------------------
        # Privado na subnet delegada e só com login do Entra ID: não existe senha para vazar.
        resource "azurerm_postgresql_flexible_server" "this" {
          name                          = "${var.name_prefix}-db"
          resource_group_name           = var.resource_group_name
          location                      = var.location
          version                       = var.engine_version
          sku_name                      = var.sku_name
          storage_mb                    = var.storage_gb * 1024
          delegated_subnet_id           = var.subnet_id
          private_dns_zone_id           = var.private_dns_zone_id
          public_network_access_enabled = false
          backup_retention_days         = 7
          tags                          = var.tags

          authentication {
            active_directory_auth_enabled = true
            password_auth_enabled         = false
            tenant_id                     = data.azurerm_client_config.current.tenant_id
          }

          dynamic "high_availability" {
            for_each = var.zone_redundant ? [1] : []

            content {
              mode = "ZoneRedundant"
            }
          }

          # Apagar o banco pede tirar esta proteção antes.
          lifecycle {
            prevent_destroy = true
            ignore_changes  = [zone]
          }
        }

        resource "azurerm_postgresql_flexible_server_database" "app" {
          name      = var.database_name
          server_id = azurerm_postgresql_flexible_server.this.id
          charset   = "UTF8"
          collation = "en_US.utf8"
        }

        # ------------------------- Administrator -----------------------------
        resource "azurerm_postgresql_flexible_server_active_directory_administrator" "this" {
          count = var.admin_object_id == null ? 0 : 1

          server_name         = azurerm_postgresql_flexible_server.this.name
          resource_group_name = var.resource_group_name
          tenant_id           = data.azurerm_client_config.current.tenant_id
          object_id           = var.admin_object_id
          principal_name      = var.admin_name
          principal_type      = var.admin_type
        }

        """;
}
