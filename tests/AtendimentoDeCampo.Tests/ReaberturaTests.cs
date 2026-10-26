using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using AtendimentoDeCampo.Api.Contratos;
using AtendimentoDeCampo.Domain;

namespace AtendimentoDeCampo.Tests;

/// <summary>
/// Reabrir um atendimento o deixa de novo aberto — para valer.
///
/// CORRIGE: `ReabrirAsync` muda o status, e de proposito NAO limpa
/// <c>FinalizadoEm</c>: e por ela que a auditoria sabe marcar as correcoes como
/// feitas depois do fecho, que e todo o motivo de a reabertura exigir
/// justificativa. So que encaminhar, devolver e encerrar liam justamente
/// <c>FinalizadoEm</c> para decidir se o atendimento estava fechado. O efeito
/// era que reabrir nao reabria nada: a correcao de campo era aceita, mas mover
/// o paciente continuava recusado — com a mensagem mandando reabrir um
/// atendimento que acabara de ser reaberto —, e fechar de novo com desfecho era
/// impossivel.
///
/// Agora o status responde "ainda esta aberto?" e e o que trava; a hora do
/// fecho responde "ja foi fechado alguma vez?" e e o que marca a auditoria.
/// </summary>
[Collection(Colecoes.Api)]
public class ReaberturaTests
{
    private readonly ApiFixture _fixture;

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    public ReaberturaTests(ApiFixture fixture) => _fixture = fixture;

    private Task<HttpClient> ProfissionalAsync()
        => _fixture.ClienteDeAsync(
            "reabre.teste", "Reabre Teste", FuncaoProfissional.Coordenacao);

    private static async Task<ProntuarioDto> TriadoEAltaAsync(HttpClient client, string nome)
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
                idadeAproximada = 41,
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
            statusAlergia = "SemAlergiaConhecida"
        }, Json)).EnsureSuccessStatusCode();

        (await client.PostAsJsonAsync(
            $"/api/atendimentos/{prontuario.Id}/etapas/Triagem/alta",
            new { cancelarPendentes = true },
            Json)).EnsureSuccessStatusCode();

        return prontuario;
    }

    [SkippableFact]
    public async Task Reaberto_recusa_encaminhar_pela_etapa_fechada_e_diz_o_motivo_certo()
    {
        Skip.IfNot(ApiFixture.BancoDisponivel, "ATENDIMENTO_TEST_DB nao configurado.");

        var client = await ProfissionalAsync();
        var atendimento = await TriadoEAltaAsync(client, "Paciente Que Voltou");

        (await client.PostAsJsonAsync(
            $"/api/atendimentos/{atendimento.Id}/reabrir",
            new { justificativa = "A paciente voltou com a mesma queixa no mesmo dia." },
            Json)).EnsureSuccessStatusCode();

        var resposta = await client.PostAsJsonAsync(
            $"/api/atendimentos/{atendimento.Id}/etapas/Triagem/encaminhar",
            new { destino = "ClinicaGeral", motivo = "Queixa persiste; avaliar." },
            Json);

        /*
            Continua recusado, e esta certo: a alta fechou a etapa da triagem, e
            de fila fechada ninguem encaminha. O que muda e a razao da recusa.

            Antes a resposta era "este atendimento ja foi finalizado, reabra
            antes de encaminhar" num atendimento que acabara de ser reaberto —
            instrucao impossivel de seguir. Agora diz o que de fato impede, que
            e outra coisa e tem outra saida: abrir a fila para onde o paciente
            precisa ir.
        */
        Assert.False(resposta.IsSuccessStatusCode);

        var corpo = await resposta.Content.ReadAsStringAsync();

        Assert.Contains("etapa ja foi concluida", corpo);
        Assert.DoesNotContain("Reabra antes de encaminhar", corpo);
    }

    [SkippableFact]
    public async Task Reaberto_aceita_ser_fechado_de_novo_com_outro_desfecho()
    {
        Skip.IfNot(ApiFixture.BancoDisponivel, "ATENDIMENTO_TEST_DB nao configurado.");

        var client = await ProfissionalAsync();
        var atendimento = await TriadoEAltaAsync(client, "Paciente Com Desfecho Errado");

        // O caso real da reabertura: a alta foi lancada por engano, e o
        // paciente tinha sido transferido.
        (await client.PostAsJsonAsync(
            $"/api/atendimentos/{atendimento.Id}/reabrir",
            new { justificativa = "A alta foi lancada por engano; houve transferencia." },
            Json)).EnsureSuccessStatusCode();

        var resposta = await client.PostAsJsonAsync(
            $"/api/atendimentos/{atendimento.Id}/etapas/Triagem/encerrar",
            new
            {
                desfecho = "TransferenciaHospitalar",
                detalhe = "Hospital Regional, por ambulancia da base.",
                cancelarPendentes = true
            },
            Json);

        resposta.EnsureSuccessStatusCode();

        var depois = await client.GetFromJsonAsync<ProntuarioDto>(
            $"/api/atendimentos/{atendimento.Id}", Json);

        Assert.Equal(StatusAtendimento.Finalizado, depois!.Status);
        Assert.Equal(DesfechoAtendimento.TransferenciaHospitalar, depois.Desfecho);
        Assert.Contains("Hospital Regional", depois.DesfechoDetalhe);
    }

    [SkippableFact]
    public async Task Sem_reabrir_continua_recusado()
    {
        Skip.IfNot(ApiFixture.BancoDisponivel, "ATENDIMENTO_TEST_DB nao configurado.");

        var client = await ProfissionalAsync();
        var atendimento = await TriadoEAltaAsync(client, "Paciente Fechado");

        var resposta = await client.PostAsJsonAsync(
            $"/api/atendimentos/{atendimento.Id}/etapas/Triagem/encaminhar",
            new { destino = "ClinicaGeral", motivo = "Sem reabrir antes." },
            Json);

        Assert.False(resposta.IsSuccessStatusCode);
    }

    [SkippableFact]
    public async Task A_correcao_depois_do_fecho_continua_marcada_na_auditoria()
    {
        Skip.IfNot(ApiFixture.BancoDisponivel, "ATENDIMENTO_TEST_DB nao configurado.");

        var client = await ProfissionalAsync();
        var atendimento = await TriadoEAltaAsync(client, "Paciente Corrigido");

        (await client.PostAsJsonAsync(
            $"/api/atendimentos/{atendimento.Id}/reabrir",
            new { justificativa = "Faltou registrar a orientacao de retorno." },
            Json)).EnsureSuccessStatusCode();

        (await client.PutAsJsonAsync($"/api/atendimentos/{atendimento.Id}/consulta", new
        {
            especialidade = "ClinicaGeral",
            cid10Codigo = "M54.5",
            conduta = "Analgesia e orientacao de retorno.",
            desfecho = "Alta"
        }, Json)).EnsureSuccessStatusCode();

        var depois = await client.GetFromJsonAsync<ProntuarioDto>(
            $"/api/atendimentos/{atendimento.Id}", Json);

        // E isto que a hora do fecho preserva, e por isso ela nao e limpa na
        // reabertura: a correcao honesta continua distinguivel da alteracao
        // silenciosa.
        Assert.Contains(depois!.Historico, h => h.Acao == AcaoAuditoria.EditouAposFinalizacao);
        Assert.Contains(depois.Historico, h => h.Acao == AcaoAuditoria.ReabriuAtendimento);
    }
}
