using System.Text.RegularExpressions;

namespace Mariliz.Application.Iac;

// O bootstrap do bucket de state no formato do modelo (TERRAFORM_PROJECT_LAYOUT.md, seções 1, 2, 5): providers.tf sem
// backend remoto (a raiz cria o bucket que guarda o state das outras), variables.tf com validação, resource.tf com
// prevent_destroy, versionamento, criptografia e acesso público bloqueado, output.tf com o que a raiz principal precisa.
// Versões dos providers fixadas, conferidas nos registros do Terraform e do OpenTofu em 2026-11-15.
public static partial class IacBootstrapTemplates
{
    public const string AwsVersion = "6.73.0";
    public const string AzurermVersion = "5.14.0";
    public const string GoogleVersion = "8.11.0";

    public sealed record Generated(IReadOnlyList<(string File, string Content)> Files, string BackendBlock, IReadOnlyList<string> Notes);

    public static Generated Aws(string project, string bucket, string region, string? profile, string? account)
    {
        Require(bucket, CloudIdentifiers.S3Bucket(), "O bucket deve ter de 3 a 63 caracteres: letras minúsculas, dígitos, ponto e hífen.");
        Require(region, CloudIdentifiers.AwsRegion(), "A região deve estar no formato sa-east-1.");
        if (profile is not null) Require(profile, CloudIdentifiers.AwsProfile(), "O perfil deve ter só letras, dígitos, ponto, hífen e sublinhado.");
        if (account is not null) Require(account, CloudIdentifiers.AwsAccount(), "A conta AWS tem 12 dígitos.");

        var providers = $$"""
            terraform {
              required_version = ">= 1.11"

              required_providers {
                aws = {
                  source  = "hashicorp/aws"
                  version = "{{AwsVersion}}"
                }
              }
              # Sem backend remoto de propósito: esta raiz cria o bucket que guarda o state das demais.
            }

            provider "aws" {
            {{ProviderAttributes(account)}}
            }
            """;
        var variables = $$"""
            # ------------------------- AWS Region -------------------------------
            variable "aws_region" {
              description = "Região da AWS onde o bucket de state é criado"
              type        = string
              default     = "{{region}}"
            }

            # ------------------------- AWS Profile ------------------------------
            variable "aws_profile" {
              description = "Profile da AWS CLI (SSO) usado para autenticar; null usa a cadeia padrão de credenciais"
              type        = string
              default     = {{(profile is null ? "null" : $"\"{profile}\"")}}
            }

            # ------------------------- State Bucket -----------------------------
            variable "bucket_state" {
              description = "Nome do bucket S3 que guarda o state do Terraform de todos os ambientes"
              type        = string
              default     = "{{bucket}}"

              validation {
                condition     = can(regex("^[a-z0-9][a-z0-9.-]{1,61}[a-z0-9]$", var.bucket_state))
                error_message = "O bucket deve ter de 3 a 63 caracteres: letras minúsculas, dígitos, ponto e hífen."
              }
            }
            """;
        var resource = $$"""
            # ------------------------- State Bucket -----------------------------
            resource "aws_s3_bucket" "tf_state" {
              bucket = var.bucket_state

              tags = {
                Project     = "{{project}}"
                Environment = "shared"
                ManagedBy   = "terraform"
              }

              lifecycle {
                # Apagar este bucket apaga o state de todos os ambientes.
                prevent_destroy = true
              }
            }

            # Mantém versões anteriores do state para recuperar de uma escrita errada.
            resource "aws_s3_bucket_versioning" "tf_state" {
              bucket = aws_s3_bucket.tf_state.id

              versioning_configuration {
                status = "Enabled"
              }
            }

            resource "aws_s3_bucket_server_side_encryption_configuration" "tf_state" {
              bucket = aws_s3_bucket.tf_state.id

              rule {
                apply_server_side_encryption_by_default {
                  sse_algorithm = "AES256"
                }
              }
            }

            resource "aws_s3_bucket_public_access_block" "tf_state" {
              bucket = aws_s3_bucket.tf_state.id

              block_public_acls       = true
              block_public_policy     = true
              ignore_public_acls      = true
              restrict_public_buckets = true
            }
            """;
        var output = """
            output "state_bucket_name" {
              description = "Nome do bucket S3 que guarda o state; vai no bloco backend da raiz principal"
              value       = aws_s3_bucket.tf_state.bucket
            }
            """;
        var backend = $$"""
            backend "s3" {
              bucket               = "{{bucket}}"
              key                  = "terraform.tfstate"
              workspace_key_prefix = "{{project}}"
              region               = "{{region}}"
            {{(profile is null ? "" : $"  profile              = \"{profile}\"\n")}}  encrypt              = true
              use_lockfile         = true
            }
            """;
        return new Generated(Files(providers, variables, resource, output), backend, Notes("bucket S3", "s3"));
    }

    public static Generated Azure(string project, string resourceGroup, string storageAccount, string container, string location, string? subscription)
    {
        Require(resourceGroup, AzureGroup(), "O grupo de recursos deve ter até 90 caracteres: letras, dígitos, ponto, hífen, sublinhado e parênteses.");
        Require(storageAccount, CloudIdentifiers.AzureStorage(), "A storage account deve ter de 3 a 24 letras minúsculas e dígitos.");
        Require(container, CloudIdentifiers.AzureContainer(), "O container deve ter de 3 a 63 caracteres: letras minúsculas, dígitos e hífen.");
        Require(location, CloudIdentifiers.AzureLocation(), "A região do Azure deve estar no formato brazilsouth.");
        if (subscription is not null) Require(subscription, CloudIdentifiers.Guid(), "A subscription é um GUID.");

        var providers = $$"""
            terraform {
              required_version = ">= 1.6"

              required_providers {
                azurerm = {
                  source  = "hashicorp/azurerm"
                  version = "{{AzurermVersion}}"
                }
              }
              # Sem backend remoto de propósito: esta raiz cria a storage account que guarda o state das demais.
            }

            provider "azurerm" {
              features {}
              subscription_id = var.subscription_id
            }
            """;
        var variables = $$"""
            # ------------------------- Subscription ------------------------------
            variable "subscription_id" {
              description = "Subscription onde a storage account de state é criada; qualquer outra é recusada pelo provider"
              type        = string
              default     = {{(subscription is null ? "null" : $"\"{subscription}\"")}}
            }

            # ------------------------- Location ----------------------------------
            variable "location" {
              description = "Região do Azure da storage account de state"
              type        = string
              default     = "{{location}}"
            }

            # ------------------------- State Storage -----------------------------
            variable "resource_group_name" {
              description = "Grupo de recursos que guarda a storage account de state"
              type        = string
              default     = "{{resourceGroup}}"
            }

            variable "storage_account_name" {
              description = "Storage account que guarda o state de todos os ambientes"
              type        = string
              default     = "{{storageAccount}}"

              validation {
                condition     = can(regex("^[a-z0-9]{3,24}$", var.storage_account_name))
                error_message = "A storage account deve ter de 3 a 24 letras minúsculas e dígitos."
              }
            }

            variable "container_name" {
              description = "Container privado onde ficam os arquivos de state"
              type        = string
              default     = "{{container}}"
            }
            """;
        var resource = $$"""
            # ------------------------- Resource Group ----------------------------
            resource "azurerm_resource_group" "tf_state" {
              name     = var.resource_group_name
              location = var.location

              tags = {
                Project   = "{{project}}"
                ManagedBy = "terraform"
              }
            }

            # ------------------------- State Storage -----------------------------
            resource "azurerm_storage_account" "tf_state" {
              name                            = var.storage_account_name
              resource_group_name             = azurerm_resource_group.tf_state.name
              location                        = azurerm_resource_group.tf_state.location
              account_tier                    = "Standard"
              account_replication_type        = "GRS"
              min_tls_version                 = "TLS1_2"
              allow_nested_items_to_be_public = false

              # Versões e exclusão reversível dos blobs: recuperar o state de uma escrita errada.
              blob_properties {
                versioning_enabled = true

                delete_retention_policy {
                  days = 30
                }
              }

              tags = {
                Project   = "{{project}}"
                ManagedBy = "terraform"
              }

              lifecycle {
                # Apagar esta conta apaga o state de todos os ambientes.
                prevent_destroy = true
              }
            }

            resource "azurerm_storage_container" "tf_state" {
              name                  = var.container_name
              storage_account_id    = azurerm_storage_account.tf_state.id
              container_access_type = "private"
            }
            """;
        var output = """
            output "storage_account_name" {
              description = "Storage account do state; vai no bloco backend da raiz principal"
              value       = azurerm_storage_account.tf_state.name
            }

            output "container_name" {
              description = "Container do state; vai no bloco backend da raiz principal"
              value       = azurerm_storage_container.tf_state.name
            }
            """;
        var backend = $$"""
            backend "azurerm" {
              resource_group_name  = "{{resourceGroup}}"
              storage_account_name = "{{storageAccount}}"
              container_name       = "{{container}}"
              key                  = "{{project}}.tfstate"
              use_azuread_auth     = true
            }
            """;
        return new Generated(Files(providers, variables, resource, output), backend, Notes("storage account", "azurerm"));
    }

    public static Generated Gcp(string project, string bucket, string gcpProject, string location)
    {
        Require(bucket, CloudIdentifiers.GcsBucket(), "O bucket deve ter de 3 a 63 caracteres: letras minúsculas, dígitos, ponto, hífen e sublinhado.");
        Require(gcpProject, CloudIdentifiers.GoogleProject(), "O projeto do Google Cloud deve ter de 6 a 30 caracteres: letras minúsculas, dígitos e hífen.");
        Require(location, GcsLocation(), "A localização deve estar no formato US ou southamerica-east1.");

        var providers = $$"""
            terraform {
              required_version = ">= 1.6"

              required_providers {
                google = {
                  source  = "hashicorp/google"
                  version = "{{GoogleVersion}}"
                }
              }
              # Sem backend remoto de propósito: esta raiz cria o bucket que guarda o state das demais.
            }

            provider "google" {
              project = var.project_id
            }
            """;
        var variables = $$"""
            # ------------------------- Project -----------------------------------
            variable "project_id" {
              description = "Projeto do Google Cloud onde o bucket de state é criado"
              type        = string
              default     = "{{gcpProject}}"
            }

            # ------------------------- Location ----------------------------------
            variable "location" {
              description = "Localização do bucket de state (multirregião ou região)"
              type        = string
              default     = "{{location}}"
            }

            # ------------------------- State Bucket ------------------------------
            variable "bucket_state" {
              description = "Nome do bucket que guarda o state de todos os ambientes"
              type        = string
              default     = "{{bucket}}"

              validation {
                condition     = can(regex("^[a-z0-9][a-z0-9._-]{1,61}[a-z0-9]$", var.bucket_state))
                error_message = "O bucket deve ter de 3 a 63 caracteres: letras minúsculas, dígitos, ponto, hífen e sublinhado."
              }
            }
            """;
        var resource = $$"""
            # ------------------------- State Bucket ------------------------------
            resource "google_storage_bucket" "tf_state" {
              name                        = var.bucket_state
              location                    = var.location
              uniform_bucket_level_access = true
              public_access_prevention    = "enforced"
              force_destroy               = false

              # Mantém versões anteriores do state para recuperar de uma escrita errada.
              versioning {
                enabled = true
              }

              labels = {
                project    = "{{project}}"
                managed-by = "terraform"
              }

              lifecycle {
                # Apagar este bucket apaga o state de todos os ambientes.
                prevent_destroy = true
              }
            }
            """;
        var output = """
            output "state_bucket_name" {
              description = "Nome do bucket que guarda o state; vai no bloco backend da raiz principal"
              value       = google_storage_bucket.tf_state.name
            }
            """;
        var backend = $$"""
            backend "gcs" {
              bucket = "{{bucket}}"
              prefix = "{{project}}"
            }
            """;
        return new Generated(Files(providers, variables, resource, output), backend, Notes("bucket", "gcs"));
    }

    // O fmt alinha o "=" de atributos seguidos: com allowed_account_ids, região e perfil ganham o mesmo recuo.
    private static string ProviderAttributes(string? account)
    {
        var attributes = new List<(string Name, string Value)> { ("region", "var.aws_region"), ("profile", "var.aws_profile") };
        if (account is not null) attributes.Add(("allowed_account_ids", $"[\"{account}\"]"));
        var width = attributes.Max(a => a.Name.Length);
        return string.Join("\n", attributes.Select(a => $"  {a.Name.PadRight(width)} = {a.Value}"));
    }

    // Nome do projeto vira prefixo e tag: minúsculas, dígitos e hífen.
    public static string ProjectSlug(string name)
    {
        var slug = Slug().Replace(name.ToLowerInvariant(), "-").Trim('-');
        return slug.Length == 0 ? "projeto" : slug[..Math.Min(slug.Length, 40)];
    }

    private static IReadOnlyList<(string File, string Content)> Files(string providers, string variables, string resource, string output) =>
    [
        ("providers.tf", providers.TrimEnd() + "\n"),
        ("variables.tf", variables.TrimEnd() + "\n"),
        ("resource.tf", resource.TrimEnd() + "\n"),
        ("output.tf", output.TrimEnd() + "\n")
    ];

    private static IReadOnlyList<string> Notes(string what, string backend) =>
    [
        $"Grava só arquivos: o {what} não é criado agora. Depois, valide, gere o plano da pasta bootstrap e aprove o apply na tela.",
        "O state do bootstrap fica local, na própria pasta e fora do git (.gitignore); o Spock aplica na cópia e devolve o state ao projeto.",
        $"Com o {what} criado, copie o bloco backend \"{backend}\" para o terraform {{}} do providers.tf da raiz principal e migre o state " +
            "(init -migrate-state) numa operação à parte; o Spock não migra state sozinho."
    ];

    private static void Require(string value, Regex pattern, string message)
    {
        if (!pattern.IsMatch(value)) throw IacErrors.BootstrapValueInvalid(message);
    }

    [GeneratedRegex(@"^[-\w._()]{1,90}$")]
    private static partial Regex AzureGroup();

    [GeneratedRegex("^[A-Za-z0-9-]{2,40}$")]
    private static partial Regex GcsLocation();

    [GeneratedRegex("[^a-z0-9]+")]
    private static partial Regex Slug();
}
