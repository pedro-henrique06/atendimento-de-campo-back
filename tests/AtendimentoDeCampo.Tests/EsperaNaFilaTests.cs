using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using AtendimentoDeCampo.Api.Contratos;
using AtendimentoDeCampo.Domain;

namespace AtendimentoDeCampo.Tests;

/// <summary>
/// Desde quando o paciente espera nesta fila.
///
/// A lista dizia ha quanto tempo alguem estava *sendo atendido* — o cronometro
/// conta de <c>AssumidaEm</c> —, e nao dizia ha quanto tempo os outros estavam
/// esperando. Numa fila de vinte pessoas, esse e o numero que decide quem passa
/// na frente junto da cor do risco, e ele nao existia em lugar nenhum da tela.
///
/// O dado ja estava gravado na passagem pela fila desde sempre. So faltava sair
/// do servidor.
/// </summary>
[Collection(Colecoes.Api)]
public class EsperaNaFilaTests
{
    private readonly ApiFixture _fixture;

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    public EsperaNaFilaTests(ApiFixture fixture) => _fixture = fixture;

    private Task<HttpClient> ProfissionalAsync()
        => _fixture.ClienteDeAsync("espera.teste", "Espera Teste", FuncaoProfissional.Coordenacao);

    private static async Task<ProntuarioDto> AbrirAsync(HttpClient client, string nome)
    {
        var bases = await client.GetFromJsonAsync<List<BaseDto>>("/api/bases", Json);
        var codigo = (await client.GetFromJsonAsync<CodigoNovoDto>("/api/pacientes/codigo-novo", Json))!.Codigo;

        var resposta = await client.PostAsJsonAsync("/api/atendimentos", new
        {
            baseId = bases![0].Id,
            paciente = new
            {
                codigo,
                nome,
                tipoDocumento = "SemDocumento",
                idadeAproximada = 35,
                sexo = "NaoInformado",
                statusAlergia = "NaoPerguntado",
                consentimentoRegistro = true
            }
        }, Json);

        resposta.EnsureSuccessStatusCode();
        return (await resposta.Content.ReadFromJsonAsync<ProntuarioDto>(Json))!;
    }

    private static async Task<List<EtapaResumoDto>> EtapasAsync(HttpClient client, Guid id)
    {
        var bases = await client.GetFromJsonAsync<List<BaseDto>>("/api/bases", Json);
        var lista = await client.GetFromJsonAsync<List<AtendimentoResumoDto>>(
            $"/api/atendimentos?baseId={bases![0].Id}", Json);

        return lista!.Single(a => a.Id == id).Etapas;
    }

    [SkippableFact]
    public async Task A_fila_diz_desde_quando_o_paciente_espera()
    {
        Skip.IfNot(ApiFixture.BancoDisponivel, "ATENDIMENTO_TEST_DB nao configurado.");

        var client = await ProfissionalAsync();
        var antes = DateTime.UtcNow.AddSeconds(-5);

        var atendimento = await AbrirAsync(client, "Paciente Que Espera");

        var triagem = (await EtapasAsync(client, atendimento.Id))
            .Single(e => e.Especialidade == Especialidade.Triagem);

        Assert.NotNull(triagem.EntrouNaFilaEm);
        Assert.InRange(triagem.EntrouNaFilaEm!.Value, antes, DateTime.UtcNow.AddSeconds(5));

        // Entrar na fila nao e ser atendido: o cronometro do atendimento so
        // comeca quando alguem assume.
        Assert.Null(triagem.AssumidaEm);
    }

    [SkippableFact]
    public async Task Encaminhar_reinicia_a_espera_na_fila_de_destino()
    {
        Skip.IfNot(ApiFixture.BancoDisponivel, "ATENDIMENTO_TEST_DB nao configurado.");

        var client = await ProfissionalAsync();
        var atendimento = await AbrirAsync(client, "Paciente Encaminhado");

        var naTriagem = (await EtapasAsync(client, atendimento.Id))
            .Single(e => e.Especialidade == Especialidade.Triagem);

        (await client.PostAsJsonAsync(
            $"/api/atendimentos/{atendimento.Id}/etapas/Triagem/encaminhar",
            new { destino = "ClinicaGeral", motivo = "Dor abdominal ha dois dias." },
            Json)).EnsureSuccessStatusCode();

        var naClinica = (await EtapasAsync(client, atendimento.Id))
            .Single(e => e.Especialidade == Especialidade.ClinicaGeral);

        // A espera na clinica geral comeca no encaminhamento, e nao na chegada
        // ao posto: quem foi encaminhado agora e o ultimo a chegar nesta fila,
        // ainda que ja esteja no posto ha duas horas.
        Assert.NotNull(naClinica.EntrouNaFilaEm);
        Assert.True(naClinica.EntrouNaFilaEm >= naTriagem.EntrouNaFilaEm);
    }

    [SkippableFact]
    public async Task Assumir_nao_apaga_desde_quando_o_paciente_esperou()
    {
        Skip.IfNot(ApiFixture.BancoDisponivel, "ATENDIMENTO_TEST_DB nao configurado.");

        var client = await ProfissionalAsync();
        var atendimento = await AbrirAsync(client, "Paciente Assumido");

        var antes = (await EtapasAsync(client, atendimento.Id))
            .Single(e => e.Especialidade == Especialidade.Triagem);

        (await client.PostAsJsonAsync(
            $"/api/atendimentos/{atendimento.Id}/etapas/Triagem/assumir",
            new { },
            Json)).EnsureSuccessStatusCode();

        var depois = (await EtapasAsync(client, atendimento.Id))
            .Single(e => e.Especialidade == Especialidade.Triagem);

        // Os dois convivem: quanto esperou e ha quanto tempo esta sendo
        // atendido sao perguntas diferentes, e a segunda nao apaga a primeira —
        // e dela que sai quanto tempo a fila fez a pessoa esperar.
        Assert.Equal(antes.EntrouNaFilaEm, depois.EntrouNaFilaEm);
        Assert.NotNull(depois.AssumidaEm);
    }
}
