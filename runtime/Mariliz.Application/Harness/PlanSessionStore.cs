using Mariliz.Application.Execution;

namespace Mariliz.Application.Harness;

/// <summary>
/// Guarda os últimos planos gerados para que a aplicação use exatamente o plano que a pessoa revisou. Ao passar da
/// capacidade, sai o plano gerado há mais tempo: a ordem é explícita (o <c>Dictionary</c> reaproveita posições liberadas
/// e não garante a ordem de inserção).
/// </summary>
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
