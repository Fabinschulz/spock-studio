# Roadmap dos conectores cloud

O que vem depois da primeira entrega (AWS validada, Azure e Google implementados sem identidade para validar). A ordem é
por valor e risco; nada aqui está decidido sem aprovação do dono.

## Curto prazo

1. **Validar Azure e Google com identidade de leitura.** Uma conta de teste com `Reader` no Azure e `roles/viewer` no
   Google, seguindo a seção 3 de [CLOUD_CONNECTORS_STATUS.md](CLOUD_CONNECTORS_STATUS.md). Só então marcá-los como
   prontos.
2. **Login por código de dispositivo dentro do Spock.** O AWS SDK oferece o fluxo de SSO com callback de verificação e o
   Azure.Identity tem `DeviceCodeCredential`. A tela mostraria o link e o código (estado "Aguardando autenticação"),
   sem terminal. Exige cuidado: só a pessoa inicia, nunca um agente.
3. **Lista de ferramentas MCP dinâmica.** Recalcular `tools/list` a cada pedido e anunciar `listChanged`, para uma
   leitura liberada aparecer sem reiniciar a IDE.
4. **`aws login` (`login_session`).** Acompanhar o suporte no AWS SDK para .NET; até lá, a mensagem orienta.

## Médio prazo

5. **Agentes internos consumindo cloud.** O workflow de engenharia e a investigação de gestão poderiam pedir leituras
   cloud como fatos, pela mesma `CloudReadService` e com o mesmo vínculo e `agentAccess`, com cada leitura citada como
   evidência na resposta.
6. **Mais leituras de alto valor para engenharia:** VPC e sub-redes, Route 53, IAM (papéis e políticas anexadas, só
   leitura), logs recentes do CloudWatch Logs com filtro fixo, Cost Explorer agregado. Cada uma com permissão
   declarada e colunas fixas.
7. **Leitura cruzada com o projeto.** Relacionar recursos à infraestrutura como código do repositório (pilha
   CloudFormation × template, state do Terraform, recursos com tag do projeto), para responder "o que deste repositório
   está rodando". O lado Terraform está em [INFRASTRUCTURE_MANAGEMENT.md](INFRASTRUCTURE_MANAGEMENT.md).

## Longo prazo

8. **Operações de escrita** fora do Terraform (criar e mudar infraestrutura vai pelo Terraform, com plano e aprovação:
   [TERRAFORM_EXECUTION_LIFECYCLE.md](TERRAFORM_EXECUTION_LIFECYCLE.md)). Uma escrita direta pelo conector, se um dia
   existir, só com: capacidade própria `impact: write`, permissão explícita por vínculo, confirmação
   da pessoa a cada execução (o mesmo caminho das ações aprovadas dos workflows), desfazer quando possível e trilha
   própria. Nenhum agente executaria escrita sem aprovação.
9. **Provedores adicionais** (Oracle Cloud, DigitalOcean, Cloudflare, Kubernetes como "provedor" de leitura) seguindo
   a seção 7 de [CLOUD_CONNECTORS_ARCHITECTURE.md](CLOUD_CONNECTORS_ARCHITECTURE.md).
10. **Provedores fora do executável.** Carregar conectores de terceiros exigiria assinatura, isolamento de processo e
    um contrato estável da porta `ICloudProvider`; só faz sentido se houver demanda real.
