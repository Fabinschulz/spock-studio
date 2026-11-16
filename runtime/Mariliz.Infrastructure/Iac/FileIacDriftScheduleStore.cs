using System.Text.Json;
using Mariliz.Application.Iac;
using Mariliz.Application.Integrations;
using Mariliz.Infrastructure.Integrations;

namespace Mariliz.Infrastructure.Iac;

// Um agendamento por projeto, na pasta do Spock e nunca dentro do projeto.
public sealed class FileIacDriftScheduleStore(string dataRoot) : IIacDriftScheduleStore
{
    private readonly object _gate = new();
    private string FilePath => Path.Combine(dataRoot, "iac", "drift-schedules.json");

    public IReadOnlyList<IacDriftSchedule> All()
    {
        lock (_gate) return AtomicFile.ReadJson<List<IacDriftSchedule>>(FilePath, IntegrationJson.Options, s => s is not null) ?? [];
    }

    public void Save(IacDriftSchedule schedule)
    {
        lock (_gate)
        {
            var all = (AtomicFile.ReadJson<List<IacDriftSchedule>>(FilePath, IntegrationJson.Options, s => s is not null) ?? [])
                .Where(s => s.ProjectId != schedule.ProjectId).Append(schedule).ToList();
            AtomicFile.Write(FilePath, JsonSerializer.Serialize(all, IntegrationJson.Options));
        }
    }
}
