using Mariliz.Application.Protocol;

namespace Mariliz.Application.Iac;

// Os prazos de cada etapa da ferramenta e o total de cada operação. As etapas somadas passavam da espera do host
// (engine.rs): um apply com verificação chegava a ~103 min contra 90, e a tela mostrava ENGINE_TIMEOUT enquanto a nuvem
// continuava mudando (NET-S08). O total vale para a operação inteira, e cada etapa ganha o próprio prazo ou o que sobrou.
public static class IacTimeouts
{
    public static readonly TimeSpan Short = TimeSpan.FromMinutes(2);
    public static readonly TimeSpan Init = TimeSpan.FromMinutes(10);
    public static readonly TimeSpan Plan = TimeSpan.FromMinutes(30);
    public static readonly TimeSpan Apply = TimeSpan.FromMinutes(55);

    // O que o serviço faz fora da ferramenta (cópia da configuração, credencial temporária, leituras da nuvem no drift,
    // registro) cabe nesta folga entre o total da operação e a espera do host.
    public static readonly TimeSpan HostMargin = TimeSpan.FromMinutes(5);

    public static readonly TimeSpan ValidateTotal = TimeSpan.FromMinutes(40);
    public static readonly TimeSpan PlanTotal = TimeSpan.FromMinutes(40);
    public static readonly TimeSpan DriftTotal = TimeSpan.FromMinutes(40);
    public static readonly TimeSpan StateTotal = TimeSpan.FromMinutes(40);
    public static readonly TimeSpan UnlockTotal = TimeSpan.FromMinutes(40);

    // init, workspace select e o apply inteiro cabem antes de o total cortar alguma coisa: o corte só alcança a verificação
    // depois do apply, nunca o apply em andamento.
    public static readonly TimeSpan ApplyTotal = TimeSpan.FromMinutes(85);

    // Por método do protocolo: o teste confere cada um contra a espera do host.
    public static readonly IReadOnlyDictionary<string, TimeSpan> ByMethod = new Dictionary<string, TimeSpan>(StringComparer.Ordinal)
    {
        [ProtocolMethods.IacValidate] = ValidateTotal,
        [ProtocolMethods.IacPlan] = PlanTotal,
        [ProtocolMethods.IacDrift] = DriftTotal,
        [ProtocolMethods.IacStateResources] = StateTotal,
        [ProtocolMethods.IacStateUnlock] = UnlockTotal,
        [ProtocolMethods.IacApply] = ApplyTotal
    };
}

// O total de uma operação repartido entre as etapas, contado desde que o serviço recebeu o pedido.
public sealed class IacDeadline
{
    private readonly TimeProvider _time;
    private readonly long _started;

    public IacDeadline(TimeSpan total, TimeProvider? time = null)
    {
        Total = total;
        _time = time ?? TimeProvider.System;
        _started = _time.GetTimestamp();
    }

    // Sem total: cada etapa fica só com o próprio prazo (adaptador usado fora de uma operação do serviço).
    public static IacDeadline Unlimited { get; } = new(TimeSpan.MaxValue);

    public TimeSpan Total { get; }

    public TimeSpan Remaining
    {
        get
        {
            if (Total == TimeSpan.MaxValue) return TimeSpan.MaxValue;
            var left = Total - _time.GetElapsedTime(_started);
            return left > TimeSpan.Zero ? left : TimeSpan.Zero;
        }
    }

    // Zero quando o total acabou: a etapa nem começa.
    public TimeSpan Stage(TimeSpan limit) => Remaining < limit ? Remaining : limit;
}
