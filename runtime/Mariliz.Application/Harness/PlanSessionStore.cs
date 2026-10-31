using Mariliz.Application.Execution;

namespace Mariliz.Application.Harness;

/// <summary>Guarda os últimos planos gerados para que a aplicação use exatamente o plano que a pessoa revisou.</summary>
internal sealed class PlanSessionStore
{
    private const int Capacity = 20;

    private readonly Dictionary<string, PlanSession> _sessions = new();
    private readonly object _gate = new();

    public void Save(PlanSession session)
    {
        lock (_gate)
        {
            _sessions[session.Plan.PlanId] = session;
            if (_sessions.Count > Capacity) _sessions.Remove(_sessions.Keys.First());
        }
    }

    public PlanSession? Find(string planId)
    {
        lock (_gate) return _sessions.GetValueOrDefault(planId);
    }
}
