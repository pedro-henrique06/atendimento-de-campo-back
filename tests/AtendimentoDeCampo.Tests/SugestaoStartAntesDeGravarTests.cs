using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using AtendimentoDeCampo.Api.Contratos;
using AtendimentoDeCampo.Domain;

namespace AtendimentoDeCampo.Tests;

/// <summary>
/// A sugestao do START antes da decisao, e nao depois dela.
///
/// Ela so existia como efeito de gravar a triagem: a tela mostrava o que o
/// protocolo diria *depois* que o profissional ja tinha escolhido a cor e
/// apertado salvar. Ou a sugestao chega a tempo de ajudar, ou vira aviso que se
/// aprende a fechar.
///
/// O endpoint nao grava nada e nao decide nada: quem classifica continua sendo
/// quem esta com o paciente, e a divergencia continua sendo julgada e registrada
/// na hora de gravar a triagem.
/// </summary>
[Collection(Colecoes.Api)]
public class SugestaoStartAntesDeGravarTests
{
    private readonly ApiFixture _fixture;

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    public SugestaoStartAntesDeGravarTests(ApiFixture fixture) => _fixture = fixture;

    private Task<HttpClient> EnfermeiroAsync()
        => _fixture.ClienteDeAsync("start.teste", "Start Teste", FuncaoProfissional.Coordenacao);

    private static async Task<SugestaoStartDto> AvaliarAsync(HttpClient client, object achados)
    {
        var resposta = await client.PostAsJsonAsync("/api/protocolo-start", achados, Json);
        resposta.EnsureSuccessStatusCode();

        return (await resposta.Content.ReadFromJsonAsync<SugestaoStartDto>(Json))!;
    }

    [SkippableFact]
    public async Task Avalia_sem_gravar_nada()
    {
        Skip.IfNot(ApiFixture.BancoDisponivel, "ATENDIMENTO_TEST_DB nao configurado.");

        var client = await EnfermeiroAsync();

        var sugestao = await AvaliarAsync(client, new
        {
            deambula = true,
            respiraEspontaneamente = true,
            pulsoRadialPresente = true,
            obedeceComandos = true
        });

        Assert.Equal(ClassificacaoRisco.Verde, sugestao.Sugerida);
        Assert.Contains("deambula", sugestao.Motivo, StringComparison.OrdinalIgnoreCase);

        // Nao ha com que divergir: ninguem escolheu classificacao ainda. Quem
        // julga divergencia e a gravacao da triagem.
        Assert.False(sugestao.Divergente);
    }

    [SkippableFact]
    public async Task E_a_mesma_implementacao_que_julga_a_divergencia_ao_gravar()
    {
        Skip.IfNot(ApiFixture.BancoDisponivel, "ATENDIMENTO_TEST_DB nao configurado.");

        var client = await EnfermeiroAsync();

        // Nao deambula, respira sozinho, mas a frequencia respiratoria esta
        // acima do corte: o START manda para o vermelho.
        var achados = new
        {
            deambula = false,
            respiraEspontaneamente = true,
            frequenciaRespiratoria = 34,
            pulsoRadialPresente = true,
            obedeceComandos = true
        };

        var antes = await AvaliarAsync(client, achados);
        Assert.Equal(ClassificacaoRisco.Vermelho, antes.Sugerida);

        var bases = await client.GetFromJsonAsync<List<BaseDto>>("/api/bases", Json);
        var codigo = (await client.GetFromJsonAsync<CodigoNovoDto>("/api/pacientes/codigo-novo", Json))!.Codigo;

        var criado = await client.PostAsJsonAsync("/api/atendimentos", new
        {
            baseId = bases![0].Id,
            paciente = new
            {
                codigo,
                nome = "Paciente Do Start",
                tipoDocumento = "SemDocumento",
                idadeAproximada = 30,
                sexo = "NaoInformado",
                statusAlergia = "NaoPerguntado",
                consentimentoRegistro = true
            }
        }, Json);

        criado.EnsureSuccessStatusCode();
        var atendimento = (await criado.Content.ReadFromJsonAsync<ProntuarioDto>(Json))!;

        // O profissional vê a sugestão e decide outra coisa, que é o direito
        // dele: a classificação gravada é sempre a escolhida.
        var gravada = await client.PutAsJsonAsync($"/api/atendimentos/{atendimento.Id}/triagem", new
        {
            sintomas = Array.Empty<string>(),
            statusAlergia = "NaoPerguntado",
            classificacaoRisco = "Amarelo",
            achadosStart = achados
        }, Json);

        gravada.EnsureSuccessStatusCode();
        var depois = (await gravada.Content.ReadFromJsonAsync<SugestaoStartDto>(Json))!;

        // A mesma cor sugerida nos dois momentos. Se a tela usasse uma cópia do
        // algoritmo, seria aqui que as duas passariam a discordar — a da tela e
        // a que a auditoria registra.
        Assert.Equal(antes.Sugerida, depois.Sugerida);
        Assert.Equal(antes.Motivo, depois.Motivo);

        // E agora sim há divergência: sugeriu vermelho, o profissional escolheu
        // amarelo.
        Assert.True(depois.Divergente);
    }

    [SkippableFact]
    public async Task Exige_sessao()
    {
        Skip.IfNot(ApiFixture.BancoDisponivel, "ATENDIMENTO_TEST_DB nao configurado.");

        // São achados clínicos de um paciente: não é endpoint aberto.
        var anonimo = _fixture.CreateClient();

        var resposta = await anonimo.PostAsJsonAsync("/api/protocolo-start", new { deambula = true }, Json);

        Assert.Equal(HttpStatusCode.Unauthorized, resposta.StatusCode);
    }
}
