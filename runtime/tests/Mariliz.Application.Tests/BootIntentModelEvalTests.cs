using Mariliz.Application.Boot;
using Mariliz.Application.Models;
using Mariliz.Application.Tests.Support;
using Mariliz.Infrastructure;

namespace Mariliz.Application.Tests;

// A eval de intenção com o segundo estágio. O dublê responde por uma regra boba (palavras em comum com o nome e a descrição da capacidade) e
// serve só para provar o encanamento: que o estágio roda os dois corpora pela mesma régua e que nunca troca o que o léxico já resolveu. O número
// dele NÃO é a qualidade de um modelo. A medida real é o teste ao vivo (LiveFact), que roda o holdout com o provedor do dono.
public class BootIntentModelEvalTests(ITestOutputHelper output)
{
    private sealed class OverlapChat(CapabilityRegistry registry) : IModelChatTransport
    {
        public Task<ChatTransportResult> SendChatAsync(Uri endpoint, string key, string model, ChatRequest request, CancellationToken ct)
        {
            var user = request.Messages[1].Content;
            var text = user["<pedido>\n".Length..^"\n</pedido>".Length];
            var words = Words(text);
            var best = registry.All
                .Select(capability => (capability.Id, Overlap: Words(capability.Name + " " + capability.Description).Count(words.Contains)))
                .OrderByDescending(item => item.Overlap).ThenBy(item => item.Id, StringComparer.Ordinal).First();
            var reply = best.Overlap >= 2 ? $$"""{"capabilityId": "{{best.Id}}", "alternatives": []}""" : ScriptedIntentChat.NoCapability;
            return Task.FromResult(new ChatTransportResult(reply, "duble-por-regra", 1200, 25));
        }

        private static HashSet<string> Words(string text) => [.. IntentRecognizer.Tokens(text).Where(word => word.Length >= 5)];
    }

    private static ModelIntentStage Stage(IModelChatTransport chat, string provider = "openai", string key = "sk-chave-de-teste-eval-0001")
    {
        var registry = new CapabilityRegistry();
        return new ModelIntentStage(new ModelGateway(new EnvModelSettings(provider, key), new UnusedModelTransport(), chat: chat), registry);
    }

    private void Report(string label, BootIntentEvalRunner.Score without, BootIntentEvalRunner.Score with)
    {
        var total = without.Cases.Count;
        output.WriteLine($"[eval de intenção com modelo, {label}] sem modelo {without.Hits}/{total} ({(double)without.Hits / total:P0}); com modelo {with.Hits}/{total} ({(double)with.Hits / total:P0}); " +
            $"{with.ModelCalls} chamadas, {with.InputTokens} tokens de entrada e {with.OutputTokens} de saída");
        foreach (var miss in with.LexiconMisses) output.WriteLine("  léxico errou: " + miss);
        foreach (var change in with.Changes) output.WriteLine("  mudou: " + change);
    }

    [Theory]
    [InlineData("ajuste")]
    [InlineData("holdout")]
    public async Task Com_o_modelo_duble_os_dois_corpora_passam_pelo_mesmo_encanamento_e_o_lexico_resolvido_nunca_muda(string corpus)
    {
        var cases = corpus == "ajuste" ? BootIntentEvalTests.Corpus : BootIntentEvalTests.Holdout;

        var without = await BootIntentEvalRunner.RunAsync(cases, null);
        var with = await BootIntentEvalRunner.RunAsync(cases, Stage(new OverlapChat(new CapabilityRegistry())));
        Report(corpus + " (dublê por regra, não é medida de qualidade)", without, with);

        Assert.Equal(cases.Length, with.Cases.Count);
        Assert.Equal(0, without.ModelCalls);
        // O modelo só é chamado onde o léxico não resolveu, e onde o léxico resolveu a resposta final é a dele.
        Assert.All(with.Cases, item =>
        {
            var lexiconResolved = item.Lexicon is not ("?" or "-");
            Assert.Equal(!lexiconResolved, item.ModelCalled);
            if (lexiconResolved) Assert.Equal(item.Lexicon, item.Final);
        });
        Assert.Equal(with.Cases.Count(item => item.ModelCalled), with.ModelCalls);
        Assert.Equal(without.Hits, without.LexiconHits);
        Assert.Equal(without.LexiconHits, with.LexiconHits);
    }

    [Fact]
    public async Task O_estagio_com_um_modelo_que_so_diz_nenhuma_serve_nao_muda_nenhum_resultado_do_holdout()
    {
        var silent = new ScriptedIntentChat();

        var without = await BootIntentEvalRunner.RunAsync(BootIntentEvalTests.Holdout, null);
        var with = await BootIntentEvalRunner.RunAsync(BootIntentEvalTests.Holdout, Stage(silent));

        Assert.Equal(without.Cases.Select(item => item.Final), with.Cases.Select(item => item.Final));
        Assert.Equal(with.Cases.Count(item => item.ModelCalled), silent.Seen.Count);
    }

    // Só roda com a chave do provedor no terminal (LiveFactAttribute); a chave não vai para o relatório. O que o dono precisa ver é o
    // acerto do léxico sozinho, o acerto com o modelo e o que isso custou em tokens: é a régua da decisão de manter o segundo estágio.
    [LiveFact(LiveKeys.OpenAi)]
    public Task Holdout_ao_vivo_com_a_OpenAI() => LiveAsync(ModelProviders.OpenAi.Id, LiveKeys.Read(LiveKeys.OpenAi));

    [LiveFact(LiveKeys.Anthropic)]
    public Task Holdout_ao_vivo_com_a_Anthropic() => LiveAsync(ModelProviders.Anthropic.Id, LiveKeys.Read(LiveKeys.Anthropic));

    private async Task LiveAsync(string provider, string key)
    {
        using var network = new HttpsModelTransport();
        var registry = new CapabilityRegistry();
        var gateway = new ModelGateway(new EnvModelSettings(provider, key), network, timeout: TimeSpan.FromSeconds(60), chat: network);
        var stage = new ModelIntentStage(gateway, registry);

        var without = await BootIntentEvalRunner.RunAsync(BootIntentEvalTests.Holdout, null);
        var watch = System.Diagnostics.Stopwatch.StartNew();
        var with = await BootIntentEvalRunner.RunAsync(BootIntentEvalTests.Holdout, stage);
        watch.Stop();

        var total = without.Cases.Count;
        output.WriteLine($"[eval de intenção ao vivo, {provider}] holdout: sem modelo {without.Hits}/{total} ({(double)without.Hits / total:P0}); " +
            $"com modelo {with.Hits}/{total} ({(double)with.Hits / total:P0}); {with.ModelCalls} chamadas em {watch.Elapsed.TotalSeconds:0} s; " +
            $"{with.InputTokens} tokens de entrada e {with.OutputTokens} de saída");
        foreach (var miss in with.LexiconMisses) output.WriteLine("  léxico errou: " + miss);
        foreach (var change in with.Changes) output.WriteLine("  mudou: " + change);
        var failed = with.Cases.Count(item => item.ModelCalled) - with.Cases.Count(item => item.Final != item.Lexicon);
        output.WriteLine($"[eval de intenção ao vivo, {provider}] o modelo foi chamado em {with.Cases.Count(item => item.ModelCalled)} frases e mudou o resultado em {with.Cases.Count(item => item.Final != item.Lexicon)}; {failed} ficaram como o léxico (sem proposta ou resposta que não serviu).");

        Assert.All(with.Cases.Where(item => item.Lexicon is not ("?" or "-")), item => Assert.Equal(item.Lexicon, item.Final));
        Assert.DoesNotContain(key, string.Join("\n", with.Changes), StringComparison.Ordinal);
    }
}
