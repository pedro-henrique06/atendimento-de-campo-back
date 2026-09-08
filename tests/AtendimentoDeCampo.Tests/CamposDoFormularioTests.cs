using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using AtendimentoDeCampo.Api.Contratos;
using AtendimentoDeCampo.Domain;

namespace AtendimentoDeCampo.Tests;

/// <summary>
/// Os campos que os formularios de papel tem e o sistema nao tinha: raca/cor,
/// etnia, polo base e DSEI; CPF em campo proprio; circunferencia cefalica,
/// testes rapidos e cirurgias previas na triagem; historia clinica, exame
/// fisico e orientacoes gerais na consulta.
/// </summary>
[Collection(Colecoes.Api)]
public class CamposDoFormularioTests
{
    private readonly ApiFixture _fixture;

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    public CamposDoFormularioTests(ApiFixture fixture) => _fixture = fixture;

    private Task<HttpClient> ProfissionalAsync(string usuario)
        => _fixture.ClienteDeAsync(usuario, $"Campos {usuario}", FuncaoProfissional.Coordenacao);

    private static async Task<HttpResponseMessage> CriarAsync(
        HttpClient client, object dadosPaciente)
    {
        var bases = await client.GetFromJsonAsync<List<BaseDto>>("/api/bases", Json);

        return await client.PostAsJsonAsync("/api/atendimentos", new
        {
            baseId = bases![0].Id,
            paciente = dadosPaciente
        }, Json);
    }

    private static async Task<string> CodigoAsync(HttpClient client)
        => (await client.GetFromJsonAsync<CodigoNovoDto>("/api/pacientes/codigo-novo", Json))!.Codigo;

    // -----------------------------------------------------------------------
    // Identificacao
    // -----------------------------------------------------------------------

    [SkippableFact]
    public async Task Guarda_raca_cor_etnia_polo_base_e_dsei()
    {
        Skip.IfNot(ApiFixture.BancoDisponivel, "ATENDIMENTO_TEST_DB nao configurado.");

        var client = await ProfissionalAsync("form.identificacao");

        var resposta = await CriarAsync(client, new
        {
            codigo = await CodigoAsync(client),
            nome = "Paciente Da Aldeia",
            tipoDocumento = "SemDocumento",
            idadeAproximada = 30,
            sexo = "NaoInformado",
            statusAlergia = "NaoPerguntado",
            consentimentoRegistro = true,
            racaCor = "Indigena",
            etnia = "Yanomami",
            poloBase = "Polo Base Surucucu",
            dsei = "DSEI Yanomami",
            municipioNascimento = "Alto Alegre",
            paisNascimento = "Brasil",
            estadoResidencia = "RR"
        });

        resposta.EnsureSuccessStatusCode();

        var prontuario = (await resposta.Content.ReadFromJsonAsync<ProntuarioDto>(Json))!;

        // Raca/cor e etnia sao campos distintos: a lista fechada do IBGE nao
        // comporta o povo, e juntar as duas apagaria justamente o dado que
        // orienta atendimento a populacao indigena.
        Assert.Equal(RacaCor.Indigena, prontuario.Paciente.RacaCor);
        Assert.Equal("Yanomami", prontuario.Paciente.Etnia);
        Assert.Equal("Polo Base Surucucu", prontuario.Paciente.PoloBase);
        Assert.Equal("DSEI Yanomami", prontuario.Paciente.Dsei);
        Assert.Equal("Alto Alegre", prontuario.Paciente.MunicipioNascimento);
        Assert.Equal("RR", prontuario.Paciente.EstadoResidencia);
    }

    [SkippableFact]
    public async Task O_cpf_convive_com_o_documento_e_com_o_cartao_do_sus()
    {
        Skip.IfNot(ApiFixture.BancoDisponivel, "ATENDIMENTO_TEST_DB nao configurado.");

        var client = await ProfissionalAsync("form.cpf");

        var resposta = await CriarAsync(client, new
        {
            codigo = await CodigoAsync(client),
            nome = "Paciente Com Tres Numeros",
            tipoDocumento = "Rg",
            numeroDocumento = "12.345.678-9",
            cartaoSus = "700123456789012",
            cpf = "123.456.789-00",
            idadeAproximada = 40,
            sexo = "NaoInformado",
            statusAlergia = "NaoPerguntado",
            consentimentoRegistro = true
        });

        resposta.EnsureSuccessStatusCode();

        var paciente = (await resposta.Content.ReadFromJsonAsync<ProntuarioDto>(Json))!.Paciente;

        // Como tipo de documento, o CPF excluiria o RG — o mesmo problema que o
        // cartao do SUS ja teve. O formulario de papel traz os tres em linhas
        // separadas.
        Assert.Equal("12.345.678-9", paciente.NumeroDocumento);
        Assert.Equal("700123456789012", paciente.CartaoSus);
        Assert.Equal("123.456.789-00", paciente.Cpf);
    }

    [SkippableFact]
    public async Task Sem_informar_raca_cor_o_paciente_fica_como_nao_informado()
    {
        Skip.IfNot(ApiFixture.BancoDisponivel, "ATENDIMENTO_TEST_DB nao configurado.");

        var client = await ProfissionalAsync("form.semraca");

        var resposta = await CriarAsync(client, new
        {
            codigo = await CodigoAsync(client),
            nome = "Paciente Que Nao Declarou",
            tipoDocumento = "SemDocumento",
            idadeAproximada = 25,
            sexo = "NaoInformado",
            statusAlergia = "NaoPerguntado",
            consentimentoRegistro = true
        });

        resposta.EnsureSuccessStatusCode();

        var paciente = (await resposta.Content.ReadFromJsonAsync<ProntuarioDto>(Json))!.Paciente;

        // A pergunta e autodeclarada: chutar por aparencia e pior que nao ter.
        Assert.Equal(RacaCor.NaoInformado, paciente.RacaCor);
        Assert.Null(paciente.Etnia);
    }

    // -----------------------------------------------------------------------
    // Triagem
    // -----------------------------------------------------------------------

    private async Task<Guid> AtendimentoAsync(HttpClient client, string nome)
    {
        var resposta = await CriarAsync(client, new
        {
            codigo = await CodigoAsync(client),
            nome,
            tipoDocumento = "SemDocumento",
            idadeAproximada = 3,
            sexo = "NaoInformado",
            statusAlergia = "NaoPerguntado",
            consentimentoRegistro = true,
            nomeDaMae = "Maria Silva",
            endereco = "Rua das Flores, 10"
        });

        resposta.EnsureSuccessStatusCode();
        return (await resposta.Content.ReadFromJsonAsync<ProntuarioDto>(Json))!.Id;
    }

    [SkippableFact]
    public async Task Triagem_guarda_perimetro_cefalico_e_testes_rapidos()
    {
        Skip.IfNot(ApiFixture.BancoDisponivel, "ATENDIMENTO_TEST_DB nao configurado.");

        var client = await ProfissionalAsync("form.triagem");
        var id = await AtendimentoAsync(client, "Crianca Com Perimetro");

        (await client.PutAsJsonAsync($"/api/atendimentos/{id}/triagem", new
        {
            classificacaoRisco = "Verde",
            statusAlergia = "SemAlergiaConhecida",
            circunferenciaCefalicaCm = 48.5,
            testeRapidoCovid = "Negativo",
            testeRapidoMalaria = "Positivo"
        }, Json)).EnsureSuccessStatusCode();

        var prontuario = await client.GetFromJsonAsync<ProntuarioDto>(
            $"/api/atendimentos/{id}", Json);

        Assert.Equal(48.5, prontuario!.Triagem!.CircunferenciaCefalicaCm);
        Assert.Equal(ResultadoTesteRapido.Negativo, prontuario.Triagem.TesteRapidoCovid);
        Assert.Equal(ResultadoTesteRapido.Positivo, prontuario.Triagem.TesteRapidoMalaria);
    }

    [SkippableFact]
    public async Task Teste_nao_feito_fica_nulo()
    {
        Skip.IfNot(ApiFixture.BancoDisponivel, "ATENDIMENTO_TEST_DB nao configurado.");

        var client = await ProfissionalAsync("form.semteste");
        var id = await AtendimentoAsync(client, "Crianca Sem Teste");

        (await client.PutAsJsonAsync($"/api/atendimentos/{id}/triagem", new
        {
            classificacaoRisco = "Verde",
            statusAlergia = "SemAlergiaConhecida"
        }, Json)).EnsureSuccessStatusCode();

        var prontuario = await client.GetFromJsonAsync<ProntuarioDto>(
            $"/api/atendimentos/{id}", Json);

        // O nulo ja diz "nao foi feito": um terceiro valor no enum criaria duas
        // formas de dizer a mesma coisa.
        Assert.Null(prontuario!.Triagem!.TesteRapidoCovid);
        Assert.Null(prontuario.Triagem.TesteRapidoMalaria);
    }

    [SkippableFact]
    public async Task Quem_nao_teve_cirurgia_nao_guarda_lista_de_cirurgias()
    {
        Skip.IfNot(ApiFixture.BancoDisponivel, "ATENDIMENTO_TEST_DB nao configurado.");

        var client = await ProfissionalAsync("form.cirurgia");
        var id = await AtendimentoAsync(client, "Crianca Sem Cirurgia");

        (await client.PutAsJsonAsync($"/api/atendimentos/{id}/triagem", new
        {
            classificacaoRisco = "Verde",
            statusAlergia = "SemAlergiaConhecida",
            teveCirurgiaPrevia = false,
            cirurgiasPrevias = "Apendicectomia"
        }, Json)).EnsureSuccessStatusCode();

        var prontuario = await client.GetFromJsonAsync<ProntuarioDto>(
            $"/api/atendimentos/{id}", Json);

        // "Quais" preenchido junto de "nao teve" e contradicao gravada, e
        // alguem vai ler so um dos dois.
        Assert.False(prontuario!.Triagem!.TeveCirurgiaPrevia);
        Assert.Null(prontuario.Triagem.CirurgiasPrevias);
    }

    [SkippableFact]
    public async Task Quem_teve_cirurgia_guarda_quais()
    {
        Skip.IfNot(ApiFixture.BancoDisponivel, "ATENDIMENTO_TEST_DB nao configurado.");

        var client = await ProfissionalAsync("form.teveCirurgia");
        var id = await AtendimentoAsync(client, "Crianca Operada");

        (await client.PutAsJsonAsync($"/api/atendimentos/{id}/triagem", new
        {
            classificacaoRisco = "Verde",
            statusAlergia = "SemAlergiaConhecida",
            teveCirurgiaPrevia = true,
            cirurgiasPrevias = "Apendicectomia em 2019"
        }, Json)).EnsureSuccessStatusCode();

        var prontuario = await client.GetFromJsonAsync<ProntuarioDto>(
            $"/api/atendimentos/{id}", Json);

        Assert.True(prontuario!.Triagem!.TeveCirurgiaPrevia);
        Assert.Equal("Apendicectomia em 2019", prontuario.Triagem.CirurgiasPrevias);
    }

    [SkippableFact]
    public async Task Nao_perguntar_sobre_cirurgia_e_diferente_de_responder_que_nao()
    {
        Skip.IfNot(ApiFixture.BancoDisponivel, "ATENDIMENTO_TEST_DB nao configurado.");

        var client = await ProfissionalAsync("form.cirurgiaNula");
        var id = await AtendimentoAsync(client, "Crianca Nao Perguntada");

        (await client.PutAsJsonAsync($"/api/atendimentos/{id}/triagem", new
        {
            classificacaoRisco = "Verde",
            statusAlergia = "SemAlergiaConhecida"
        }, Json)).EnsureSuccessStatusCode();

        var prontuario = await client.GetFromJsonAsync<ProntuarioDto>(
            $"/api/atendimentos/{id}", Json);

        // A mesma distincao que a alergia ja fazia.
        Assert.Null(prontuario!.Triagem!.TeveCirurgiaPrevia);
    }

    [SkippableFact]
    public async Task Perimetro_cefalico_fora_da_faixa_e_recusado()
    {
        Skip.IfNot(ApiFixture.BancoDisponivel, "ATENDIMENTO_TEST_DB nao configurado.");

        var client = await ProfissionalAsync("form.perimetro");
        var id = await AtendimentoAsync(client, "Crianca Com Medida Errada");

        var resposta = await client.PutAsJsonAsync($"/api/atendimentos/{id}/triagem", new
        {
            classificacaoRisco = "Verde",
            statusAlergia = "SemAlergiaConhecida",
            circunferenciaCefalicaCm = 300
        }, Json);

        Assert.Equal(HttpStatusCode.BadRequest, resposta.StatusCode);
    }

    // -----------------------------------------------------------------------
    // Consulta
    // -----------------------------------------------------------------------

    [SkippableFact]
    public async Task Consulta_guarda_historia_exame_fisico_e_orientacoes()
    {
        Skip.IfNot(ApiFixture.BancoDisponivel, "ATENDIMENTO_TEST_DB nao configurado.");

        var client = await ProfissionalAsync("form.consulta");
        var id = await AtendimentoAsync(client, "Crianca Da Consulta");

        (await client.PutAsJsonAsync($"/api/atendimentos/{id}/consulta", new
        {
            especialidade = "ClinicaGeral",
            sintomasDescricao = "Dor abdominal ha dois dias.",
            historiaClinica = "Sem internacoes previas. Vacinacao em dia.",
            exameFisico = "Abdome flacido, doloroso a palpacao em FID.",
            cid10Codigo = "M79.1",
            conduta = "Analgesia e reavaliacao em 24h.",
            orientacoesGerais = "Retornar se piora da dor ou febre.",
            desfecho = "Alta"
        }, Json)).EnsureSuccessStatusCode();

        var prontuario = await client.GetFromJsonAsync<ProntuarioDto>(
            $"/api/atendimentos/{id}", Json);

        var consulta = prontuario!.Consultas.Single();

        // Blocos proprios, e nao um pedaco da descricao dos sintomas: sao
        // perguntas diferentes, e juntar as tres numa caixa so faz as duas
        // ultimas deixarem de ser preenchidas.
        Assert.Equal("Sem internacoes previas. Vacinacao em dia.", consulta.HistoriaClinica);
        Assert.Equal("Abdome flacido, doloroso a palpacao em FID.", consulta.ExameFisico);
        Assert.Equal("Retornar se piora da dor ou febre.", consulta.OrientacoesGerais);
    }

    [SkippableFact]
    public async Task Tabagismo_entra_como_condicao_cronica()
    {
        Skip.IfNot(ApiFixture.BancoDisponivel, "ATENDIMENTO_TEST_DB nao configurado.");

        var client = await ProfissionalAsync("form.tabagista");

        var resposta = await CriarAsync(client, new
        {
            codigo = await CodigoAsync(client),
            nome = "Paciente Tabagista",
            tipoDocumento = "SemDocumento",
            idadeAproximada = 55,
            sexo = "NaoInformado",
            statusAlergia = "NaoPerguntado",
            consentimentoRegistro = true,
            condicoesCronicas = new[] { "Hipertensao", "Tabagista" }
        });

        resposta.EnsureSuccessStatusCode();

        var paciente = (await resposta.Content.ReadFromJsonAsync<ProntuarioDto>(Json))!.Paciente;

        // Consta dos antecedentes de todos os formularios, ao lado de HAS e DM.
        Assert.Contains(CondicaoCronica.Tabagista, paciente.CondicoesCronicas);
        Assert.Contains(CondicaoCronica.Hipertensao, paciente.CondicoesCronicas);
    }
}
