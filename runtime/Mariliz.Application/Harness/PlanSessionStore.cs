using Mariliz.Application.Execution;

namespace Mariliz.Application.Harness;

// A aplicação usa exatamente o plano que a pessoa revisou. A ordem de saída é explícita: o Dictionary reaproveita posições liberadas e não garante a ordem de inserção.
internal sealed class PlanSessionStore
{
    private const int Capacity = 20;

    private readonly OrderedDictionary<string, PlanSession> _sessions = new();
    private readonly object _gate = new();

    public void Save(PlanSession session)
    {
        lock (_gate)
        {
            _sessions.Remove(session.Plan.PlanId);
            _sessions.Add(session.Plan.PlanId, session);
            if (_sessions.Count > Capacity) _sessions.RemoveAt(0);
        }
    }

    public PlanSession? Find(string planId)
    {
        lock (_gate) return _sessions.GetValueOrDefault(planId);
    }
}
