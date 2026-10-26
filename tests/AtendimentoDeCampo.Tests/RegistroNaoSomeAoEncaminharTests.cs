using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using AtendimentoDeCampo.Api.Contratos;
using AtendimentoDeCampo.Domain;

namespace AtendimentoDeCampo.Tests;

/// <summary>
/// Encaminhar depois de atender conclui a etapa; encaminhar sem atender a
/// cancela. As duas coisas na mesma rota, e a diferenca e existir ficha.
///
/// CORRIGE: a checagem lia as navegacoes da etapa (<c>etapa.Consulta</c> e
/// companhia), que a consulta do atendimento nao carrega e que sem lazy loading
/// nunca chegam preenchidas — vinha falso sempre. E a lista olhava quatro
/// fichas, de quando so existiam quatro.
///
/// Com a ficha cirurgica isso deixou de ser teorico: ela e a unica que fica
/// aberta entre as quatro paradas da lista de verificacao, entao o cirurgiao
/// que operava e mandava o paciente para a enfermagem cuidar do pos-operatorio
/// tinha a cirurgia marcada como CANCELADA e a passagem fechada sem
/// profissional. A operacao inteira saia da producao, e o prontuario registrava
/// que a fila da cirurgia havia sido cancelada.
/// </summary>
[Collection(Colecoes.Api)]
public class RegistroNaoSomeAoEncaminharTests
{
    private readonly ApiFixture _fixture;

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    public RegistroNaoSomeAoEncaminharTests(ApiFixture fixture) => _fixture = fixture;

    private Task<HttpClient> CirurgiaoAsync()
        => _fixture.ClienteDeAsync(
            "encaminha.cirurgia", "Cirurgiao Que Encaminha", FuncaoProfissional.Coordenacao);

    private static async Task<ProntuarioDto> AbrirNaFilaAsync(
        HttpClient client,
        string nome,
        Especialidade fila)
    {
        var bases = await client.GetFromJsonAsync<List<BaseDto>>("/api/bases", Json);
        var codigo = (await client.GetFromJsonAsync<CodigoNovoDto>("/api/pacientes/codigo-novo", Json))!.Codigo;

        var criado = await client.PostAsJsonAsync("/api/atendimentos", new
        {
            baseId = bases![0].Id,
            paciente = new
            {
                codigo,
                nome,
                tipoDocumento = "SemDocumento",
                idadeAproximada = 48,
                sexo = "NaoInformado",
                statusAlergia = "NaoPerguntado",
                consentimentoRegistro = true
            }
        }, Json);

        criado.EnsureSuccessStatusCode();

        var prontuario = (await criado.Content.ReadFromJsonAsync<ProntuarioDto>(Json))!;

        (await client.PutAsJsonAsync($"/api/atendimentos/{prontuario.Id}/triagem", new
        {
            classificacaoRisco = "Verde",
            statusAlergia = "SemAlergiaConhecida",
            encaminhamento = fila.ToString()
        }, Json)).EnsureSuccessStatusCode();

        (await client.PostAsync(
            $"/api/atendimentos/{prontuario.Id}/etapas/{fila}/assumir", null)).EnsureSuccessStatusCode();

        return prontuario;
    }

    [SkippableFact]
    public async Task A_cirurgia_operada_nao_vira_fila_cancelada_ao_mandar_para_a_enfermagem()
    {
        Skip.IfNot(ApiFixture.BancoDisponivel, "ATENDIMENTO_TEST_DB nao configurado.");

        var client = await CirurgiaoAsync();
        var atendimento = await AbrirNaFilaAsync(client, "Paciente Operado", Especialidade.Cirurgia);

        // A ficha cirurgica inteira, sem desfecho: e assim que ela fica entre
        // as paradas da lista de verificacao, e e nesse estado que o paciente
        // vai para a enfermagem.
        (await client.PutAsJsonAsync($"/api/atendimentos/{atendimento.Id}/cirurgia", new
        {
            indicacao = "Hernia inguinal direita.",
            procedimentoProposto = "Herniorrafia inguinal com tela.",
            lateralidade = "Direito",
            checkInIdentidadeConfirmada = true,
            checkInSitioMarcado = true,
            checkInConsentimentoConferido = true,
            checkInAlergiaConferida = true,
            checkInJejumConferido = true,
            timeOutUmEquipeApresentada = true,
            timeOutDoisAntibioticoProfilatico = true,
            checkOutProcedimentoRegistrado = true,
            checkOutContagemConfere = true
        }, Json)).EnsureSuccessStatusCode();

        (await client.PostAsJsonAsync(
            $"/api/atendimentos/{atendimento.Id}/etapas/Cirurgia/encaminhar",
            new { destino = "Enfermagem", motivo = "Cuidados de pos-operatorio." },
            Json)).EnsureSuccessStatusCode();

        var depois = await client.GetFromJsonAsync<ProntuarioDto>(
            $"/api/atendimentos/{atendimento.Id}", Json);

        var cirurgia = depois!.Etapas.Single(e => e.Especialidade == Especialidade.Cirurgia);

        Assert.Equal(StatusEtapa.Concluida, cirurgia.Status);

        // E a passagem fica com quem operou: sem isso a cirurgia desaparece da
        // producao, que e justamente o numero que decide escala de plantao.
        var passagem = depois.TempoNasFilas.Single(p => p.Especialidade == Especialidade.Cirurgia);

        Assert.NotNull(passagem.SaiuEm);
        Assert.NotNull(depois.Cirurgia);
        Assert.Equal("Herniorrafia inguinal com tela.", depois.Cirurgia!.ProcedimentoProposto);
    }

    [SkippableFact]
    public async Task Fila_sem_ficha_nenhuma_continua_sendo_cancelada()
    {
        Skip.IfNot(ApiFixture.BancoDisponivel, "ATENDIMENTO_TEST_DB nao configurado.");

        var client = await CirurgiaoAsync();
        var atendimento = await AbrirNaFilaAsync(client, "Paciente So De Passagem", Especialidade.Cirurgia);

        // Nada registrado: o paciente nunca foi atendido ali, e marcar como
        // concluida inflaria a producao da cirurgia com uma operacao que nao
        // aconteceu.
        (await client.PostAsJsonAsync(
            $"/api/atendimentos/{atendimento.Id}/etapas/Cirurgia/encaminhar",
            new { destino = "ClinicaGeral", motivo = "Nao tem indicacao cirurgica." },
            Json)).EnsureSuccessStatusCode();

        var depois = await client.GetFromJsonAsync<ProntuarioDto>(
            $"/api/atendimentos/{atendimento.Id}", Json);

        Assert.Equal(
            StatusEtapa.Cancelada,
            depois!.Etapas.Single(e => e.Especialidade == Especialidade.Cirurgia).Status);
    }
}
