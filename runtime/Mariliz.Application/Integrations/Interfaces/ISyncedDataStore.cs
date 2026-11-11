namespace Mariliz.Application.Integrations;

/// <summary>Dados sincronizados, endereçados sempre pelo par projeto e integração.</summary>
public interface ISyncedDataStore
{
    /// <summary>Devolve dados vazios quando ainda não houve sincronização.</summary>
    SyncedData Read(string projectId, string integrationId);

    void Write(string projectId, string integrationId, SyncedData data);

    void Delete(string projectId, string integrationId);
}
