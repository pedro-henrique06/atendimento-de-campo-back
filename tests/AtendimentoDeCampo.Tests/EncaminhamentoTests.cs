using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using AtendimentoDeCampo.Api.Contratos;
using AtendimentoDeCampo.Domain;

namespace AtendimentoDeCampo.Tests;

/// <summary>
/// Encaminhar como acao propria, e nao como efeito de fechar uma consulta.
///
/// Antes a unica forma de redirecionar era concluir a consulta com desfecho
/// "Encaminhado", o que exige CID-10: quando a triagem errava a fila, o medico
/// teria que inventar um diagnostico para uma consulta que nao aconteceu.
/// </summary>
[Collection(Colecoes.Api)]
public class EncaminhamentoTests
{
    private readonly ApiFixture _fixture;

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    public EncaminhamentoTests(ApiFixture fixture) => _fixture = fixture;

    private async Task<HttpClient> ProfissionalAsync(string usuario = "encaminha.teste")
    {
        var client = _fixture.CreateClient();
        const string senha = "plantao-2026";

        var registro = await client.PostAsJsonAsync("/api/auth/registrar", new
        {
            usuario,
            nome = "Encaminha Teste",
            funcao = "Medico",
            registro = "44221",
            senha,
            confirmacaoSenha = senha,
            idioma = "Pt"
        }, Json);

        if (registro.IsSuccessStatusCode)
        {
            var criado = await registro.Content.ReadFromJsonAsync<ProfissionalDto>(Json);
            var admin = _fixture.CreateClient();

            var entradaAdmin = await (await admin.PostAsJsonAsync("/api/auth/login", new
            {
                usuario = ApiFixture.AdminUsuario,
                senha = ApiFixture.AdminSenha,
                idioma = "Pt"
            }, Json)).Content.ReadFromJsonAsync<LoginResponse>(Json);

            admin.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", entradaAdmin!.Token);
            (await admin.PostAsJsonAsync($"/api/profissionais/{criado!.Id}/aprovar", new { }, Json))
                .EnsureSuccessStatusCode();
        }

        var entrada = await client.PostAsJsonAsync("/api/auth/login",
            new { usuario, senha, idioma = "Pt" }, Json);
        entrada.EnsureSuccessStatusCode();

        var sessao = await entrada.Content.ReadFromJsonAsync<LoginResponse>(Json);
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", sessao!.Token);

        return client;
    }

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

    /// <summary>As etapas so aparecem no resumo da lista, nao no prontuario.</summary>
    private static async Task<List<EtapaResumoDto>> EtapasAsync(HttpClient client, Guid id)
    {
        var bases = await client.GetFromJsonAsync<List<BaseDto>>("/api/bases", Json);
        var lista = await client.GetFromJsonAsync<List<AtendimentoResumoDto>>(
            $"/api/atendimentos?baseId={bases![0].Id}", Json);

        return lista!.Single(a => a.Id == id).Etapas;
    }

    /// <summary>Tria e manda para a fila indicada, como o fluxo normal faz.</summary>
    private static async Task TriarParaAsync(HttpClient client, Guid id, Especialidade destino)
    {
        (await client.PutAsJsonAsync($"/api/atendimentos/{id}/triagem", new
        {
            classificacaoRisco = "Verde",
            statusAlergia = "SemAlergiaConhecida",
            encaminhamento = destino.ToString()
        }, Json)).EnsureSuccessStatusCode();
    }

    [SkippableFact]
    public async Task Redireciona_sem_precisar_inventar_diagnostico()
    {
        Skip.IfNot(ApiFixture.BancoDisponivel, "ATENDIMENTO_TEST_DB nao configurado.");

        var client = await ProfissionalAsync();
        var atendimento = await AbrirAsync(client, "Paciente Mal Encaminhado");

        await TriarParaAsync(client, atendimento.Id, Especialidade.ClinicaGeral);

        // A triagem errou: e problema dentario. Sem esta rota, o medico teria
        // que fechar uma consulta com CID-10 para poder redirecionar.
        var resposta = await client.PostAsJsonAsync(
            $"/api/atendimentos/{atendimento.Id}/etapas/ClinicaGeral/encaminhar",
            new { destino = "Odontologia", motivo = "Queixa e dor de dente, nao clinica." },
            Json);

        resposta.EnsureSuccessStatusCode();

        var etapas = await EtapasAsync(client, atendimento.Id);
        var odonto = etapas.Single(e => e.Especialidade == Especialidade.Odontologia);

        Assert.Equal(StatusEtapa.Aguardando, odonto.Status);
    }

    [SkippableFact]
    public async Task Fila_de_onde_saiu_fica_cancelada_e_nao_concluida()
    {
        Skip.IfNot(ApiFixture.BancoDisponivel, "ATENDIMENTO_TEST_DB nao configurado.");

        var client = await ProfissionalAsync();
        var atendimento = await AbrirAsync(client, "Paciente Que So Passou");

        await TriarParaAsync(client, atendimento.Id, Especialidade.ClinicaGeral);

        var resposta = await client.PostAsJsonAsync(
            $"/api/atendimentos/{atendimento.Id}/etapas/ClinicaGeral/encaminhar",
            new { destino = "Odontologia", motivo = "Nao e caso clinico." },
            Json);

        resposta.EnsureSuccessStatusCode();

        // Marcar como concluida inflaria a producao da clinica geral com um
        // atendimento que nao aconteceu.
        var etapas = await EtapasAsync(client, atendimento.Id);
        var clinica = etapas.Single(e => e.Especialidade == Especialidade.ClinicaGeral);

        Assert.Equal(StatusEtapa.Cancelada, clinica.Status);
    }

    [SkippableFact]
    public async Task Encaminhamento_sem_motivo_e_recusado()
    {
        Skip.IfNot(ApiFixture.BancoDisponivel, "ATENDIMENTO_TEST_DB nao configurado.");

        var client = await ProfissionalAsync();
        var atendimento = await AbrirAsync(client, "Paciente Sem Motivo");

        await TriarParaAsync(client, atendimento.Id, Especialidade.ClinicaGeral);

        // Sem motivo, o paciente circula entre filas e ninguem entende o caminho.
        var resposta = await client.PostAsJsonAsync(
            $"/api/atendimentos/{atendimento.Id}/etapas/ClinicaGeral/encaminhar",
            new { destino = "Odontologia", motivo = "" },
            Json);

        Assert.Equal(HttpStatusCode.BadRequest, resposta.StatusCode);
    }

    [SkippableFact]
    public async Task O_motivo_fica_no_historico_para_quem_recebe()
    {
        Skip.IfNot(ApiFixture.BancoDisponivel, "ATENDIMENTO_TEST_DB nao configurado.");

        var client = await ProfissionalAsync();
        var atendimento = await AbrirAsync(client, "Paciente Com Motivo");

        await TriarParaAsync(client, atendimento.Id, Especialidade.ClinicaGeral);

        (await client.PostAsJsonAsync(
            $"/api/atendimentos/{atendimento.Id}/etapas/ClinicaGeral/encaminhar",
            new { destino = "Odontologia", motivo = "Abscesso dentario visivel." },
            Json)).EnsureSuccessStatusCode();

        var prontuario = await client.GetFromJsonAsync<ProntuarioDto>(
            $"/api/atendimentos/{atendimento.Id}", Json);

        Assert.Contains(prontuario!.Historico, a => a.ValorNovo == "Abscesso dentario visivel.");

        // Chave e valores canonicos: a traducao acontece na hora de exibir.
        Assert.Contains(prontuario.Historico,
            a => a.Campo == "atendimento.fila" && a.ValorNovo == "Odontologia");
    }

    [SkippableFact]
    public async Task A_odontologia_tambem_encaminha()
    {
        Skip.IfNot(ApiFixture.BancoDisponivel, "ATENDIMENTO_TEST_DB nao configurado.");

        var client = await ProfissionalAsync();
        var atendimento = await AbrirAsync(client, "Paciente Da Odonto");

        await TriarParaAsync(client, atendimento.Id, Especialidade.Odontologia);

        // A odontologia nao tem campo de encaminhamento na ficha: sem esta rota
        // o dentista nao tinha como mandar ninguem para lugar nenhum.
        var resposta = await client.PostAsJsonAsync(
            $"/api/atendimentos/{atendimento.Id}/etapas/Odontologia/encaminhar",
            new { destino = "ClinicaGeral", motivo = "Precisa de antibiotico sistemico." },
            Json);

        resposta.EnsureSuccessStatusCode();

        var etapas = await EtapasAsync(client, atendimento.Id);

        Assert.Contains(etapas, e => e.Especialidade == Especialidade.ClinicaGeral);
    }

    [SkippableFact]
    public async Task Nao_encaminha_para_a_propria_fila()
    {
        Skip.IfNot(ApiFixture.BancoDisponivel, "ATENDIMENTO_TEST_DB nao configurado.");

        var client = await ProfissionalAsync();
        var atendimento = await AbrirAsync(client, "Paciente Circular");

        await TriarParaAsync(client, atendimento.Id, Especialidade.ClinicaGeral);

        var resposta = await client.PostAsJsonAsync(
            $"/api/atendimentos/{atendimento.Id}/etapas/ClinicaGeral/encaminhar",
            new { destino = "ClinicaGeral", motivo = "Engano." },
            Json);

        Assert.Equal(HttpStatusCode.BadRequest, resposta.StatusCode);
    }

    [SkippableFact]
    public async Task Nao_encaminha_a_partir_de_etapa_ja_concluida()
    {
        Skip.IfNot(ApiFixture.BancoDisponivel, "ATENDIMENTO_TEST_DB nao configurado.");

        var client = await ProfissionalAsync();
        var atendimento = await AbrirAsync(client, "Paciente Ja Triado");

        await TriarParaAsync(client, atendimento.Id, Especialidade.ClinicaGeral);

        // A triagem se conclui ao ser registrada; reencaminhar dali reescreveria
        // um passo que ja aconteceu.
        var resposta = await client.PostAsJsonAsync(
            $"/api/atendimentos/{atendimento.Id}/etapas/Triagem/encaminhar",
            new { destino = "Odontologia", motivo = "Tarde demais." },
            Json);

        Assert.Equal(HttpStatusCode.BadRequest, resposta.StatusCode);
    }
}
