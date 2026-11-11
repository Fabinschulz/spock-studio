namespace Mariliz.Application.Integrations;

/// <summary>Conexões e integrações de projeto, no diretório de dados do motor. Nunca dentro de um projeto.</summary>
public interface IIntegrationStore
{
    IReadOnlyList<IntegrationConnection> Connections();

    void SaveConnection(IntegrationConnection connection);

    void RemoveConnection(string connectionId);

    IReadOnlyList<ProjectIntegration> Integrations();

    void SaveIntegration(ProjectIntegration integration);

    void RemoveIntegration(string integrationId);
}
