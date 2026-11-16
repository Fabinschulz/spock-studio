using static Mariliz.Application.Iac.IacHcl;

namespace Mariliz.Application.Iac;

// Gera a pasta de infraestrutura no formato do modelo (TERRAFORM_PROJECT_LAYOUT.md): raiz com providers.tf,
// variables.tf, resource.tf, output.tf, terraform.tfvars.example e README.md, e um módulo por serviço da AWS. Tudo
// sai de modelos fixos a partir do desenho conferido; o texto já sai no formato do `fmt`.
public static class IacAwsGenerator
{
    public sealed record File(string Path, string Content);

    public static IReadOnlyList<File> Generate(IacBlueprint b, string root)
    {
        var service = b.Service;
        var database = b.Database;
        var queues = b.Queues.Count > 0;
        string P(string relative) => root.Length == 0 ? relative : $"{root}/{relative}";

        var files = new List<File>
        {
            new(P("providers.tf"), Providers(b)),
            new(P("variables.tf"), RootVariables(b)),
            new(P("resource.tf"), RootResources(b)),
            new(P("output.tf"), RootOutputs(b)),
            new(P("terraform.tfvars.example"), TfvarsExample(b)),
            new(P("README.md"), Readme(b, root)),
            new(P("modules/sns/resource.tf"), SnsResource()),
            new(P("modules/sns/variables.tf"), ModuleVariables(v => v.Nullable("alarm_email", "string", "E-mail que recebe os alarmes; sem ele, o tópico existe e não há assinatura"))),
            new(P("modules/sns/outputs.tf"), Outputs(("topic_arn", "ARN do tópico de alarmes, usado pelos alarmes do CloudWatch", "aws_sns_topic.alarms.arn"))),
            new(P("modules/cloudwatch/resource.tf"), CloudWatchResource(b)),
            new(P("modules/cloudwatch/variables.tf"), CloudWatchVariables(b))
        };

        if (queues)
        {
            files.Add(new(P("modules/sqs/resource.tf"), SqsResource()));
            files.Add(new(P("modules/sqs/variables.tf"), ModuleVariables(v => v.Required("flows", "list(string)",
                "Fluxos de mensagens; cada um vira uma fila standard e uma fila de mensagens mortas. Os nomes são endereços de recurso: renomear destrói a fila"))));
            files.Add(new(P("modules/sqs/outputs.tf"), SqsOutputs()));
        }

        if (service is not null)
        {
            files.Add(new(P("modules/ecr/resource.tf"), EcrResource()));
            files.Add(new(P("modules/ecr/variables.tf"), ModuleVariables(v => v.Required("hosts", "list(string)", "Hosts que publicam imagem; um repositório por host"))));
            files.Add(new(P("modules/ecr/outputs.tf"), Outputs(
                ("repository_urls", "URL de cada repositório, por host, onde o CI publica a imagem com a tag do commit",
                    "{ for host, repository in aws_ecr_repository.this : host => repository.repository_url }"))));
            files.Add(new(P("modules/secrets/resource.tf"), SecretsResource()));
            files.Add(new(P("modules/secrets/variables.tf"), ModuleVariables(_ => { })));
            files.Add(new(P("modules/secrets/outputs.tf"), Outputs(("secret_arn", "ARN do segredo da aplicação, criado sem valor", "aws_secretsmanager_secret.app.arn"))));
            files.Add(new(P("modules/iam/resource.tf"), IamResource()));
            files.Add(new(P("modules/iam/variables.tf"), ModuleVariables(v => v
                .Required("secret_arns", "list(string)", "Segredos que a execution role lê para montar as variáveis da tarefa")
                .Required("queue_arns", "list(string)", "Filas que a tarefa produz e consome")
                .Required("grant_queue_access", "bool", "Se a task role recebe a política das filas"))));
            files.Add(new(P("modules/iam/outputs.tf"), Outputs(
                ("execution_role_arn", "Papel que o ECS usa para baixar a imagem, escrever logs e ler os segredos", "aws_iam_role.execution.arn"),
                ("task_role_arn", "Papel que o código da aplicação usa na AWS", "aws_iam_role.task.arn"))));
            files.Add(new(P("modules/alb/resource.tf"), AlbResource()));
            files.Add(new(P("modules/alb/variables.tf"), ModuleVariables(v => v
                .Required("vpc_id", "string", "VPC existente do load balancer")
                .Required("public_subnet_ids", "list(string)", "Subnets públicas, em pelo menos duas zonas")
                .Required("certificate_arn", "string", "Certificado ACM do listener HTTPS")
                .Required("port", "number", "Porta do contêiner para onde o load balancer encaminha")
                .Required("health_path", "string", "Caminho do health check"))));
            files.Add(new(P("modules/alb/outputs.tf"), Outputs(
                ("security_group_id", "Security group do load balancer; as tarefas só aceitam entrada dele", "aws_security_group.alb.id"),
                ("target_group_arn", "Target group que o serviço registra", "aws_lb_target_group.service.arn"),
                ("dns_name", "Nome DNS do load balancer, para apontar o hostname público", "aws_lb.this.dns_name"))));
            files.Add(new(P("modules/ecs/resource.tf"), EcsResource()));
            files.Add(new(P("modules/ecs/variables.tf"), EcsVariables()));
            files.Add(new(P("modules/ecs/outputs.tf"), Outputs(
                ("cluster_name", "Nome do cluster ECS", "aws_ecs_cluster.this.name"),
                ("service_name", "Nome do serviço ECS", "aws_ecs_service.service.name"),
                ("tasks_security_group_id", "Security group das tarefas; o banco aceita entrada dele", "aws_security_group.tasks.id"))));
        }

        if (database is not null)
        {
            files.Add(new(P("modules/rds/resource.tf"), RdsResource()));
            files.Add(new(P("modules/rds/variables.tf"), RdsVariables()));
            files.Add(new(P("modules/rds/outputs.tf"), Outputs(
                ("address", "Endereço do banco", "aws_db_instance.this.address"),
                ("port", "Porta do banco", "aws_db_instance.this.port"),
                ("database_name", "Nome do banco criado", "aws_db_instance.this.db_name"),
                ("master_secret_arn", "Segredo que o RDS criou com a senha do usuário principal", "aws_db_instance.this.master_user_secret[0].secret_arn"))));
        }
        return files;
    }

    // Só o que é cobrado, sem valores: o preço muda por região e uso, e o Spock não tem fonte de preços nesta entrega.
    public static IReadOnlyList<IacCostItem> Costs(IacBlueprint b)
    {
        var items = new List<IacCostItem>();
        if (b.Service is { } s)
        {
            items.Add(new("Application Load Balancer", "Por hora e por unidade de capacidade (LCU) usada."));
            items.Add(new($"ECS Fargate ({s.DesiredCount} tarefa(s), {s.Cpu} CPU, {s.Memory} MB)", "Por vCPU-hora e GB-hora de cada tarefa em execução."));
            items.Add(new("ECR", "Armazenamento das imagens por GB-mês; o ciclo de vida guarda as 20 mais recentes."));
            items.Add(new("Secrets Manager", "Por segredo por mês e por chamada de API."));
            items.Add(new("CloudWatch Logs e Container Insights", "Logs por GB ingerido e armazenado (30 dias); métricas do Container Insights."));
            if (!s.PublicIp) items.Add(new("NAT Gateway ou endpoints VPC (fora desta configuração)", "Tarefas sem IP público precisam deles para baixar a imagem; cobrados por hora e por GB."));
        }
        if (b.Database is { } d)
            items.Add(new($"RDS {d.Engine} {d.InstanceClass}{(d.MultiAz ? " Multi-AZ" : "")}", $"Instância por hora, {d.StorageGb} GB de armazenamento gp3 por mês e backup além do tamanho do banco."));
        if (b.Queues.Count > 0) items.Add(new($"SQS ({b.Queues.Count} fila(s) e suas filas de mensagens mortas)", "Por requisição."));
        items.Add(new("CloudWatch Alarms e SNS", "Por alarme por mês e por notificação enviada."));
        items.Add(new("Transferência de dados", "Saída para a internet por GB."));
        return items;
    }

    // Filas e alarmes não ficam em rede; serviço e banco precisam da VPC existente.
    public static bool NeedsNetwork(IacBlueprint b) => b.Service is not null || b.Database is not null;

    private static string Providers(IacBlueprint b)
    {
        var h = new IacHcl();
        h.Block("terraform", t =>
        {
            t.Attr("required_version", Quote(">= 1.11"));
            t.Blank();
            t.Block("required_providers", r => r.Object("aws", o => o.Attr("source", Quote("hashicorp/aws")).Attr("version", Quote(IacBootstrapTemplates.AwsVersion))));
            t.Blank();
            if (b.Backend is { } backend)
            {
                t.Comment($"O state de cada workspace fica em {b.Project}/<workspace>/terraform.tfstate.");
                t.Block("backend \"s3\"", s =>
                {
                    s.Attr("bucket", Quote(backend.Bucket)).Attr("key", Quote("terraform.tfstate")).Attr("workspace_key_prefix", Quote(b.Project))
                        .Attr("region", Quote(backend.Region));
                    if (b.Profile is { } profile) s.Attr("profile", Quote(profile));
                    s.Attr("encrypt", "true").Attr("use_lockfile", "true");
                });
            }
            else
            {
                t.Comment("Sem backend remoto ainda: crie o bucket com o bootstrap (tela Infraestrutura, State) e cole aqui o bloco backend \"s3\".");
            }
        });
        h.Blank();
        h.Block("provider \"aws\"", p =>
        {
            p.Attr("region", "var.aws_region").Attr("profile", "var.aws_profile");
            if (b.Account is { } account) p.Attr("allowed_account_ids", QuotedList([account]));
        });
        return h.ToString();
    }

    private static string RootVariables(IacBlueprint b)
    {
        var v = new Variables();
        v.Default("aws_region", "string", "Região da AWS onde os recursos são criados", Quote(b.Region), "AWS Region");
        v.Default("aws_profile", "string", "Profile da AWS CLI (SSO) usado para autenticar; null usa a cadeia padrão de credenciais",
            b.Profile is null ? "null" : Quote(b.Profile), "AWS Profile");
        if (NeedsNetwork(b))
        {
            v.Validated("vpc_id", "string", "ID da VPC existente (o Terraform não cria rede)", "can(regex(\"^vpc-[0-9a-f]+$\", var.vpc_id))",
                "O ID da VPC deve estar no formato vpc-0123456789abcdef0.", "Rede existente");
            v.Validated("private_subnet_ids", "list(string)", "Subnets privadas, para tarefas sem IP público e para o banco",
                "length(var.private_subnet_ids) >= 1 && alltrue([for id in var.private_subnet_ids : can(regex(\"^subnet-[0-9a-f]+$\", id))])",
                "Informe ao menos uma subnet, cada uma no formato subnet-0123456789abcdef0.");
        }
        if (b.Service is not null)
        {
            v.Validated("public_subnet_ids", "list(string)", "Subnets públicas, em pelo menos duas zonas de disponibilidade, onde fica o load balancer",
                "length(var.public_subnet_ids) >= 2 && alltrue([for id in var.public_subnet_ids : can(regex(\"^subnet-[0-9a-f]+$\", id))])",
                "Informe ao menos duas subnets públicas, cada uma no formato subnet-0123456789abcdef0.");
            v.Validated("certificate_arn", "string", $"ARN do certificado ACM do listener HTTPS, emitido em {b.Region}",
                $"can(regex(\"^arn:aws:acm:{b.Region}:[0-9]{{12}}:certificate/\", var.certificate_arn))",
                $"O certificado deve ser um ARN do ACM na região {b.Region}.", "Certificado");
            v.Validated("image_tag", "string", "Tag da imagem da task definition inicial; só vale para o primeiro apply, porque depois o CI é o dono da revisão",
                "can(regex(\"^[A-Za-z0-9_.-]{1,128}$\", var.image_tag)) && var.image_tag != \"latest\"",
                "Use uma tag imutável (o SHA do commit), nunca latest.", "Imagem");
        }
        v.Default("alarm_email", "string", "E-mail que recebe os alarmes; null cria o tópico sem assinatura",
            b.AlarmEmail is null ? "null" : Quote(b.AlarmEmail), "Alarmes");
        return v.ToString();
    }

    private static string RootResources(IacBlueprint b)
    {
        var h = new IacHcl();
        var service = b.Service;
        var tasksSubnets = service is { PublicIp: true } ? "var.public_subnet_ids" : "var.private_subnet_ids";
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

        Module("sns", m => m.Attr("name_prefix", "local.name_prefix").Attr("alarm_email", "var.alarm_email").Attr("tags", "local.tags"));
        if (b.Queues.Count > 0)
            Module("sqs", m => m.Attr("name_prefix", "local.name_prefix").Attr("flows", QuotedList(b.Queues)).Attr("tags", "local.tags"));
        if (service is not null)
        {
            Module("ecr", m => m.Attr("name_prefix", "local.name_prefix").Attr("hosts", QuotedList([service.Name])).Attr("tags", "local.tags"));
            Module("secrets", m => m.Attr("name_prefix", "local.name_prefix").Attr("tags", "local.tags"));
            var secrets = b.Database is null ? "[module.secrets.secret_arn]" : "[module.secrets.secret_arn, module.rds.master_secret_arn]";
            var queueArns = b.Queues.Count > 0 ? "[for queue in module.sqs.queues : queue.arn]" : "[]";
            h.Blank();
            h.Section("IAM");
            h.Comment("Papéis das tarefas e políticas das filas e dos segredos, sem curinga de serviço.");
            h.Block("module \"iam\"", m =>
            {
                m.Attr("source", Quote("./modules/iam"));
                m.Blank();
                m.Attr("name_prefix", "local.name_prefix").Attr("secret_arns", secrets).Attr("queue_arns", queueArns)
                    .Attr("grant_queue_access", b.Queues.Count > 0 ? "true" : "false").Attr("tags", "local.tags");
            });
            Module("alb", m => m.Attr("name_prefix", "local.name_prefix").Attr("vpc_id", "var.vpc_id").Attr("public_subnet_ids", "var.public_subnet_ids")
                .Attr("certificate_arn", "var.certificate_arn").Attr("port", service.Port.ToString()).Attr("health_path", Quote(service.HealthPath))
                .Attr("tags", "local.tags"));
            h.Blank();
            h.Section("ECS");
            h.Block("module \"ecs\"", m =>
            {
                m.Attr("source", Quote("./modules/ecs"));
                m.Blank();
                m.Attr("name_prefix", "local.name_prefix").Attr("aws_region", "var.aws_region").Attr("vpc_id", "var.vpc_id")
                    .Attr("subnet_ids", tasksSubnets).Attr("assign_public_ip", service.PublicIp ? "true" : "false")
                    .Attr("alb_security_group_id", "module.alb.security_group_id").Attr("target_group_arn", "module.alb.target_group_arn")
                    .Attr("service_name", Quote(service.Name)).Attr("repository_url", $"module.ecr.repository_urls[{Quote(service.Name)}]")
                    .Attr("image_tag", "var.image_tag").Attr("port", service.Port.ToString()).Attr("cpu", service.Cpu.ToString())
                    .Attr("memory", service.Memory.ToString()).Attr("desired_count", service.DesiredCount.ToString())
                    .Attr("execution_role_arn", "module.iam.execution_role_arn").Attr("task_role_arn", "module.iam.task_role_arn");
                m.Blank();
                m.Object("environment", e =>
                {
                    e.Attr("PORT", Quote(service.Port.ToString()));
                    if (b.Database is not null) e.Attr("DB_HOST", "module.rds.address").Attr("DB_PORT", "tostring(module.rds.port)").Attr("DB_NAME", "module.rds.database_name");
                    foreach (var queue in b.Queues) e.Attr($"QUEUE_{queue.ToUpperInvariant().Replace('-', '_')}_URL", $"module.sqs.queues[{Quote(queue)}].url");
                });
                m.Blank();
                m.Comment("Valor inteiro do segredo da aplicação e a senha que o RDS guardou; nada disso passa pelo Terraform.");
                m.Object("secrets", s =>
                {
                    s.Attr("APP_SECRETS", "module.secrets.secret_arn");
                    if (b.Database is not null) s.Attr("DB_PASSWORD", "\"${module.rds.master_secret_arn}:password::\"");
                });
                m.Blank();
                m.Attr("tags", "local.tags");
                m.Blank();
                m.Comment("O listener precisa existir antes de o serviço registrar as tarefas no target group.");
                m.Attr("depends_on", "[module.alb]");
            });
        }
        if (b.Database is { } database)
        {
            Module("rds", m =>
            {
                m.Attr("name_prefix", "local.name_prefix").Attr("vpc_id", "var.vpc_id").Attr("subnet_ids", "var.private_subnet_ids")
                    .Attr("engine", Quote(database.Engine)).Attr("engine_version", Quote(database.EngineVersion))
                    .Attr("instance_class", Quote(database.InstanceClass)).Attr("storage_gb", database.StorageGb.ToString())
                    .Attr("multi_az", database.MultiAz ? "true" : "false").Attr("database_name", Quote(b.Project.Replace('-', '_')));
                m.Blank();
                if (service is not null) m.Object("allowed_security_groups", a => a.Attr("tasks", "module.ecs.tasks_security_group_id"));
                else m.Attr("allowed_security_groups", "{}");
                m.Blank();
                m.Attr("tags", "local.tags");
            });
        }
        Module("cloudwatch", m =>
        {
            m.Attr("name_prefix", "local.name_prefix").Attr("topic_arn", "module.sns.topic_arn");
            if (b.Queues.Count > 0) m.Attr("flows", QuotedList(b.Queues));
            if (service is not null) m.Attr("cluster_name", "module.ecs.cluster_name").Attr("service_name", "module.ecs.service_name");
            if (b.Database is not null) m.Attr("db_instance_id", Quote("${local.name_prefix}-db"));
            m.Attr("tags", "local.tags");
        });
        return h.ToString();
    }

    private static string RootOutputs(IacBlueprint b)
    {
        var outputs = new List<(string, string, string)> { ("alarm_topic_arn", "ARN do tópico SNS que recebe os alarmes", "module.sns.topic_arn") };
        if (b.Queues.Count > 0) outputs.Add(("queues", "Filas por fluxo: URL e ARN da fila principal e da fila de mensagens mortas", "module.sqs.queues"));
        if (b.Service is not null)
        {
            outputs.Add(("ecr_repository_urls", "Repositórios ECR por host, onde o CI publica a imagem com a tag do commit", "module.ecr.repository_urls"));
            outputs.Add(("alb_dns_name", "Nome DNS do load balancer, para apontar o hostname do certificado", "module.alb.dns_name"));
            outputs.Add(("ecs_cluster_name", "Cluster ECS do ambiente", "module.ecs.cluster_name"));
        }
        if (b.Database is not null) outputs.Add(("database_address", "Endereço do banco (privado)", "module.rds.address"));
        return Outputs([.. outputs]);
    }

    private static string TfvarsExample(IacBlueprint b)
    {
        var h = new IacHcl();
        h.Comment("Copie para terraform.tfvars (no .gitignore) e troque cada valor pelo real da conta.");
        if (!NeedsNetwork(b))
        {
            h.Comment("Este desenho não tem variável obrigatória: região, perfil e e-mail de alarme já têm valor padrão.");
            return h.ToString();
        }
        h.Blank();
        h.Section("Rede existente");
        h.Comment("VPC e subnets já existem; o Terraform não as cria. O Spock pode ler as da conta pela conexão cloud.");
        h.Attr("vpc_id", Quote(b.Network.VpcId.Length > 0 ? b.Network.VpcId : "vpc-0123456789abcdef0"));
        h.Attr("private_subnet_ids", QuotedList(b.Network.PrivateSubnets.Count > 0 ? b.Network.PrivateSubnets : ["subnet-0aaaaaaaaaaaaaaa1"]));
        if (b.Service is { } service)
        {
            h.Attr("public_subnet_ids", QuotedList(b.Network.PublicSubnets.Count > 0 ? b.Network.PublicSubnets : ["subnet-0ccccccccccccccc3", "subnet-0ddddddddddddddd4"]));
            h.Blank();
            h.Section("Certificado");
            h.Comment($"Certificado ACM em {b.Region} do hostname público.");
            h.Attr("certificate_arn", Quote(service.CertificateArn));
            h.Blank();
            h.Section("Imagem");
            h.Comment("SHA do commit da primeira imagem publicada no ECR; só vale para o primeiro apply.");
            h.Attr("image_tag", Quote("0000000000000000000000000000000000000000"));
        }
        return h.ToString();
    }

    private static string Readme(IacBlueprint b, string root)
    {
        var dir = root.Length == 0 ? "." : root;
        var rows = new List<(string Path, string What)>
        {
            ("`providers.tf`, `variables.tf`, `resource.tf`, `output.tf`", "Configuração raiz. O **workspace selecionado é o ambiente**" + (b.Backend is null ? "; sem backend remoto ainda (crie com o bootstrap)" : "; state remoto no S3 com lock nativo")),
            ("`terraform.tfvars.example`", "Formato dos valores que não ficam no repositório; copie para `terraform.tfvars`, que está no .gitignore"),
            ("`modules/sns/`", "Tópico de alarmes e a assinatura por e-mail"),
            ("`modules/cloudwatch/`", "Alarmes: fila de mensagens mortas com mensagem, serviço sem tarefa rodando, CPU do banco")
        };
        if (b.Queues.Count > 0) rows.Add(("`modules/sqs/`", $"Filas standard ({string.Join(", ", b.Queues.Select(q => $"`{q}`"))}) e uma fila de mensagens mortas para cada"));
        if (b.Service is { } s)
        {
            rows.Add(("`modules/ecr/`", $"Repositório de imagens do host `{s.Name}`: tags imutáveis, scan no push, guarda as 20 imagens mais recentes"));
            rows.Add(("`modules/secrets/`", "O segredo da aplicação no Secrets Manager, criado **sem valor**"));
            rows.Add(("`modules/iam/`", "Execution role, task role e as políticas de filas e segredos"));
            rows.Add(("`modules/alb/`", $"Load balancer público só HTTPS (443), target group na {s.Port} com health check `GET {s.HealthPath}`"));
            rows.Add(("`modules/ecs/`", "Cluster com Container Insights, log group (30 dias), security group, task definition e serviço Fargate"));
        }
        if (b.Database is { } d) rows.Add(("`modules/rds/`", $"Banco {d.Engine} {d.EngineVersion} privado, senha gerenciada pelo RDS no Secrets Manager, proteção contra exclusão"));

        var text = new System.Text.StringBuilder();
        text.Append($"# Infraestrutura (OpenTofu/Terraform)\n\nRecursos AWS de `{b.Project}`, gerados pelo Spock a partir do desenho aprovado, no formato do modelo de projeto.\n\n");
        text.Append("| Caminho | O que é |\n| ------- | ------- |\n");
        foreach (var (path, what) in rows) text.Append($"| {path} | {what} |\n");
        text.Append($"\nRegião `{b.Region}`" + (b.Account is null ? ".\n" : $", conta `{b.Account}`: o provider recusa qualquer outra conta (`allowed_account_ids`).\n"));
        text.Append("\n## Pré-requisitos\n\n- OpenTofu **≥ 1.11** (ou Terraform ≥ 1.11): lock nativo do state no S3. O Spock instala e verifica a ferramenta.\n");
        text.Append($"- Provider AWS fixado em `{IacBootstrapTemplates.AwsVersion}`.\n- Perfil SSO da AWS CLI" + (b.Profile is null ? "" : $" `{b.Profile}`") + ".\n");
        text.Append($"\n## Plan e apply de um ambiente\n\nPela tela Infraestrutura do Spock (validar, gerar plano, aprovar) ou no terminal:\n\n");
        text.Append($"```bash\ntofu -chdir={dir} init\n```\n\n```bash\ntofu -chdir={dir} workspace select dev\n```\n\n```bash\ntofu -chdir={dir} plan -out=dev.tfplan\n```\n\n```bash\ntofu -chdir={dir} apply dev.tfplan\n```\n\n");
        text.Append("- Ambiente novo: `tofu workspace new <env>` (até 7 caracteres, por causa do limite de 32 do nome do load balancer).\n");
        text.Append("- No workspace `default` o plan falha de propósito.\n- Depois de um apply, `plan -detailed-exitcode` deve informar `No changes`.\n");
        if (b.Service is not null)
            text.Append("\n## Primeiro deploy\n\n1. Aplique só ECR, segredo e IAM (`-target=module.ecr -target=module.secrets -target=module.iam`).\n" +
                        "2. Grave o valor do segredo da aplicação fora do Terraform (console ou CLI), a partir de um arquivo fora do repositório.\n" +
                        "3. Publique a primeira imagem no ECR com a tag do commit e use essa tag em `image_tag`.\n" +
                        "4. Aplique o restante. Depois disso o CI é dono da revisão da task definition (`ignore_changes`).\n");
        text.Append("\n## Verificações\n\n```bash\ntofu fmt -check -recursive " + dir + "\n```\n\n");
        text.Append("Rode `tofu validate` em cada pasta com `.tf` depois de `tofu init -backend=false`. Uma mudança é provada pelo plan revisado, por um segundo plan sem mudanças depois do apply e pela leitura dos recursos na AWS.\n");
        text.Append("\n## Operando\n\n- **Alarme de fila de mensagens mortas.** Uma mensagem falhou 5 vezes e fica 14 dias em `<fila>-dlq`: encontre a causa e reenvie pelo console do SQS.\n");
        if (b.Service is not null) text.Append("- **Serviço sem tarefa rodando.** Veja o motivo da última tarefa parada no console do ECS e os logs em `/ecs/<projeto>-<env>/<serviço>`.\n");
        text.Append("- **Renomear uma fila ou host.** Os nomes são endereços de recurso: trocar destrói o recurso. Renomeie só com bloco `moved`.\n");
        return text.ToString();
    }

    private static string SnsResource()
    {
        var h = new IacHcl();
        h.Section("Alarm topic");
        h.Block("resource \"aws_sns_topic\" \"alarms\"", r => r.Attr("name", Quote("${var.name_prefix}-alarms")).Attr("tags", "var.tags"));
        h.Blank();
        h.Comment("Sem e-mail, o tópico existe e não há assinatura. A AWS pede confirmação no primeiro envio.");
        h.Block("resource \"aws_sns_topic_subscription\" \"email\"", r =>
        {
            r.Attr("count", "var.alarm_email == null ? 0 : 1");
            r.Blank();
            r.Attr("topic_arn", "aws_sns_topic.alarms.arn").Attr("protocol", Quote("email")).Attr("endpoint", "var.alarm_email");
        });
        return h.ToString();
    }

    private static string SqsResource()
    {
        var h = new IacHcl();
        h.Section("Flows");
        h.Comment("Os fluxos são endereços de recurso: renomear um destrói a fila e as mensagens dela. Para renomear, use um bloco moved.");
        h.Block("locals", l => l.Attr("flows", "toset(var.flows)"));
        h.Blank();
        h.Section("Dead-letter queues");
        h.Block("resource \"aws_sqs_queue\" \"dlq\"", r =>
        {
            r.Attr("for_each", "local.flows");
            r.Blank();
            r.Attr("name", Quote("${var.name_prefix}-${each.key}-dlq")).Attr("message_retention_seconds", "1209600").Attr("sqs_managed_sse_enabled", "true").Attr("tags", "var.tags");
        });
        h.Blank();
        h.Section("Main queues");
        h.Block("resource \"aws_sqs_queue\" \"main\"", r =>
        {
            r.Attr("for_each", "local.flows");
            r.Blank();
            r.Attr("name", Quote("${var.name_prefix}-${each.key}")).Attr("visibility_timeout_seconds", "180").Attr("message_retention_seconds", "345600")
                .Attr("receive_wait_time_seconds", "20").Attr("sqs_managed_sse_enabled", "true").Attr("tags", "var.tags");
            r.Blank();
            r.Object("redrive_policy", p => p.Attr("deadLetterTargetArn", "aws_sqs_queue.dlq[each.key].arn").Attr("maxReceiveCount", "5"), "jsonencode({", "})");
        });
        h.Blank();
        h.Comment("Recurso separado: a política dentro da fila de mensagens mortas criaria um ciclo entre as duas filas.");
        h.Block("resource \"aws_sqs_queue_redrive_allow_policy\" \"dlq\"", r =>
        {
            r.Attr("for_each", "local.flows");
            r.Blank();
            r.Attr("queue_url", "aws_sqs_queue.dlq[each.key].id");
            r.Blank();
            r.Object("redrive_allow_policy", p => p.Attr("redrivePermission", Quote("byQueue")).Attr("sourceQueueArns", "[aws_sqs_queue.main[each.key].arn]"), "jsonencode({", "})");
        });
        return h.ToString();
    }

    private static string SqsOutputs()
    {
        var h = new IacHcl();
        h.Block("output \"queues\"", o =>
        {
            o.Attr("description", Quote("Filas por fluxo: URL e ARN da fila principal e da fila de mensagens mortas"));
            o.Object("value", v => v.Block("for key, queue in aws_sqs_queue.main : key =>", q =>
                q.Attr("url", "queue.url").Attr("arn", "queue.arn").Attr("dlq_arn", "aws_sqs_queue.dlq[key].arn").Attr("dlq_name", "aws_sqs_queue.dlq[key].name")));
        });
        return h.ToString();
    }

    private static string EcrResource()
    {
        var h = new IacHcl();
        h.Section("Repositories");
        h.Block("resource \"aws_ecr_repository\" \"this\"", r =>
        {
            r.Attr("for_each", "toset(var.hosts)");
            r.Blank();
            r.Attr("name", Quote("${var.name_prefix}-${each.key}"));
            r.Comment("Tag imutável: um deploy aponta sempre para o mesmo conteúdo e o rollback nomeia uma tag que não mudou.");
            r.Attr("image_tag_mutability", Quote("IMMUTABLE")).Attr("tags", "var.tags");
            r.Blank();
            r.Block("image_scanning_configuration", s => s.Attr("scan_on_push", "true"));
            r.Blank();
            r.Block("encryption_configuration", e => e.Attr("encryption_type", Quote("AES256")));
        });
        h.Blank();
        h.Section("Lifecycle");
        h.Block("resource \"aws_ecr_lifecycle_policy\" \"this\"", r =>
        {
            r.Attr("for_each", "aws_ecr_repository.this");
            r.Blank();
            r.Attr("repository", "each.value.name");
            r.Blank();
            r.Object("policy", p => p.Object("rules", rule =>
            {
                rule.Attr("rulePriority", "1").Attr("description", Quote("Mantém as 20 imagens mais recentes e expira as demais"));
                rule.Object("selection", s => s.Attr("tagStatus", Quote("any")).Attr("countType", Quote("imageCountMoreThan")).Attr("countNumber", "20"));
                rule.Object("action", a => a.Attr("type", Quote("expire")));
            }, "[{", "}]"), "jsonencode({", "})");
        });
        return h.ToString();
    }

    private static string SecretsResource()
    {
        var h = new IacHcl();
        h.Section("Application secret");
        h.Comment("Criado sem valor: o valor é gravado fora do Terraform e nunca passa pelo state.");
        h.Block("resource \"aws_secretsmanager_secret\" \"app\"", r => r.Attr("name", Quote("${var.name_prefix}/app")).Attr("tags", "var.tags"));
        return h.ToString();
    }

    private static string IamResource()
    {
        var h = new IacHcl();
        h.Section("Trust");
        h.Block("data \"aws_iam_policy_document\" \"ecs_tasks\"", d => d.Block("statement", s =>
        {
            s.Attr("actions", QuotedList(["sts:AssumeRole"]));
            s.Blank();
            s.Block("principals", p => p.Attr("type", Quote("Service")).Attr("identifiers", QuotedList(["ecs-tasks.amazonaws.com"])));
        }));
        h.Blank();
        h.Section("Execution role");
        h.Block("resource \"aws_iam_role\" \"execution\"", r => r.Attr("name", Quote("${var.name_prefix}-execution"))
            .Attr("assume_role_policy", "data.aws_iam_policy_document.ecs_tasks.json").Attr("tags", "var.tags"));
        h.Blank();
        h.Block("resource \"aws_iam_role_policy_attachment\" \"execution\"", r => r.Attr("role", "aws_iam_role.execution.name")
            .Attr("policy_arn", Quote("arn:aws:iam::aws:policy/service-role/AmazonECSTaskExecutionRolePolicy")));
        h.Blank();
        h.Comment("A execution role lê só os segredos que viram variáveis da tarefa.");
        h.Block("data \"aws_iam_policy_document\" \"secrets\"", d => d.Block("statement", s =>
            s.Attr("actions", QuotedList(["secretsmanager:GetSecretValue"])).Attr("resources", "var.secret_arns")));
        h.Blank();
        h.Block("resource \"aws_iam_role_policy\" \"secrets\"", r => r.Attr("name", Quote("${var.name_prefix}-secrets"))
            .Attr("role", "aws_iam_role.execution.id").Attr("policy", "data.aws_iam_policy_document.secrets.json"));
        h.Blank();
        h.Section("Task role");
        h.Block("resource \"aws_iam_role\" \"task\"", r => r.Attr("name", Quote("${var.name_prefix}-task"))
            .Attr("assume_role_policy", "data.aws_iam_policy_document.ecs_tasks.json").Attr("tags", "var.tags"));
        h.Blank();
        h.Comment("Produtor e consumidor das filas do projeto, só nelas.");
        h.Block("data \"aws_iam_policy_document\" \"queues\"", d =>
        {
            d.Attr("count", "var.grant_queue_access ? 1 : 0");
            d.Blank();
            d.Block("statement", s => s.Attr("actions", QuotedList(["sqs:SendMessage", "sqs:ReceiveMessage", "sqs:DeleteMessage", "sqs:ChangeMessageVisibility",
                "sqs:GetQueueAttributes", "sqs:GetQueueUrl"])).Attr("resources", "var.queue_arns"));
        });
        h.Blank();
        h.Block("resource \"aws_iam_role_policy\" \"queues\"", r =>
        {
            r.Attr("count", "var.grant_queue_access ? 1 : 0");
            r.Blank();
            r.Attr("name", Quote("${var.name_prefix}-queues")).Attr("role", "aws_iam_role.task.id").Attr("policy", "data.aws_iam_policy_document.queues[0].json");
        });
        return h.ToString();
    }

    private static string AlbResource()
    {
        var h = new IacHcl();
        h.Section("Security group");
        h.Block("resource \"aws_security_group\" \"alb\"", r => r.Attr("name", Quote("${var.name_prefix}-alb"))
            .Attr("description", Quote("HTTPS from the internet to the load balancer")).Attr("vpc_id", "var.vpc_id").Attr("tags", "var.tags"));
        h.Blank();
        h.Comment("Só HTTPS entra; não há listener na porta 80.");
        h.Block("resource \"aws_vpc_security_group_ingress_rule\" \"https\"", r => r.Attr("security_group_id", "aws_security_group.alb.id")
            .Attr("cidr_ipv4", Quote("0.0.0.0/0")).Attr("from_port", "443").Attr("to_port", "443").Attr("ip_protocol", Quote("tcp")));
        h.Blank();
        h.Block("resource \"aws_vpc_security_group_egress_rule\" \"all\"", r => r.Attr("security_group_id", "aws_security_group.alb.id")
            .Attr("cidr_ipv4", Quote("0.0.0.0/0")).Attr("ip_protocol", Quote("-1")));
        h.Blank();
        h.Section("Load balancer");
        h.Block("resource \"aws_lb\" \"this\"", r => r.Attr("name", Quote("${var.name_prefix}-alb")).Attr("load_balancer_type", Quote("application"))
            .Attr("security_groups", "[aws_security_group.alb.id]").Attr("subnets", "var.public_subnet_ids").Attr("drop_invalid_header_fields", "true")
            .Attr("tags", "var.tags"));
        h.Blank();
        h.Block("resource \"aws_lb_target_group\" \"service\"", r =>
        {
            r.Attr("name", Quote("${var.name_prefix}-tg")).Attr("port", "var.port").Attr("protocol", Quote("HTTP")).Attr("target_type", Quote("ip"))
                .Attr("vpc_id", "var.vpc_id").Attr("tags", "var.tags");
            r.Blank();
            r.Block("health_check", c => c.Attr("path", "var.health_path").Attr("matcher", Quote("200")).Attr("healthy_threshold", "2")
                .Attr("unhealthy_threshold", "3").Attr("interval", "30"));
        });
        h.Blank();
        h.Block("resource \"aws_lb_listener\" \"https\"", r =>
        {
            r.Attr("load_balancer_arn", "aws_lb.this.arn").Attr("port", "443").Attr("protocol", Quote("HTTPS"))
                .Attr("ssl_policy", Quote("ELBSecurityPolicy-TLS13-1-2-2021-06")).Attr("certificate_arn", "var.certificate_arn");
            r.Blank();
            r.Block("default_action", a => a.Attr("type", Quote("forward")).Attr("target_group_arn", "aws_lb_target_group.service.arn"));
        });
        return h.ToString();
    }

    private static string EcsResource()
    {
        var h = new IacHcl();
        h.Section("Cluster");
        h.Block("resource \"aws_ecs_cluster\" \"this\"", r =>
        {
            r.Attr("name", "var.name_prefix").Attr("tags", "var.tags");
            r.Blank();
            r.Block("setting", s => s.Attr("name", Quote("containerInsights")).Attr("value", Quote("enabled")));
        });
        h.Blank();
        h.Block("resource \"aws_cloudwatch_log_group\" \"service\"", r => r.Attr("name", Quote("/ecs/${var.name_prefix}/${var.service_name}"))
            .Attr("retention_in_days", "30").Attr("tags", "var.tags"));
        h.Blank();
        h.Section("Security group");
        h.Comment("As tarefas só aceitam entrada do load balancer, na porta do contêiner.");
        h.Block("resource \"aws_security_group\" \"tasks\"", r => r.Attr("name", Quote("${var.name_prefix}-${var.service_name}-tasks"))
            .Attr("description", Quote("Service tasks; inbound only from the load balancer")).Attr("vpc_id", "var.vpc_id").Attr("tags", "var.tags"));
        h.Blank();
        h.Block("resource \"aws_vpc_security_group_ingress_rule\" \"from_alb\"", r => r.Attr("security_group_id", "aws_security_group.tasks.id")
            .Attr("referenced_security_group_id", "var.alb_security_group_id").Attr("from_port", "var.port").Attr("to_port", "var.port")
            .Attr("ip_protocol", Quote("tcp")));
        h.Blank();
        h.Block("resource \"aws_vpc_security_group_egress_rule\" \"all\"", r => r.Attr("security_group_id", "aws_security_group.tasks.id")
            .Attr("cidr_ipv4", Quote("0.0.0.0/0")).Attr("ip_protocol", Quote("-1")));
        h.Blank();
        h.Section("Task definition");
        h.Block("resource \"aws_ecs_task_definition\" \"service\"", r =>
        {
            r.Attr("family", Quote("${var.name_prefix}-${var.service_name}")).Attr("requires_compatibilities", QuotedList(["FARGATE"]))
                .Attr("network_mode", Quote("awsvpc")).Attr("cpu", "var.cpu").Attr("memory", "var.memory")
                .Attr("execution_role_arn", "var.execution_role_arn").Attr("task_role_arn", "var.task_role_arn").Attr("tags", "var.tags");
            r.Blank();
            r.Object("container_definitions", c =>
            {
                c.Attr("name", "var.service_name").Attr("image", Quote("${var.repository_url}:${var.image_tag}")).Attr("essential", "true")
                    .Attr("portMappings", "[{ containerPort = var.port, protocol = \"tcp\" }]")
                    .Attr("environment", "[for name, value in var.environment : { name = name, value = value }]")
                    .Attr("secrets", "[for name, from in var.secrets : { name = name, valueFrom = from }]");
                c.Blank();
                c.Object("logConfiguration", l =>
                {
                    l.Attr("logDriver", Quote("awslogs"));
                    l.Object("options", o => o.Attr("awslogs-group", "aws_cloudwatch_log_group.service.name").Attr("awslogs-region", "var.aws_region")
                        .Attr("awslogs-stream-prefix", "var.service_name"));
                });
            }, "jsonencode([{", "}])");
        });
        h.Blank();
        h.Section("Service");
        h.Block("resource \"aws_ecs_service\" \"service\"", r =>
        {
            r.Attr("name", "var.service_name").Attr("cluster", "aws_ecs_cluster.this.id").Attr("task_definition", "aws_ecs_task_definition.service.arn")
                .Attr("desired_count", "var.desired_count").Attr("launch_type", Quote("FARGATE")).Attr("tags", "var.tags");
            r.Blank();
            r.Block("network_configuration", n => n.Attr("subnets", "var.subnet_ids").Attr("security_groups", "[aws_security_group.tasks.id]")
                .Attr("assign_public_ip", "var.assign_public_ip"));
            r.Blank();
            r.Block("load_balancer", l => l.Attr("target_group_arn", "var.target_group_arn").Attr("container_name", "var.service_name").Attr("container_port", "var.port"));
            r.Blank();
            r.Comment("Tarefas novas que não ficam saudáveis voltam o serviço para a revisão anterior.");
            r.Block("deployment_circuit_breaker", d => d.Attr("enable", "true").Attr("rollback", "true"));
            r.Blank();
            r.Block("lifecycle", l =>
            {
                l.Comment("Depois do primeiro apply, o CI é o dono da revisão da task definition.");
                l.Attr("ignore_changes", "[task_definition]");
            });
        });
        return h.ToString();
    }

    private static string EcsVariables() => ModuleVariables(v => v
        .Required("aws_region", "string", "Região dos logs da tarefa")
        .Required("vpc_id", "string", "VPC existente das tarefas")
        .Required("subnet_ids", "list(string)", "Subnets onde as tarefas rodam")
        .Required("assign_public_ip", "bool", "Se cada tarefa recebe IP público (subnets públicas sem NAT)")
        .Required("alb_security_group_id", "string", "Security group do load balancer, única origem de entrada")
        .Required("target_group_arn", "string", "Target group do load balancer")
        .Required("service_name", "string", "Nome do serviço e do contêiner")
        .Required("repository_url", "string", "Repositório ECR da imagem")
        .Required("image_tag", "string", "Tag da imagem da task definition inicial")
        .Required("port", "number", "Porta do contêiner")
        .Required("cpu", "number", "CPU da tarefa (unidades do Fargate)")
        .Required("memory", "number", "Memória da tarefa em MB")
        .Required("desired_count", "number", "Quantas tarefas ficam rodando")
        .Required("execution_role_arn", "string", "Papel de execução do ECS")
        .Required("task_role_arn", "string", "Papel da aplicação")
        .Required("environment", "map(string)", "Variáveis de ambiente do contêiner")
        .Required("secrets", "map(string)", "Variáveis vindas do Secrets Manager: nome para ARN (ou ARN:chave::)"));

    private static string RdsResource()
    {
        var h = new IacHcl();
        h.Block("locals", l => l.Attr("port", "var.engine == \"postgres\" ? 5432 : 3306"));
        h.Blank();
        h.Section("Network");
        h.Block("resource \"aws_db_subnet_group\" \"this\"", r => r.Attr("name", Quote("${var.name_prefix}-db")).Attr("subnet_ids", "var.subnet_ids").Attr("tags", "var.tags"));
        h.Blank();
        h.Block("resource \"aws_security_group\" \"db\"", r => r.Attr("name", Quote("${var.name_prefix}-db"))
            .Attr("description", Quote("Database; inbound only from the allowed security groups")).Attr("vpc_id", "var.vpc_id").Attr("tags", "var.tags"));
        h.Blank();
        h.Comment("Chaves fixas (tasks): o for_each precisa conhecer as chaves no plano, mesmo com os ids ainda por criar.");
        h.Block("resource \"aws_vpc_security_group_ingress_rule\" \"allowed\"", r =>
        {
            r.Attr("for_each", "var.allowed_security_groups");
            r.Blank();
            r.Attr("security_group_id", "aws_security_group.db.id").Attr("referenced_security_group_id", "each.value").Attr("from_port", "local.port")
                .Attr("to_port", "local.port").Attr("ip_protocol", Quote("tcp"));
        });
        h.Blank();
        h.Section("Database");
        h.Block("resource \"aws_db_instance\" \"this\"", r =>
        {
            r.Attr("identifier", Quote("${var.name_prefix}-db")).Attr("engine", "var.engine").Attr("engine_version", "var.engine_version")
                .Attr("instance_class", "var.instance_class").Attr("allocated_storage", "var.storage_gb").Attr("storage_type", Quote("gp3"))
                .Attr("storage_encrypted", "true")
                .Attr("db_name", "var.database_name").Attr("username", Quote("app_admin"));
            r.Comment("A senha é criada e guardada pelo próprio RDS no Secrets Manager: nunca aparece no Terraform.");
            r.Attr("manage_master_user_password", "true").Attr("db_subnet_group_name", "aws_db_subnet_group.this.name")
                .Attr("vpc_security_group_ids", "[aws_security_group.db.id]").Attr("multi_az", "var.multi_az").Attr("publicly_accessible", "false")
                .Attr("backup_retention_period", "7").Attr("copy_tags_to_snapshot", "true").Attr("auto_minor_version_upgrade", "true");
            r.Comment("Apagar o banco pede tirar a proteção antes, e sempre deixa um snapshot final.");
            r.Attr("deletion_protection", "true").Attr("skip_final_snapshot", "false").Attr("final_snapshot_identifier", Quote("${var.name_prefix}-db-final"))
                .Attr("tags", "var.tags");
        });
        return h.ToString();
    }

    private static string RdsVariables() => ModuleVariables(v => v
        .Required("vpc_id", "string", "VPC existente do banco")
        .Required("subnet_ids", "list(string)", "Subnets privadas do grupo de subnets do banco, em duas zonas")
        .Required("engine", "string", "Motor do banco: postgres ou mysql")
        .Required("engine_version", "string", "Versão principal do motor")
        .Required("instance_class", "string", "Classe da instância (ex.: db.t4g.micro)")
        .Required("storage_gb", "number", "Armazenamento em GB")
        .Required("multi_az", "bool", "Réplica em outra zona de disponibilidade")
        .Required("database_name", "string", "Nome do banco criado")
        .Required("allowed_security_groups", "map(string)", "Security groups que podem conectar no banco, por chave fixa"));

    private static string CloudWatchResource(IacBlueprint b)
    {
        var h = new IacHcl();
        if (b.Queues.Count > 0)
        {
            h.Section("Dead-letter alarms");
            h.Comment("Uma mensagem na fila de mensagens mortas é uma falha que se repetiu 5 vezes.");
            h.Block("resource \"aws_cloudwatch_metric_alarm\" \"dead_letters\"", r =>
            {
                r.Attr("for_each", "toset(var.flows)");
                r.Blank();
                r.Attr("alarm_name", Quote("${var.name_prefix}-${each.key}-dlq")).Attr("alarm_description", Quote("Mensagens na fila de mensagens mortas de ${each.key}"))
                    .Attr("namespace", Quote("AWS/SQS")).Attr("metric_name", Quote("ApproximateNumberOfMessagesVisible"))
                    .Attr("dimensions", "{ QueueName = \"${var.name_prefix}-${each.key}-dlq\" }").Attr("statistic", Quote("Maximum")).Attr("period", "300")
                    .Attr("evaluation_periods", "1").Attr("threshold", "0").Attr("comparison_operator", Quote("GreaterThanThreshold"))
                    .Attr("treat_missing_data", Quote("notBreaching")).Attr("alarm_actions", "[var.topic_arn]").Attr("ok_actions", "[var.topic_arn]").Attr("tags", "var.tags");
            });
        }
        if (b.Service is not null)
        {
            h.Blank();
            h.Section("Service alarms");
            h.Comment("Serviço sem nenhuma tarefa rodando por 10 minutos: processo caindo, segredo ausente ou imagem que não puxa.");
            h.Block("resource \"aws_cloudwatch_metric_alarm\" \"no_running_tasks\"", r => r
                .Attr("alarm_name", Quote("${var.name_prefix}-${var.service_name}-no-tasks")).Attr("alarm_description", Quote("Serviço ECS sem tarefa rodando"))
                .Attr("namespace", Quote("ECS/ContainerInsights")).Attr("metric_name", Quote("RunningTaskCount"))
                .Attr("dimensions", "{ ClusterName = var.cluster_name, ServiceName = var.service_name }").Attr("statistic", Quote("Minimum")).Attr("period", "300")
                .Attr("evaluation_periods", "2").Attr("threshold", "1").Attr("comparison_operator", Quote("LessThanThreshold"))
                .Attr("treat_missing_data", Quote("breaching")).Attr("alarm_actions", "[var.topic_arn]").Attr("ok_actions", "[var.topic_arn]").Attr("tags", "var.tags"));
        }
        if (b.Database is not null)
        {
            h.Blank();
            h.Section("Database alarms");
            h.Block("resource \"aws_cloudwatch_metric_alarm\" \"database_cpu\"", r => r
                .Attr("alarm_name", Quote("${var.db_instance_id}-cpu")).Attr("alarm_description", Quote("CPU do banco acima de 80% por 15 minutos"))
                .Attr("namespace", Quote("AWS/RDS")).Attr("metric_name", Quote("CPUUtilization")).Attr("dimensions", "{ DBInstanceIdentifier = var.db_instance_id }")
                .Attr("statistic", Quote("Average")).Attr("period", "300").Attr("evaluation_periods", "3").Attr("threshold", "80")
                .Attr("comparison_operator", Quote("GreaterThanThreshold")).Attr("treat_missing_data", Quote("missing")).Attr("alarm_actions", "[var.topic_arn]")
                .Attr("ok_actions", "[var.topic_arn]").Attr("tags", "var.tags"));
        }
        return h.ToString();
    }

    private static string CloudWatchVariables(IacBlueprint b) => ModuleVariables(v =>
    {
        v.Required("topic_arn", "string", "Tópico SNS que recebe os alarmes");
        if (b.Queues.Count > 0) v.Required("flows", "list(string)", "Fluxos com fila de mensagens mortas");
        if (b.Service is not null) v.Required("cluster_name", "string", "Cluster ECS").Required("service_name", "string", "Serviço ECS");
        if (b.Database is not null) v.Required("db_instance_id", "string", "Identificador da instância RDS");
    });

    internal static string Outputs(params (string Name, string Description, string Value)[] outputs)
    {
        var h = new IacHcl();
        foreach (var (name, description, value) in outputs)
        {
            h.Block($"output \"{name}\"", o => o.Attr("description", Quote(description)).Attr("value", value));
            h.Blank();
        }
        return h.ToString();
    }

    // name_prefix e tags em todo módulo, como environment e tags no modelo.
    internal static string ModuleVariables(Action<Variables> extra)
    {
        var v = new Variables();
        v.Validated("name_prefix", "string", "Prefixo dos nomes: <projeto>-<ambiente>", "can(regex(\"^[a-z0-9-]+$\", var.name_prefix))",
            "O prefixo deve conter apenas letras minúsculas, dígitos e hífens.", "Environment");
        extra(v);
        v.Default("tags", "map(string)", "Tags aplicadas aos recursos", "{}", "Tags");
        return v.ToString();
    }

    internal sealed class Variables
    {
        private readonly IacHcl _h = new();
        private bool _first = true;

        public Variables Required(string name, string type, string description, string? section = null) => Add(name, type, description, null, null, null, section);

        public Variables Nullable(string name, string type, string description) => Add(name, type, description, "null", null, null, null);

        public Variables Default(string name, string type, string description, string value, string? section = null) =>
            Add(name, type, description, value, null, null, section);

        public Variables Validated(string name, string type, string description, string condition, string message, string? section = null) =>
            Add(name, type, description, null, condition, message, section);

        private Variables Add(string name, string type, string description, string? value, string? condition, string? message, string? section)
        {
            if (!_first) _h.Blank();
            _first = false;
            if (section is not null) _h.Section(section);
            _h.Block($"variable \"{name}\"", v =>
            {
                v.Attr("description", Quote(description)).Attr("type", type);
                if (value is not null) v.Attr("default", value);
                if (condition is not null)
                {
                    v.Blank();
                    v.Block("validation", c => c.Attr("condition", condition).Attr("error_message", Quote(message!)));
                }
            });
            return this;
        }

        public override string ToString() => _h.ToString();
    }
}
