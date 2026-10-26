using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using AtendimentoDeCampo.Api.Contratos;
using AtendimentoDeCampo.Domain;

namespace AtendimentoDeCampo.Tests;

/// <summary>
/// O transoperatorio e a recuperacao pos-anestesica.
///
/// Ate aqui a ficha ia do time out direto para o check out: a lista de
/// verificacao era registrada inteira e o procedimento em si — anestesia,
/// posicionamento, incisao, curativo, compressas — nao tinha onde ser descrito.
/// A recuperacao tinha entrada, saida e dois campos de texto; o estado em que o
/// paciente chegou nela, nenhum.
/// </summary>
[Collection(Colecoes.Api)]
public class TransoperatorioTests
{
    private readonly ApiFixture _fixture;

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    public TransoperatorioTests(ApiFixture fixture) => _fixture = fixture;

    private Task<HttpClient> CirurgiaoAsync(string usuario = "transop.teste")
        => _fixture.ClienteDeAsync(usuario, "Cirurgiao Transop", FuncaoProfissional.Cirurgiao, "90001");

    private static async Task<Guid> NaCirurgiaAsync(HttpClient client, string nome)
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
                idadeAproximada = 47,
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
            encaminhamento = "Cirurgia"
        }, Json)).EnsureSuccessStatusCode();

        return prontuario.Id;
    }

    private static async Task<CirurgiaDto> SalvarAsync(HttpClient client, Guid id, object ficha)
    {
        (await client.PutAsJsonAsync($"/api/atendimentos/{id}/cirurgia", ficha, Json))
            .EnsureSuccessStatusCode();

        var prontuario = await client.GetFromJsonAsync<ProntuarioDto>($"/api/atendimentos/{id}", Json);

        Assert.NotNull(prontuario!.Cirurgia);

        return prontuario.Cirurgia!;
    }

    [SkippableFact]
    public async Task O_que_aconteceu_na_sala_volta_inteiro_no_prontuario()
    {
        Skip.IfNot(ApiFixture.BancoDisponivel, "ATENDIMENTO_TEST_DB nao configurado.");

        var client = await CirurgiaoAsync();
        var id = await NaCirurgiaAsync(client, "Paciente Operado Inteiro");

        var ficha = await SalvarAsync(client, id, new
        {
            indicacao = "Hernia inguinal direita.",
            procedimentoProposto = "Herniorrafia inguinal com tela.",
            lateralidade = "Direito",

            // Raqui com sedacao: a tecnica e combinada, e guardar so uma das
            // duas apagaria metade do que foi feito.
            anestesia = new[] { "BloqueioRaqui", "Sedacao" },
            anestesiaLocal = "L3-L4.",
            posicionamento = "DecubitoDorsal",
            placaBisturi = true,
            placaBisturiLocal = "Coxa direita, face lateral.",
            preparoDaPele = "Tricotomia inguinal, pele integra.",
            antissepsia = "Clorexidina alcoolica 0,5%, dois tempos.",
            incisaoCirurgica = "Inguinotomia direita de 6 cm.",
            curativo = "Curativo oclusivo seco.",
            medicacoesRealizadas = "Cefazolina 2 g IV. Dipirona 2 g IV.",
            gazesUtilizadas = 20,
            compressasUtilizadas = 4,
            evolucaoMedica = "Procedimento sem intercorrencias, sangramento minimo.",
            evolucaoEnfermagem = "Paciente estavel durante todo o ato."
        });

        Assert.Equal([TipoAnestesia.BloqueioRaqui, TipoAnestesia.Sedacao], ficha.Anestesia);
        Assert.Equal(PosicionamentoCirurgico.DecubitoDorsal, ficha.Posicionamento);
        Assert.Equal("Coxa direita, face lateral.", ficha.PlacaBisturiLocal);
        Assert.Equal("Inguinotomia direita de 6 cm.", ficha.IncisaoCirurgica);
        Assert.Equal(20, ficha.GazesUtilizadas);
        Assert.Equal(4, ficha.CompressasUtilizadas);
        Assert.Contains("sem intercorrencias", ficha.EvolucaoMedica);
    }

    [SkippableFact]
    public async Task O_estado_em_que_o_paciente_chega_na_recuperacao_e_registravel()
    {
        Skip.IfNot(ApiFixture.BancoDisponivel, "ATENDIMENTO_TEST_DB nao configurado.");

        var client = await CirurgiaoAsync();
        var id = await NaCirurgiaAsync(client, "Paciente Na Recuperacao");

        var ficha = await SalvarAsync(client, id, new
        {
            destinoPosOperatorio = "RecuperacaoAnestesica",
            recuperacaoNivelConsciencia = "Sonolento",
            recuperacaoSuporteOxigenio = "CateterNasal",
            recuperacaoOxigenioDetalhe = "3 l/min",
            recuperacaoAcessoVenosoPeriferico = true,
            recuperacaoAcessoVenosoDescricao = "Jelco 20, antebraco esquerdo.",
            recuperacaoInfusoes = "Ringer lactato 500 mL em 4 h.",
            recuperacaoIntervencaoIntensiva = true,
            recuperacaoCateterVenosoCentral = true,
            recuperacaoCateterVenosoCentralLocal = "Jugular interna direita.",
            recuperacaoDrogasVasoativas = "Noradrenalina 0,1 mcg/kg/min.",
            recuperacaoDreno = true,
            recuperacaoDrenoDescricao = "Penrose em leito operatorio.",
            recuperacaoSondaVesical = true,
            recuperacaoSondaNasogastrica = false,
            recuperacaoEvolucaoMedica = "Estavel, dor controlada.",
            recuperacaoEvolucaoEnfermagem = "SSVV de 2/2 h conforme prescricao."
        });

        Assert.Equal(DestinoPosOperatorio.RecuperacaoAnestesica, ficha.DestinoPosOperatorio);
        Assert.Equal(NivelConsciencia.Sonolento, ficha.RecuperacaoNivelConsciencia);
        Assert.Equal(SuporteOxigenio.CateterNasal, ficha.RecuperacaoSuporteOxigenio);
        Assert.Equal("3 l/min", ficha.RecuperacaoOxigenioDetalhe);
        Assert.True(ficha.RecuperacaoIntervencaoIntensiva);
        Assert.Equal("Jugular interna direita.", ficha.RecuperacaoCateterVenosoCentralLocal);
        Assert.True(ficha.RecuperacaoSondaVesical);
        Assert.False(ficha.RecuperacaoSondaNasogastrica);
    }

    [SkippableFact]
    public async Task Detalhe_sem_a_resposta_que_o_abre_nao_e_gravado()
    {
        Skip.IfNot(ApiFixture.BancoDisponivel, "ATENDIMENTO_TEST_DB nao configurado.");

        var client = await CirurgiaoAsync();
        var id = await NaCirurgiaAsync(client, "Paciente Sem Dispositivos");

        /*
            "Onde foi a placa" sem placa, "qual antibiotico" sem antibiotico e
            "quantos litros" em ar ambiente sao a resposta de uma pergunta que
            nao foi feita. Guardados, viram contradicao no prontuario — e quem
            le depois acredita no detalhe, nao na caixa.
        */
        var ficha = await SalvarAsync(client, id, new
        {
            placaBisturi = false,
            placaBisturiLocal = "Coxa direita.",
            timeOutDoisAntibioticoProfilatico = false,
            timeOutDoisAntibioticoQual = "Cefazolina 2 g.",
            recuperacaoSuporteOxigenio = "ArAmbiente",
            recuperacaoOxigenioDetalhe = "3 l/min",
            recuperacaoAcessoVenosoPeriferico = false,
            recuperacaoAcessoVenosoDescricao = "Jelco 20.",
            recuperacaoDreno = false,
            recuperacaoDrenoDescricao = "Penrose.",
            checkOutAmostrasIdentificadas = false,
            checkOutBiopsiaQuantidade = 3
        });

        Assert.Null(ficha.PlacaBisturiLocal);
        Assert.Null(ficha.TimeOutDoisAntibioticoQual);
        Assert.Null(ficha.RecuperacaoOxigenioDetalhe);
        Assert.Null(ficha.RecuperacaoAcessoVenosoDescricao);
        Assert.Null(ficha.RecuperacaoDrenoDescricao);
        Assert.Null(ficha.CheckOutBiopsiaQuantidade);
    }

    [SkippableFact]
    public async Task As_paradas_so_carimbam_com_as_conferencias_novas()
    {
        Skip.IfNot(ApiFixture.BancoDisponivel, "ATENDIMENTO_TEST_DB nao configurado.");

        var client = await CirurgiaoAsync();
        var id = await NaCirurgiaAsync(client, "Paciente Do Time Out");

        // A lista antiga, inteira. O time out ganhou duas conferencias novas —
        // exames em sala e verificacao anestesica —, e sem elas a parada nao
        // aconteceu.
        var parcial = await SalvarAsync(client, id, new
        {
            timeOutUmEquipeApresentada = true,
            timeOutUmMonitorizacaoOk = true,
            timeOutUmViaAereaAvaliada = true,
            timeOutUmRiscoSangramentoAvaliado = true
        });

        Assert.Null(parcial.TimeOutUmEm);

        var completo = await SalvarAsync(client, id, new
        {
            timeOutUmEquipeApresentada = true,
            timeOutUmMonitorizacaoOk = true,
            timeOutUmViaAereaAvaliada = true,
            timeOutUmRiscoSangramentoAvaliado = true,
            timeOutUmExamesEmSala = true,
            timeOutUmVerificacaoAnestesica = true
        });

        Assert.NotNull(completo.TimeOutUmEm);
    }

    [SkippableFact]
    public async Task Achado_nao_e_conferencia_e_nao_trava_a_parada()
    {
        Skip.IfNot(ApiFixture.BancoDisponivel, "ATENDIMENTO_TEST_DB nao configurado.");

        var client = await CirurgiaoAsync();
        var id = await NaCirurgiaAsync(client, "Paciente Com Via Aerea Normal");

        /*
            "Via aerea dificil" e "previsao de biopsia" sao respostas, e nao
            itens de conferencia. Exigi-las para carimbar obrigaria a equipe a
            declarar uma via aerea dificil que o paciente nao tem — e a maioria
            das cirurgias nao tem biopsia prevista.
        */
        var ficha = await SalvarAsync(client, id, new
        {
            timeOutUmEquipeApresentada = true,
            timeOutUmMonitorizacaoOk = true,
            timeOutUmViaAereaAvaliada = true,
            timeOutUmRiscoSangramentoAvaliado = true,
            timeOutUmExamesEmSala = true,
            timeOutUmVerificacaoAnestesica = true,
            timeOutUmViaAereaDificil = false,

            timeOutDoisPacienteSitioProcedimentoConfirmados = true,
            timeOutDoisAntibioticoProfilatico = true,
            timeOutDoisAntibioticoQual = "Cefazolina 2 g, 30 min antes.",
            timeOutDoisImagensDisponiveis = true,
            timeOutDoisEventosCriticosRevistos = true,
            timeOutDoisMaterialEsterilizado = true,
            timeOutDoisInstrumentalIntegrador = true,
            timeOutDoisPrevisaoBiopsia = false,
            timeOutDoisTempoPrevistoMinutos = 90
        });

        Assert.NotNull(ficha.TimeOutUmEm);
        Assert.NotNull(ficha.TimeOutDoisEm);
        Assert.False(ficha.TimeOutUmViaAereaDificil);
        Assert.Equal("Cefazolina 2 g, 30 min antes.", ficha.TimeOutDoisAntibioticoQual);
        Assert.Equal(90, ficha.TimeOutDoisTempoPrevistoMinutos);
    }

    [SkippableFact]
    public async Task A_chegada_ao_centro_e_o_par_da_saida_da_enfermaria()
    {
        Skip.IfNot(ApiFixture.BancoDisponivel, "ATENDIMENTO_TEST_DB nao configurado.");

        var client = await CirurgiaoAsync();
        var id = await NaCirurgiaAsync(client, "Paciente Que Esperou No Corredor");

        var saiu = DateTime.UtcNow.AddHours(-2);
        var chegou = DateTime.UtcNow.AddHours(-1);

        var ficha = await SalvarAsync(client, id, new
        {
            encaminhadoAoCentroEm = saiu,
            chegadaAoCentroEm = chegou
        });

        // A distancia entre as duas e tempo de paciente em jejum num corredor,
        // e e a unica forma de ver que houve espera.
        Assert.NotNull(ficha.EncaminhadoAoCentroEm);
        Assert.NotNull(ficha.ChegadaAoCentroEm);
        Assert.True(ficha.ChegadaAoCentroEm > ficha.EncaminhadoAoCentroEm);
    }

    [SkippableFact]
    public async Task O_transoperatorio_e_salvo_sem_fechar_a_fila()
    {
        Skip.IfNot(ApiFixture.BancoDisponivel, "ATENDIMENTO_TEST_DB nao configurado.");

        var client = await CirurgiaoAsync();
        var id = await NaCirurgiaAsync(client, "Paciente Em Cirurgia");

        await SalvarAsync(client, id, new
        {
            incisaoCirurgica = "Inguinotomia direita.",
            gazesUtilizadas = 20
        });

        var prontuario = await client.GetFromJsonAsync<ProntuarioDto>($"/api/atendimentos/{id}", Json);
        var etapa = prontuario!.Etapas.Single(e => e.Especialidade == Especialidade.Cirurgia);

        // A ficha e preenchida ao longo do ato. Fechar a fila a cada salvamento
        // tiraria o paciente da fila de quem esta operando.
        Assert.Equal(StatusEtapa.EmAndamento, etapa.Status);
        Assert.Null(prontuario.Cirurgia!.ConcluidaEm);
    }
}
