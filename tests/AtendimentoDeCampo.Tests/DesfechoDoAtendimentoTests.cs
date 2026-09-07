using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using AtendimentoDeCampo.Api.Contratos;
using AtendimentoDeCampo.Domain;

namespace AtendimentoDeCampo.Tests;

/// <summary>
/// O bloco "Desfecho" do formulario de papel: alta, transferencia hospitalar,
/// obito ou outro motivo.
///
/// Ate aqui o sistema so sabia dar alta. Num formulario de catastrofe, obito e
/// justamente o desfecho que precisa ser registrado — e a classificacao Preto do
/// START ja existia sem ter onde registrar o que aconteceu depois.
/// </summary>
[Collection(Colecoes.Api)]
public class DesfechoDoAtendimentoTests
{
    private readonly ApiFixture _fixture;

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    public DesfechoDoAtendimentoTests(ApiFixture fixture) => _fixture = fixture;

    private Task<HttpClient> ProfissionalAsync(string usuario)
        => _fixture.ClienteDeAsync(usuario, $"Desfecho {usuario}", FuncaoProfissional.Coordenacao);

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
                idadeAproximada = 45,
                sexo = "NaoInformado",
                statusAlergia = "NaoPerguntado",
                consentimentoRegistro = true
            }
        }, Json);

        resposta.EnsureSuccessStatusCode();
        return (await resposta.Content.ReadFromJsonAsync<ProntuarioDto>(Json))!;
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

    private static Task<HttpResponseMessage> EncerrarAsync(
        HttpClient client,
        Guid id,
        Especialidade fila,
        DesfechoAtendimento desfecho,
        string? detalhe = null,
        bool cancelarPendentes = false)
        => client.PostAsJsonAsync(
            $"/api/atendimentos/{id}/etapas/{fila}/encerrar",
            new { desfecho = desfecho.ToString(), detalhe, cancelarPendentes },
            Json);

    /// <summary>Abre, tria para a clinica geral e devolve o atendimento.</summary>
    private async Task<(HttpClient Cliente, ProntuarioDto Atendimento)> PreparadoAsync(
        string usuario, string paciente)
    {
        var client = await ProfissionalAsync(usuario);
        var atendimento = await AbrirAsync(client, paciente);
        await TriarParaAsync(client, atendimento.Id, Especialidade.ClinicaGeral);

        return (client, atendimento);
    }

    [SkippableFact]
    public async Task Registra_o_obito_e_encerra_o_atendimento()
    {
        Skip.IfNot(ApiFixture.BancoDisponivel, "ATENDIMENTO_TEST_DB nao configurado.");

        var (client, atendimento) = await PreparadoAsync("desf.obito", "Paciente Que Morreu");

        var resposta = await EncerrarAsync(
            client, atendimento.Id, Especialidade.ClinicaGeral, DesfechoAtendimento.Obito);

        resposta.EnsureSuccessStatusCode();

        var prontuario = (await resposta.Content.ReadFromJsonAsync<ProntuarioDto>(Json))!;

        Assert.Equal(StatusAtendimento.Finalizado, prontuario.Status);
        Assert.Equal(DesfechoAtendimento.Obito, prontuario.Desfecho);
    }

    [SkippableFact]
    public async Task O_obito_fica_no_historico_com_quem_registrou()
    {
        Skip.IfNot(ApiFixture.BancoDisponivel, "ATENDIMENTO_TEST_DB nao configurado.");

        var (client, atendimento) = await PreparadoAsync("desf.historico", "Paciente Do Historico");

        (await EncerrarAsync(
            client, atendimento.Id, Especialidade.ClinicaGeral, DesfechoAtendimento.Obito))
            .EnsureSuccessStatusCode();

        var prontuario = await client.GetFromJsonAsync<ProntuarioDto>(
            $"/api/atendimentos/{atendimento.Id}", Json);

        // Acao propria, e nao um detalhe do encerramento: daqui a um ano a
        // pergunta vai ser quem registrou, e a resposta nao pode depender de ler
        // o diff de um campo.
        var registro = prontuario!.Historico.Single(h => h.Acao == AcaoAuditoria.RegistrouObito);

        Assert.Equal("Desfecho desf.historico", registro.Profissional);
    }

    [SkippableFact]
    public async Task Transferencia_sem_destino_e_recusada()
    {
        Skip.IfNot(ApiFixture.BancoDisponivel, "ATENDIMENTO_TEST_DB nao configurado.");

        var (client, atendimento) = await PreparadoAsync("desf.semdestino", "Paciente Transferido");

        var resposta = await EncerrarAsync(
            client, atendimento.Id, Especialidade.ClinicaGeral,
            DesfechoAtendimento.TransferenciaHospitalar);

        // "Transferido" sem dizer para onde nao permite ninguem ir atras do
        // paciente depois, que e a unica razao de registrar a transferencia.
        Assert.Equal(HttpStatusCode.BadRequest, resposta.StatusCode);
    }

    [SkippableFact]
    public async Task Espaco_em_branco_nao_conta_como_destino()
    {
        Skip.IfNot(ApiFixture.BancoDisponivel, "ATENDIMENTO_TEST_DB nao configurado.");

        var (client, atendimento) = await PreparadoAsync("desf.branco", "Paciente Destino Vazio");

        var resposta = await EncerrarAsync(
            client, atendimento.Id, Especialidade.ClinicaGeral,
            DesfechoAtendimento.TransferenciaHospitalar, "   ");

        Assert.Equal(HttpStatusCode.BadRequest, resposta.StatusCode);
    }

    [SkippableFact]
    public async Task Transferencia_guarda_o_destino()
    {
        Skip.IfNot(ApiFixture.BancoDisponivel, "ATENDIMENTO_TEST_DB nao configurado.");

        var (client, atendimento) = await PreparadoAsync("desf.destino", "Paciente Para O Hospital");

        var resposta = await EncerrarAsync(
            client, atendimento.Id, Especialidade.ClinicaGeral,
            DesfechoAtendimento.TransferenciaHospitalar, "Hospital Regional de Boa Vista");

        resposta.EnsureSuccessStatusCode();

        var prontuario = (await resposta.Content.ReadFromJsonAsync<ProntuarioDto>(Json))!;

        Assert.Equal(DesfechoAtendimento.TransferenciaHospitalar, prontuario.Desfecho);
        Assert.Equal("Hospital Regional de Boa Vista", prontuario.DesfechoDetalhe);
    }

    [SkippableFact]
    public async Task O_destino_tambem_entra_no_historico()
    {
        Skip.IfNot(ApiFixture.BancoDisponivel, "ATENDIMENTO_TEST_DB nao configurado.");

        var (client, atendimento) = await PreparadoAsync("desf.diff", "Paciente Do Diff");

        (await EncerrarAsync(
            client, atendimento.Id, Especialidade.ClinicaGeral,
            DesfechoAtendimento.TransferenciaHospitalar, "Hospital de Campanha"))
            .EnsureSuccessStatusCode();

        var prontuario = await client.GetFromJsonAsync<ProntuarioDto>(
            $"/api/atendimentos/{atendimento.Id}", Json);

        // Para onde o paciente foi e a informacao que alguem vai procurar depois,
        // e o historico e onde se procura.
        Assert.Contains(
            prontuario!.Historico,
            h => h.Campo == "atendimento.desfechoDetalhe" && h.ValorNovo == "Hospital de Campanha");
    }

    [SkippableFact]
    public async Task Outro_motivo_exige_descricao()
    {
        Skip.IfNot(ApiFixture.BancoDisponivel, "ATENDIMENTO_TEST_DB nao configurado.");

        var (client, atendimento) = await PreparadoAsync("desf.outro", "Paciente Outro Motivo");

        var recusa = await EncerrarAsync(
            client, atendimento.Id, Especialidade.ClinicaGeral, DesfechoAtendimento.Outro);

        Assert.Equal(HttpStatusCode.BadRequest, recusa.StatusCode);

        var aceita = await EncerrarAsync(
            client, atendimento.Id, Especialidade.ClinicaGeral, DesfechoAtendimento.Outro,
            "Paciente saiu por conta propria antes da consulta.");

        aceita.EnsureSuccessStatusCode();
    }

    [SkippableFact]
    public async Task A_alta_continua_sendo_o_desfecho_padrao()
    {
        Skip.IfNot(ApiFixture.BancoDisponivel, "ATENDIMENTO_TEST_DB nao configurado.");

        var (client, atendimento) = await PreparadoAsync("desf.padrao", "Paciente De Alta Simples");

        var resposta = await client.PostAsJsonAsync(
            $"/api/atendimentos/{atendimento.Id}/etapas/ClinicaGeral/encerrar",
            new { },
            Json);

        resposta.EnsureSuccessStatusCode();

        var prontuario = (await resposta.Content.ReadFromJsonAsync<ProntuarioDto>(Json))!;

        // Sem desfecho informado e alta: e o caso comum, e obrigar a escolher
        // transformaria o clique mais frequente do plantao em dois.
        Assert.Equal(DesfechoAtendimento.Alta, prontuario.Desfecho);
    }

    [SkippableFact]
    public async Task A_rota_antiga_de_alta_continua_funcionando()
    {
        Skip.IfNot(ApiFixture.BancoDisponivel, "ATENDIMENTO_TEST_DB nao configurado.");

        var (client, atendimento) = await PreparadoAsync("desf.rotaantiga", "Paciente Da Rota Antiga");

        // Front e API sobem em servicos separados: durante a janela de deploy a
        // tela antiga ainda chama /alta, e um 404 ali derrubaria o botao de alta
        // para a equipe em campo.
        var resposta = await client.PostAsJsonAsync(
            $"/api/atendimentos/{atendimento.Id}/etapas/ClinicaGeral/alta",
            new { cancelarPendentes = false },
            Json);

        resposta.EnsureSuccessStatusCode();

        var prontuario = (await resposta.Content.ReadFromJsonAsync<ProntuarioDto>(Json))!;

        Assert.Equal(DesfechoAtendimento.Alta, prontuario.Desfecho);
    }

    [SkippableFact]
    public async Task O_desfecho_aparece_na_lista_sem_precisar_abrir_a_ficha()
    {
        Skip.IfNot(ApiFixture.BancoDisponivel, "ATENDIMENTO_TEST_DB nao configurado.");

        var (client, atendimento) = await PreparadoAsync("desf.lista", "Paciente Da Lista");

        (await EncerrarAsync(
            client, atendimento.Id, Especialidade.ClinicaGeral, DesfechoAtendimento.Obito))
            .EnsureSuccessStatusCode();

        var bases = await client.GetFromJsonAsync<List<BaseDto>>("/api/bases", Json);
        var lista = await client.GetFromJsonAsync<List<AtendimentoResumoDto>>(
            $"/api/atendimentos?baseId={bases![0].Id}", Json);

        Assert.Equal(
            DesfechoAtendimento.Obito,
            lista!.Single(a => a.Id == atendimento.Id).Desfecho);
    }

    [SkippableFact]
    public async Task Atendimento_aberto_nao_tem_desfecho()
    {
        Skip.IfNot(ApiFixture.BancoDisponivel, "ATENDIMENTO_TEST_DB nao configurado.");

        var (client, atendimento) = await PreparadoAsync("desf.aberto", "Paciente Ainda Aberto");

        var prontuario = await client.GetFromJsonAsync<ProntuarioDto>(
            $"/api/atendimentos/{atendimento.Id}", Json);

        // Nulo, e nao "Alta": os atendimentos fechados antes deste campo existir
        // tambem ficam nulos, e preencher todos como alta contaria como alta
        // quem morreu.
        Assert.Null(prontuario!.Desfecho);
    }
}
