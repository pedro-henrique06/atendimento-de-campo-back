using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using AtendimentoDeCampo.Api.Contratos;
using AtendimentoDeCampo.Domain;

namespace AtendimentoDeCampo.Tests;

/// <summary>
/// Os dois desfechos que o profissional tem na mao: encerrar dando alta, ou
/// devolver o paciente para quem o encaminhou.
///
/// E o cronometro que acompanha os dois: o tempo conta a partir de quem assumiu,
/// e sobrevive ao paciente voltar para uma fila pela qual ja passou.
/// </summary>
[Collection(Colecoes.Api)]
public class AltaEDevolucaoTests
{
    private readonly ApiFixture _fixture;

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    public AltaEDevolucaoTests(ApiFixture fixture) => _fixture = fixture;

    /// <summary>
    /// Coordenacao ve todas as filas: o que se testa aqui e a alta e a
    /// devolucao, nao a restricao por profissao.
    /// </summary>
    private Task<HttpClient> ProfissionalAsync(string usuario)
        => _fixture.ClienteDeAsync(usuario, $"Alta {usuario}", FuncaoProfissional.Coordenacao);

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

    private static async Task TriarParaAsync(HttpClient client, Guid id, Especialidade destino)
    {
        (await client.PutAsJsonAsync($"/api/atendimentos/{id}/triagem", new
        {
            classificacaoRisco = "Verde",
            statusAlergia = "SemAlergiaConhecida",
            encaminhamento = destino.ToString()
        }, Json)).EnsureSuccessStatusCode();
    }

    private static Task<HttpResponseMessage> EncaminharAsync(
        HttpClient client, Guid id, Especialidade origem, Especialidade destino, string motivo)
        => client.PostAsJsonAsync(
            $"/api/atendimentos/{id}/etapas/{origem}/encaminhar",
            new { destino = destino.ToString(), motivo },
            Json);

    private static Task<HttpResponseMessage> DarAltaAsync(
        HttpClient client, Guid id, Especialidade fila, bool cancelarPendentes = false)
        => client.PostAsJsonAsync(
            $"/api/atendimentos/{id}/etapas/{fila}/alta",
            new { cancelarPendentes },
            Json);

    // -----------------------------------------------------------------------
    // Cronometro
    // -----------------------------------------------------------------------

    [SkippableFact]
    public async Task Assumir_marca_a_hora_em_que_o_atendimento_comecou()
    {
        Skip.IfNot(ApiFixture.BancoDisponivel, "ATENDIMENTO_TEST_DB nao configurado.");

        var client = await ProfissionalAsync("cron.assume");
        var atendimento = await AbrirAsync(client, "Paciente Com Cronometro");

        var antes = (await EtapasAsync(client, atendimento.Id))
            .Single(e => e.Especialidade == Especialidade.Triagem);

        Assert.Null(antes.AssumidaEm);

        (await client.PostAsync(
            $"/api/atendimentos/{atendimento.Id}/etapas/Triagem/assumir", null))
            .EnsureSuccessStatusCode();

        var depois = (await EtapasAsync(client, atendimento.Id))
            .Single(e => e.Especialidade == Especialidade.Triagem);

        // Sem esta hora a tela nao tem de onde comecar a contar.
        Assert.NotNull(depois.AssumidaEm);
    }

    [SkippableFact]
    public async Task Devolver_para_a_fila_zera_o_cronometro()
    {
        Skip.IfNot(ApiFixture.BancoDisponivel, "ATENDIMENTO_TEST_DB nao configurado.");

        var client = await ProfissionalAsync("cron.libera");
        var atendimento = await AbrirAsync(client, "Paciente Devolvido A Fila");

        (await client.PostAsync(
            $"/api/atendimentos/{atendimento.Id}/etapas/Triagem/assumir", null))
            .EnsureSuccessStatusCode();

        (await client.PostAsync(
            $"/api/atendimentos/{atendimento.Id}/etapas/Triagem/liberar", null))
            .EnsureSuccessStatusCode();

        var etapa = (await EtapasAsync(client, atendimento.Id))
            .Single(e => e.Especialidade == Especialidade.Triagem);

        // Quem devolveu para a fila nao atendeu: deixar o cronometro correndo
        // contaria como producao dele o tempo em que o paciente esperou outro.
        Assert.Null(etapa.AssumidaEm);
    }

    // -----------------------------------------------------------------------
    // Devolucao para quem encaminhou
    // -----------------------------------------------------------------------

    [SkippableFact]
    public async Task A_fila_de_destino_sabe_quem_encaminhou_e_de_onde()
    {
        Skip.IfNot(ApiFixture.BancoDisponivel, "ATENDIMENTO_TEST_DB nao configurado.");

        var client = await ProfissionalAsync("dev.origem");
        var atendimento = await AbrirAsync(client, "Paciente Encaminhado A Pediatria");

        await TriarParaAsync(client, atendimento.Id, Especialidade.ClinicaGeral);

        (await EncaminharAsync(
            client, atendimento.Id, Especialidade.ClinicaGeral, Especialidade.Pediatria,
            "Crianca, caso de pediatria.")).EnsureSuccessStatusCode();

        var pediatria = (await EtapasAsync(client, atendimento.Id))
            .Single(e => e.Especialidade == Especialidade.Pediatria);

        // Sem isto, a pediatra teria de lembrar de onde o paciente veio — e
        // erraria nas vezes em que ele ja passou por tres filas.
        Assert.Equal(Especialidade.ClinicaGeral, pediatria.EncaminhadaDe);
        Assert.Equal("Alta dev.origem", pediatria.EncaminhadaPor);
    }

    [SkippableFact]
    public async Task Devolve_para_a_fila_que_encaminhou_sem_escolher_destino()
    {
        Skip.IfNot(ApiFixture.BancoDisponivel, "ATENDIMENTO_TEST_DB nao configurado.");

        var client = await ProfissionalAsync("dev.volta");
        var atendimento = await AbrirAsync(client, "Paciente Que Volta");

        await TriarParaAsync(client, atendimento.Id, Especialidade.ClinicaGeral);

        (await EncaminharAsync(
            client, atendimento.Id, Especialidade.ClinicaGeral, Especialidade.Pediatria,
            "Parece caso de pediatria.")).EnsureSuccessStatusCode();

        var resposta = await client.PostAsJsonAsync(
            $"/api/atendimentos/{atendimento.Id}/etapas/Pediatria/devolver",
            new { motivo = "Idade adulta, nao e caso de pediatria." },
            Json);

        resposta.EnsureSuccessStatusCode();

        var etapas = await EtapasAsync(client, atendimento.Id);

        // A clinica geral estava concluida e volta a esperar, com a ficha que ja
        // tinha: e devolucao, nao consulta nova.
        Assert.Equal(
            StatusEtapa.Aguardando,
            etapas.Single(e => e.Especialidade == Especialidade.ClinicaGeral).Status);

        // A pediatria fica cancelada, e nao concluida: a pediatra olhou e
        // devolveu sem registrar nada, e contar isso como atendimento inflaria
        // a producao da especialidade.
        Assert.Equal(
            StatusEtapa.Cancelada,
            etapas.Single(e => e.Especialidade == Especialidade.Pediatria).Status);
    }

    [SkippableFact]
    public async Task Nao_devolve_quem_nao_veio_de_outra_fila()
    {
        Skip.IfNot(ApiFixture.BancoDisponivel, "ATENDIMENTO_TEST_DB nao configurado.");

        var client = await ProfissionalAsync("dev.semorigem");
        var atendimento = await AbrirAsync(client, "Paciente Direto Da Triagem");

        var resposta = await client.PostAsJsonAsync(
            $"/api/atendimentos/{atendimento.Id}/etapas/Triagem/devolver",
            new { motivo = "Nao ha para onde." },
            Json);

        // A triagem e a porta de entrada: nao ha fila anterior, e devolver
        // "para tras" a partir dela nao significa nada.
        Assert.Equal(HttpStatusCode.BadRequest, resposta.StatusCode);
    }

    [SkippableFact]
    public async Task Devolucao_muda_sem_motivo_e_recusada()
    {
        Skip.IfNot(ApiFixture.BancoDisponivel, "ATENDIMENTO_TEST_DB nao configurado.");

        var client = await ProfissionalAsync("dev.mudo");
        var atendimento = await AbrirAsync(client, "Paciente Devolvido Sem Motivo");

        await TriarParaAsync(client, atendimento.Id, Especialidade.ClinicaGeral);

        (await EncaminharAsync(
            client, atendimento.Id, Especialidade.ClinicaGeral, Especialidade.Pediatria,
            "Avaliar com a pediatria.")).EnsureSuccessStatusCode();

        var resposta = await client.PostAsJsonAsync(
            $"/api/atendimentos/{atendimento.Id}/etapas/Pediatria/devolver",
            new { motivo = "   " },
            Json);

        // Quem recebe o paciente de volta precisa saber por que ele voltou.
        Assert.Equal(HttpStatusCode.BadRequest, resposta.StatusCode);
    }

    // -----------------------------------------------------------------------
    // Da triagem nao se volta
    // -----------------------------------------------------------------------

    [SkippableFact]
    public async Task Nao_devolve_para_a_triagem()
    {
        Skip.IfNot(ApiFixture.BancoDisponivel, "ATENDIMENTO_TEST_DB nao configurado.");

        var client = await ProfissionalAsync("triagem.devolve");
        var atendimento = await AbrirAsync(client, "Paciente Ja Triado");

        // A triagem encaminhou para a clinica geral: a fila de origem da clinica
        // geral e a triagem, e sem a regra o botao de devolver mandaria de volta.
        await TriarParaAsync(client, atendimento.Id, Especialidade.ClinicaGeral);

        var resposta = await client.PostAsJsonAsync(
            $"/api/atendimentos/{atendimento.Id}/etapas/ClinicaGeral/devolver",
            new { motivo = "Nao e caso clinico." },
            Json);

        // Reabrir a triagem joga o paciente para o comeco da fila e faz o risco
        // ser classificado de novo, possivelmente para outra cor.
        Assert.Equal(HttpStatusCode.BadRequest, resposta.StatusCode);
        Assert.Contains("ja foi triado", await resposta.Content.ReadAsStringAsync());
    }

    [SkippableFact]
    public async Task Nao_encaminha_de_volta_para_a_triagem()
    {
        Skip.IfNot(ApiFixture.BancoDisponivel, "ATENDIMENTO_TEST_DB nao configurado.");

        var client = await ProfissionalAsync("triagem.encaminha");
        var atendimento = await AbrirAsync(client, "Paciente Triado Que Nao Volta");

        await TriarParaAsync(client, atendimento.Id, Especialidade.ClinicaGeral);

        var resposta = await EncaminharAsync(
            client, atendimento.Id, Especialidade.ClinicaGeral, Especialidade.Triagem,
            "Quero que triem de novo.");

        // A regra vale nos dois caminhos: bloquear so a devolucao deixaria o
        // mesmo efeito disponivel pelo botao ao lado.
        Assert.Equal(HttpStatusCode.BadRequest, resposta.StatusCode);
    }

    [SkippableFact]
    public async Task Nao_fecha_consulta_encaminhando_para_a_triagem()
    {
        Skip.IfNot(ApiFixture.BancoDisponivel, "ATENDIMENTO_TEST_DB nao configurado.");

        var client = await ProfissionalAsync("triagem.consulta");
        var atendimento = await AbrirAsync(client, "Paciente Da Consulta Que Nao Volta");

        await TriarParaAsync(client, atendimento.Id, Especialidade.ClinicaGeral);

        var resposta = await client.PutAsJsonAsync($"/api/atendimentos/{atendimento.Id}/consulta", new
        {
            especialidade = "ClinicaGeral",
            cid10Codigo = "M79.1",
            desfecho = "Encaminhado",
            encaminhadoPara = "Triagem"
        }, Json);

        // O terceiro caminho: fechar a consulta com desfecho "Encaminhado"
        // tambem abre a fila de destino.
        Assert.Equal(HttpStatusCode.BadRequest, resposta.StatusCode);

        // E a ficha nao pode ter sido gravada pela metade: a checagem acontece
        // antes de escrever.
        var prontuario = await client.GetFromJsonAsync<ProntuarioDto>(
            $"/api/atendimentos/{atendimento.Id}", Json);

        Assert.Empty(prontuario!.Consultas);
    }

    [SkippableFact]
    public async Task Quem_nunca_foi_triado_ainda_pode_ir_para_a_triagem()
    {
        Skip.IfNot(ApiFixture.BancoDisponivel, "ATENDIMENTO_TEST_DB nao configurado.");

        var client = await ProfissionalAsync("triagem.primeira");
        var atendimento = await AbrirAsync(client, "Paciente Atendido Sem Triagem");

        // Consulta direto, sem passar pela triagem: acontece quando o medico
        // atende quem chega passando mal.
        (await client.PutAsJsonAsync($"/api/atendimentos/{atendimento.Id}/consulta", new
        {
            especialidade = "ClinicaGeral",
            cid10Codigo = "M79.1",
            desfecho = "Alta"
        }, Json)).EnsureSuccessStatusCode();

        var resposta = await EncaminharAsync(
            client, atendimento.Id, Especialidade.Triagem, Especialidade.Enfermagem,
            "Encaminho para a enfermagem.");

        // A fila da triagem continua aberta e utilizavel: mandar para la quem
        // nunca foi triado nao e voltar, e ir pela primeira vez.
        resposta.EnsureSuccessStatusCode();
    }

    // -----------------------------------------------------------------------
    // Alta
    // -----------------------------------------------------------------------

    [SkippableFact]
    public async Task Alta_encerra_o_atendimento_e_nao_so_a_etapa()
    {
        Skip.IfNot(ApiFixture.BancoDisponivel, "ATENDIMENTO_TEST_DB nao configurado.");

        var client = await ProfissionalAsync("alta.simples");
        var atendimento = await AbrirAsync(client, "Paciente De Alta");

        await TriarParaAsync(client, atendimento.Id, Especialidade.ClinicaGeral);

        var resposta = await DarAltaAsync(client, atendimento.Id, Especialidade.ClinicaGeral);
        resposta.EnsureSuccessStatusCode();

        var prontuario = (await resposta.Content.ReadFromJsonAsync<ProntuarioDto>(Json))!;

        // Antes a alta fechava so a etapa, e o atendimento ficava aberto ate
        // alguem lembrar de finalizar — ninguem lembrava.
        Assert.Equal(StatusAtendimento.Finalizado, prontuario.Status);
        Assert.NotNull(prontuario.FinalizadoEm);
    }

    [SkippableFact]
    public async Task Alta_com_fila_pendente_e_recusada_ate_confirmarem()
    {
        Skip.IfNot(ApiFixture.BancoDisponivel, "ATENDIMENTO_TEST_DB nao configurado.");

        var client = await ProfissionalAsync("alta.pendente");
        var atendimento = await AbrirAsync(client, "Paciente Atendido Antes Da Triagem");

        // Consulta registrada direto, sem passar pela triagem — acontece em
        // campo quando o medico atende quem chega passando mal. A fila da
        // triagem continua aberta.
        (await client.PutAsJsonAsync($"/api/atendimentos/{atendimento.Id}/consulta", new
        {
            especialidade = "ClinicaGeral",
            cid10Codigo = "M79.1",
            desfecho = "Alta"
        }, Json)).EnsureSuccessStatusCode();

        var recusa = await DarAltaAsync(client, atendimento.Id, Especialidade.ClinicaGeral);

        // A triagem continua aberta: tirar o paciente de uma fila em silencio e
        // o tipo de coisa que so se descobre quando ele volta perguntando por ela.
        Assert.Equal(HttpStatusCode.BadRequest, recusa.StatusCode);
        Assert.Contains("Triagem", await recusa.Content.ReadAsStringAsync());
    }

    [SkippableFact]
    public async Task Confirmada_a_alta_cancela_as_filas_pendentes()
    {
        Skip.IfNot(ApiFixture.BancoDisponivel, "ATENDIMENTO_TEST_DB nao configurado.");

        var client = await ProfissionalAsync("alta.confirma");
        var atendimento = await AbrirAsync(client, "Paciente Com Pendencia Cancelada");

        (await client.PutAsJsonAsync($"/api/atendimentos/{atendimento.Id}/consulta", new
        {
            especialidade = "ClinicaGeral",
            cid10Codigo = "M79.1",
            desfecho = "Alta"
        }, Json)).EnsureSuccessStatusCode();

        (await DarAltaAsync(client, atendimento.Id, Especialidade.ClinicaGeral, cancelarPendentes: true))
            .EnsureSuccessStatusCode();

        var etapas = await EtapasAsync(client, atendimento.Id);

        // Cancelada, e nao concluida: ninguem triou, e concluir inflaria a
        // producao da triagem com atendimento que nao aconteceu.
        Assert.Equal(
            StatusEtapa.Cancelada,
            etapas.Single(e => e.Especialidade == Especialidade.Triagem).Status);
    }

    [SkippableFact]
    public async Task Alta_de_atendimento_ja_finalizado_e_recusada()
    {
        Skip.IfNot(ApiFixture.BancoDisponivel, "ATENDIMENTO_TEST_DB nao configurado.");

        var client = await ProfissionalAsync("alta.duasvezes");
        var atendimento = await AbrirAsync(client, "Paciente De Alta Repetida");

        await TriarParaAsync(client, atendimento.Id, Especialidade.ClinicaGeral);

        (await DarAltaAsync(client, atendimento.Id, Especialidade.ClinicaGeral))
            .EnsureSuccessStatusCode();

        var segunda = await DarAltaAsync(client, atendimento.Id, Especialidade.ClinicaGeral);

        Assert.Equal(HttpStatusCode.BadRequest, segunda.StatusCode);
    }

    // -----------------------------------------------------------------------
    // Producao: o que a devolucao poderia ter apagado
    // -----------------------------------------------------------------------

    [SkippableFact]
    public async Task Paciente_que_volta_nao_apaga_a_producao_do_primeiro_medico()
    {
        Skip.IfNot(ApiFixture.BancoDisponivel, "ATENDIMENTO_TEST_DB nao configurado.");

        var clinico = await ProfissionalAsync("prod.clinico");
        var atendimento = await AbrirAsync(clinico, "Paciente Que Vai E Volta");

        await TriarParaAsync(clinico, atendimento.Id, Especialidade.ClinicaGeral);

        (await clinico.PostAsync(
            $"/api/atendimentos/{atendimento.Id}/etapas/ClinicaGeral/assumir", null))
            .EnsureSuccessStatusCode();

        (await EncaminharAsync(
            clinico, atendimento.Id, Especialidade.ClinicaGeral, Especialidade.Pediatria,
            "Avaliar com a pediatria.")).EnsureSuccessStatusCode();

        // A pediatria devolve, e a clinica geral atende de novo — desta vez com
        // outra pessoa.
        var pediatra = await ProfissionalAsync("prod.pediatra");

        (await pediatra.PostAsync(
            $"/api/atendimentos/{atendimento.Id}/etapas/Pediatria/assumir", null))
            .EnsureSuccessStatusCode();

        (await pediatra.PostAsJsonAsync(
            $"/api/atendimentos/{atendimento.Id}/etapas/Pediatria/devolver",
            new { motivo = "Adulto, devolvo ao clinico." },
            Json)).EnsureSuccessStatusCode();

        var segundoClinico = await ProfissionalAsync("prod.clinico2");

        (await segundoClinico.PostAsync(
            $"/api/atendimentos/{atendimento.Id}/etapas/ClinicaGeral/assumir", null))
            .EnsureSuccessStatusCode();

        (await DarAltaAsync(segundoClinico, atendimento.Id, Especialidade.ClinicaGeral, true))
            .EnsureSuccessStatusCode();

        var bases = await clinico.GetFromJsonAsync<List<BaseDto>>("/api/bases", Json);

        // Quem nao e coordenacao recebe da API so a propria producao, entao cada
        // um pergunta pela sua.
        var doPrimeiro = await clinico.GetFromJsonAsync<List<ProducaoProfissionalDto>>(
            $"/api/relatorios/producao?baseId={bases![0].Id}", Json);

        var doSegundo = await segundoClinico.GetFromJsonAsync<List<ProducaoProfissionalDto>>(
            $"/api/relatorios/producao?baseId={bases[0].Id}", Json);

        // A etapa da clinica geral e uma so, e foi concluida duas vezes. Lida
        // pela etapa, a segunda consulta apagaria a primeira e o trabalho do
        // primeiro medico sumiria da tabela.
        Assert.Contains(doPrimeiro!, p => p.Nome == "Alta prod.clinico");
        Assert.Contains(doSegundo!, p => p.Nome == "Alta prod.clinico2");
    }
}
