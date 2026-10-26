using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using AtendimentoDeCampo.Api.Contratos;
using AtendimentoDeCampo.Domain;

namespace AtendimentoDeCampo.Tests;

/// <summary>
/// Os dois blocos da ficha de saude mental voltam no prontuario.
///
/// CORRIGE: eram gravados e nunca devolvidos. O pedido aceitava os sintomas e
/// as perdas vivenciadas, a entidade os guardava, e o DTO de leitura nao os
/// tinha — entao a ficha de saude mental era lida como consulta generica, sem
/// as duas perguntas que a tornam uma ficha de saude mental. Sobrevivia so o
/// diff da auditoria, que serve para saber o que mudou, e nao para ler a ficha.
///
/// "Perdas vivenciadas" e pergunta do formulario de catastrofe — casa,
/// familiar, documentos —, e e o dado que orienta o acompanhamento.
/// </summary>
[Collection(Colecoes.Api)]
public class FichaDeSaudeMentalTests
{
    private readonly ApiFixture _fixture;

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    public FichaDeSaudeMentalTests(ApiFixture fixture) => _fixture = fixture;

    [SkippableFact]
    public async Task Os_sintomas_e_as_perdas_voltam_no_prontuario()
    {
        Skip.IfNot(ApiFixture.BancoDisponivel, "ATENDIMENTO_TEST_DB nao configurado.");

        var client = await _fixture.ClienteDeAsync(
            "psicologo.ficha", "Psicologo Da Ficha", FuncaoProfissional.Psicologo, "CRP 99001");

        var bases = await client.GetFromJsonAsync<List<BaseDto>>("/api/bases", Json);
        var codigo = (await client.GetFromJsonAsync<CodigoNovoDto>("/api/pacientes/codigo-novo", Json))!.Codigo;

        var criado = await client.PostAsJsonAsync("/api/atendimentos", new
        {
            baseId = bases![0].Id,
            paciente = new
            {
                codigo,
                nome = "Paciente Em Luto",
                tipoDocumento = "SemDocumento",
                idadeAproximada = 44,
                sexo = "NaoInformado",
                statusAlergia = "NaoPerguntado",
                consentimentoRegistro = true
            }
        }, Json);

        criado.EnsureSuccessStatusCode();

        var atendimento = (await criado.Content.ReadFromJsonAsync<ProntuarioDto>(Json))!;

        (await client.PutAsJsonAsync($"/api/atendimentos/{atendimento.Id}/consulta", new
        {
            especialidade = "SaudeMental",
            sintomasDescricao = "Insonia e choro facil desde a enchente.",
            cid10Codigo = "F43.2",
            conduta = "Acolhimento e retorno em uma semana.",
            desfecho = "Retorno",
            sintomasSaudeMental = new[] { "Tristeza", "Insonia", "Luto" },
            perdasVivenciadas = new[] { "Casa", "Familiar", "Documentos" }
        }, Json)).EnsureSuccessStatusCode();

        var prontuario = await client.GetFromJsonAsync<ProntuarioDto>(
            $"/api/atendimentos/{atendimento.Id}", Json);

        var consulta = prontuario!.Consultas.Single(c => c.Especialidade == Especialidade.SaudeMental);

        Assert.Equal(
            [SintomaSaudeMental.Tristeza, SintomaSaudeMental.Insonia, SintomaSaudeMental.Luto],
            consulta.SintomasSaudeMental);

        Assert.Equal(
            [PerdaVivenciada.Casa, PerdaVivenciada.Familiar, PerdaVivenciada.Documentos],
            consulta.PerdasVivenciadas);
    }

    [SkippableFact]
    public async Task Consulta_de_outra_especialidade_volta_com_as_listas_vazias()
    {
        Skip.IfNot(ApiFixture.BancoDisponivel, "ATENDIMENTO_TEST_DB nao configurado.");

        var client = await _fixture.ClienteDeAsync(
            "clinico.ficha", "Clinico Da Ficha", FuncaoProfissional.ClinicoGeral, "CRM 99002");

        var bases = await client.GetFromJsonAsync<List<BaseDto>>("/api/bases", Json);
        var codigo = (await client.GetFromJsonAsync<CodigoNovoDto>("/api/pacientes/codigo-novo", Json))!.Codigo;

        var criado = await client.PostAsJsonAsync("/api/atendimentos", new
        {
            baseId = bases![0].Id,
            paciente = new
            {
                codigo,
                nome = "Paciente Clinico",
                tipoDocumento = "SemDocumento",
                idadeAproximada = 51,
                sexo = "NaoInformado",
                statusAlergia = "NaoPerguntado",
                consentimentoRegistro = true
            }
        }, Json);

        criado.EnsureSuccessStatusCode();

        var atendimento = (await criado.Content.ReadFromJsonAsync<ProntuarioDto>(Json))!;

        (await client.PutAsJsonAsync($"/api/atendimentos/{atendimento.Id}/consulta", new
        {
            especialidade = "ClinicaGeral",
            sintomasDescricao = "Tosse ha cinco dias.",
            cid10Codigo = "J18.9",
            conduta = "Antibiotico por sete dias.",
            desfecho = "Alta"
        }, Json)).EnsureSuccessStatusCode();

        var prontuario = await client.GetFromJsonAsync<ProntuarioDto>(
            $"/api/atendimentos/{atendimento.Id}", Json);

        var consulta = prontuario!.Consultas.Single(c => c.Especialidade == Especialidade.ClinicaGeral);

        // Lista vazia, e nao nula: a ficha nao tem o bloco, e nao "tem o bloco
        // em branco" — quem le a consulta nao precisa tratar os dois casos.
        Assert.Empty(consulta.SintomasSaudeMental);
        Assert.Empty(consulta.PerdasVivenciadas);
    }
}
