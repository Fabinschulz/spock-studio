namespace Mariliz.Application.Integrations;

// Nunca dentro de um projeto.
public interface IIntegrationStore
{
    IReadOnlyList<IntegrationConnection> Connections();

    void SaveConnection(IntegrationConnection connection);

    void RemoveConnection(string connectionId);

    IReadOnlyList<ProjectIntegration> Integrations();

    void SaveIntegration(ProjectIntegration integration);

    void RemoveIntegration(string integrationId);
}
