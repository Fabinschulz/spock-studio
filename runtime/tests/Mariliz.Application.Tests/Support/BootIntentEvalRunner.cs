using Mariliz.Application.Boot;
using Mariliz.Application.Tests;

namespace Mariliz.Application.Tests.Support;

// A régua é a mesma do léxico: o reconhecedor primeiro e, só onde ele não resolve, o estágio do modelo, com a mesma regra de combinação
// que o Boot usa. Sem estágio, é a medida do léxico puro.
internal static class BootIntentEvalRunner
{
    internal sealed record CaseResult(string Text, string Expected, string Lexicon, string Final, bool ModelCalled);

    internal sealed record Score(IReadOnlyList<CaseResult> Cases, int ModelCalls, int InputTokens, int OutputTokens)
    {
        public int Hits => Cases.Count(item => item.Final == item.Expected);

        public int LexiconHits => Cases.Count(item => item.Lexicon == item.Expected);

        // Onde o léxico errou e se o modelo sequer foi chamado: o estágio só alcança o que o léxico deixou sem resolver.
        public IEnumerable<string> LexiconMisses => Cases.Where(item => item.Lexicon != item.Expected).Select(item =>
            $"\"{item.Text}\": esperado {item.Expected}, léxico {item.Lexicon}, {(item.ModelCalled ? "o modelo foi chamado" : "o modelo não foi chamado (o léxico resolveu)")}");

        public IEnumerable<string> Changes => Cases.Where(item => item.Final != item.Lexicon).Select(item =>
            $"\"{item.Text}\": léxico {item.Lexicon}, com modelo {item.Final}, esperado {item.Expected} ({(item.Final == item.Expected ? "o modelo acertou" : item.Lexicon == item.Expected ? "o modelo estragou" : "os dois erraram")})");
    }

    public static async Task<Score> RunAsync(IEnumerable<BootIntentEvalTests.Case> cases, ModelIntentStage? stage, CancellationToken ct = default)
    {
        var recognizer = new IntentRecognizer(new CapabilityRegistry());
        var results = new List<CaseResult>();
        int calls = 0, input = 0, output = 0;
        foreach (var testCase in cases)
        {
            var lexicon = recognizer.Recognize(new IntentRequest(testCase.Text, testCase.Hint, null, testCase.HasProject));
            var final = lexicon;
            var called = false;
            if (stage is not null && lexicon.Resolution != IntentResolution.Resolved)
            {
                var proposal = await stage.ProposeAsync(testCase.Text, ct);
                calls += proposal.Usage.Calls;
                input += proposal.Usage.InputTokens;
                output += proposal.Usage.OutputTokens;
                called = proposal.Usage.Calls > 0;
                final = recognizer.WithModel(lexicon, proposal);
            }
            results.Add(new CaseResult(testCase.Text, testCase.Expected, BootIntentEvalTests.Outcome(lexicon), BootIntentEvalTests.Outcome(final), called));
        }
        return new Score(results, calls, input, output);
    }
}
