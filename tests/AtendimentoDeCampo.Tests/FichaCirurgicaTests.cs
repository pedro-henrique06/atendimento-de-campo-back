using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using AtendimentoDeCampo.Api.Contratos;
using AtendimentoDeCampo.Domain;

namespace AtendimentoDeCampo.Tests;

/// <summary>
/// A ficha cirurgica: pre-operatorio, as quatro paradas da lista de verificacao
/// e a recuperacao.
///
/// A ficha e preenchida em momentos diferentes — antes de entrar na sala, antes
/// da anestesia, antes da incisao e antes de sair —, e o que se testa aqui e
/// justamente isso: salvar uma parada nao pode fechar a fila, e a hora de cada
/// parada nao pode ser reescrita.
/// </summary>
[Collection(Colecoes.Api)]
public class FichaCirurgicaTests
{
    private readonly ApiFixture _fixture;

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    public FichaCirurgicaTests(ApiFixture fixture) => _fixture = fixture;

    private Task<HttpClient> CirurgiaoAsync(string usuario)
        => _fixture.ClienteDeAsync(usuario, $"Cirurgia {usuario}", FuncaoProfissional.Cirurgiao, "70701");

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
                idadeAproximada = 44,
                sexo = "NaoInformado",
                statusAlergia = "NaoPerguntado",
                consentimentoRegistro = true
            }
        }, Json);

        resposta.EnsureSuccessStatusCode();
        var prontuario = (await resposta.Content.ReadFromJsonAsync<ProntuarioDto>(Json))!;

        (await client.PutAsJsonAsync($"/api/atendimentos/{prontuario.Id}/triagem", new
        {
            classificacaoRisco = "Amarelo",
            statusAlergia = "SemAlergiaConhecida",
            encaminhamento = "Cirurgia"
        }, Json)).EnsureSuccessStatusCode();

        return prontuario;
    }

    private static Task<HttpResponseMessage> SalvarAsync(HttpClient client, Guid id, object ficha)
        => client.PutAsJsonAsync($"/api/atendimentos/{id}/cirurgia", ficha, Json);

    private static async Task<ProntuarioDto> LerAsync(HttpClient client, Guid id)
        => (await client.GetFromJsonAsync<ProntuarioDto>($"/api/atendimentos/{id}", Json))!;

    /// <summary>O check-in inteiro, que e o que faz a parada ser carimbada.</summary>
    private static object CheckInCompleto => new
    {
        indicacao = "Hernia inguinal direita encarcerada.",
        procedimentoProposto = "Herniorrafia inguinal",
        lateralidade = "Direito",
        jejumHoras = 8,
        consentimentoAssinado = true,
        checkInIdentidadeConfirmada = true,
        checkInSitioMarcado = true,
        checkInConsentimentoConferido = true,
        checkInAlergiaConferida = true,
        checkInJejumConferido = true
    };

    [SkippableFact]
    public async Task Salvar_uma_parada_nao_fecha_a_fila()
    {
        Skip.IfNot(ApiFixture.BancoDisponivel, "ATENDIMENTO_TEST_DB nao configurado.");

        var client = await CirurgiaoAsync("cirurgia.parcial");
        var atendimento = await AbrirAsync(client, "Paciente Da Hernia");

        (await SalvarAsync(client, atendimento.Id, CheckInCompleto)).EnsureSuccessStatusCode();

        var prontuario = await LerAsync(client, atendimento.Id);
        var etapa = prontuario.Etapas.Single(e => e.Especialidade == Especialidade.Cirurgia);

        // A ficha e preenchida em quatro momentos. Se salvar o check-in fechasse
        // a fila, o cirurgiao perderia o paciente da propria lista e teria de
        // reabrir a etapa para continuar.
        Assert.NotEqual(StatusEtapa.Concluida, etapa.Status);
        Assert.NotNull(prontuario.Cirurgia);
        Assert.Equal("Herniorrafia inguinal", prontuario.Cirurgia!.ProcedimentoProposto);
        Assert.Equal(Lateralidade.Direito, prontuario.Cirurgia.Lateralidade);
    }

    [SkippableFact]
    public async Task A_parada_e_carimbada_quando_fica_completa()
    {
        Skip.IfNot(ApiFixture.BancoDisponivel, "ATENDIMENTO_TEST_DB nao configurado.");

        var client = await CirurgiaoAsync("cirurgia.carimbo");
        var atendimento = await AbrirAsync(client, "Paciente Do Carimbo");

        // Parada incompleta nao carimba: uma lista pela metade nao e uma parada.
        (await SalvarAsync(client, atendimento.Id, new
        {
            checkInIdentidadeConfirmada = true,
            checkInSitioMarcado = true
        })).EnsureSuccessStatusCode();

        var meio = await LerAsync(client, atendimento.Id);
        Assert.Null(meio.Cirurgia!.CheckInEm);

        (await SalvarAsync(client, atendimento.Id, CheckInCompleto)).EnsureSuccessStatusCode();

        var completo = await LerAsync(client, atendimento.Id);
        Assert.NotNull(completo.Cirurgia!.CheckInEm);
    }

    [SkippableFact]
    public async Task O_carimbo_da_parada_nao_e_reescrito_nos_salvamentos_seguintes()
    {
        Skip.IfNot(ApiFixture.BancoDisponivel, "ATENDIMENTO_TEST_DB nao configurado.");

        var client = await CirurgiaoAsync("cirurgia.hora");
        var atendimento = await AbrirAsync(client, "Paciente Da Hora");

        (await SalvarAsync(client, atendimento.Id, CheckInCompleto)).EnsureSuccessStatusCode();

        var primeiro = (await LerAsync(client, atendimento.Id)).Cirurgia!.CheckInEm;

        await Task.Delay(1100);

        (await SalvarAsync(client, atendimento.Id, new
        {
            checkInIdentidadeConfirmada = true,
            checkInSitioMarcado = true,
            checkInConsentimentoConferido = true,
            checkInAlergiaConferida = true,
            checkInJejumConferido = true,
            timeOutUmEquipeApresentada = true,
            timeOutUmMonitorizacaoOk = true,
            timeOutUmViaAereaAvaliada = true,
            timeOutUmRiscoSangramentoAvaliado = true
        })).EnsureSuccessStatusCode();

        var depois = (await LerAsync(client, atendimento.Id)).Cirurgia!;

        // A hora do check-in e a do check-in. Reescrita a cada salvamento, as
        // quatro paradas terminariam marcando o mesmo minuto — que e exatamente
        // a evidencia de lista preenchida de uma vez no fim.
        Assert.Equal(primeiro, depois.CheckInEm);
        Assert.NotNull(depois.TimeOutUmEm);
        Assert.True(depois.TimeOutUmEm > depois.CheckInEm);
    }

    [SkippableFact]
    public async Task Problema_com_equipamento_nao_e_exigido_para_fechar_o_check_out()
    {
        Skip.IfNot(ApiFixture.BancoDisponivel, "ATENDIMENTO_TEST_DB nao configurado.");

        var client = await CirurgiaoAsync("cirurgia.equipamento");
        var atendimento = await AbrirAsync(client, "Paciente Sem Intercorrencia");

        // Marcado significa que houve problema. Exigi-lo para dar a parada por
        // concluida obrigaria a equipe a relatar um problema que nao teve.
        (await SalvarAsync(client, atendimento.Id, new
        {
            checkOutProcedimentoRegistrado = true,
            checkOutContagemConfere = true,
            checkOutAmostrasIdentificadas = true,
            checkOutProblemasComEquipamento = false
        })).EnsureSuccessStatusCode();

        var prontuario = await LerAsync(client, atendimento.Id);

        Assert.NotNull(prontuario.Cirurgia!.CheckOutEm);
    }

    [SkippableFact]
    public async Task Com_desfecho_a_ficha_fecha_a_fila()
    {
        Skip.IfNot(ApiFixture.BancoDisponivel, "ATENDIMENTO_TEST_DB nao configurado.");

        var client = await CirurgiaoAsync("cirurgia.fecha");
        var atendimento = await AbrirAsync(client, "Paciente Operado");

        var entrada = DateTime.UtcNow.AddHours(-2);

        (await SalvarAsync(client, atendimento.Id, new
        {
            indicacao = "Hernia inguinal direita.",
            procedimentoProposto = "Herniorrafia inguinal",
            lateralidade = "Direito",
            recuperacaoEntradaEm = entrada,
            recuperacaoSaidaEm = entrada.AddHours(1),
            intercorrencias = "Nenhuma.",
            observacoesRecuperacao = "Acordado, sem dor, deambulando.",
            desfecho = "Alta"
        })).EnsureSuccessStatusCode();

        var prontuario = await LerAsync(client, atendimento.Id);
        var etapa = prontuario.Etapas.Single(e => e.Especialidade == Especialidade.Cirurgia);

        Assert.Equal(StatusEtapa.Concluida, etapa.Status);
        Assert.Equal("Nenhuma.", prontuario.Cirurgia!.Intercorrencias);
        Assert.NotNull(prontuario.Cirurgia.RecuperacaoSaidaEm);
    }

    [SkippableFact]
    public async Task A_cirurgia_nao_e_registrada_como_consulta()
    {
        Skip.IfNot(ApiFixture.BancoDisponivel, "ATENDIMENTO_TEST_DB nao configurado.");

        var client = await CirurgiaoAsync("cirurgia.rota");
        var atendimento = await AbrirAsync(client, "Paciente Da Rota Errada");

        // Ate a etapa anterior a cirurgia usava a ficha de consulta. Com ficha
        // propria, deixar as duas portas abertas gravaria duas fichas na mesma
        // fila e o prontuario mostraria so uma.
        var resposta = await client.PutAsJsonAsync($"/api/atendimentos/{atendimento.Id}/consulta", new
        {
            especialidade = "Cirurgia",
            sintomasDescricao = "Nao deveria entrar por aqui."
        }, Json);

        Assert.Equal(HttpStatusCode.BadRequest, resposta.StatusCode);
    }
}
