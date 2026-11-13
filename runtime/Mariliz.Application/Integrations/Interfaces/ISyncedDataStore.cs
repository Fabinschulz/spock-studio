namespace Mariliz.Application.Integrations;

public interface ISyncedDataStore
{
    SyncedData Read(string projectId, string integrationId);

    void Write(string projectId, string integrationId, SyncedData data);

    void Delete(string projectId, string integrationId);
}
