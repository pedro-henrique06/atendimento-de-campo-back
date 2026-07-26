using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using AtendimentoDeCampo.Api.Contratos;

namespace AtendimentoDeCampo.Tests;

/// <summary>
/// O codigo do paciente e o unico jeito de reencontrar quem nao tem documento —
/// a maioria em campo. Estes testes cobrem a volta: mesma pessoa, visita nova,
/// historico preservado.
/// </summary>
[Collection(Colecoes.Api)]
public class PacienteQueRetornaTests
{
    private readonly ApiFixture _fixture;

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    public PacienteQueRetornaTests(ApiFixture fixture) => _fixture = fixture;

    private async Task<HttpClient> AutenticarAsync()
    {
        var client = _fixture.CreateClient();
        const string senha = "plantao-2026";
        const string usuario = "retorno.teste";

        var registro = await client.PostAsJsonAsync("/api/auth/registrar", new
        {
            usuario,
            nome = "Retorno Teste",
            funcao = "Enfermeiro",
            registro = "99881",
            senha,
            confirmacaoSenha = senha,
            idioma = "Pt"
        }, Json);

        if (registro.IsSuccessStatusCode)
        {
            var criado = await registro.Content.ReadFromJsonAsync<ProfissionalDto>(Json);
            var admin = _fixture.CreateClient();

            var login = await (await admin.PostAsJsonAsync("/api/auth/login", new
            {
                usuario = ApiFixture.AdminUsuario,
                senha = ApiFixture.AdminSenha,
                idioma = "Pt"
            }, Json)).Content.ReadFromJsonAsync<LoginResponse>(Json);

            admin.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", login!.Token);
            (await admin.PostAsJsonAsync($"/api/profissionais/{criado!.Id}/aprovar", new { }, Json))
                .EnsureSuccessStatusCode();
        }

        var entrada = await client.PostAsJsonAsync("/api/auth/login", new { usuario, senha, idioma = "Pt" }, Json);
        entrada.EnsureSuccessStatusCode();

        var sessao = await entrada.Content.ReadFromJsonAsync<LoginResponse>(Json);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", sessao!.Token);

        return client;
    }

    private static async Task<Guid> BaseAsync(HttpClient client)
    {
        var bases = await client.GetFromJsonAsync<List<BaseDto>>("/api/bases", Json);
        return bases![0].Id;
    }

    private static async Task<ProntuarioDto> AbrirAsync(
        HttpClient client,
        Guid baseId,
        string codigo,
        string nome,
        string queixa)
    {
        var resposta = await client.PostAsJsonAsync("/api/atendimentos", new
        {
            baseId,
            queixaPrincipal = queixa,
            paciente = new
            {
                codigo,
                nome,
                tipoDocumento = "SemDocumento",
                idadeAproximada = 34,
                sexo = "Feminino",
                statusAlergia = "SemAlergiaConhecida",
                consentimentoRegistro = true
            }
        }, Json);

        resposta.EnsureSuccessStatusCode();
        return (await resposta.Content.ReadFromJsonAsync<ProntuarioDto>(Json))!;
    }

    [SkippableFact]
    public async Task Codigo_novo_nao_cria_cadastro_antes_de_salvar()
    {
        Skip.IfNot(ApiFixture.BancoDisponivel, "ATENDIMENTO_TEST_DB nao configurado.");

        var client = await AutenticarAsync();
        var codigo = (await client.GetFromJsonAsync<CodigoNovoDto>("/api/pacientes/codigo-novo", Json))!.Codigo;

        // Sortear o codigo e so isso: nada gravado, porque o consentimento ainda
        // nao foi dado. Quem desiste da tela nao deixa rastro.
        var busca = await client.GetAsync($"/api/pacientes/codigo/{codigo}");

        Assert.Equal(HttpStatusCode.NotFound, busca.StatusCode);
    }

    [SkippableFact]
    public async Task Mesmo_codigo_na_volta_reaproveita_o_cadastro()
    {
        Skip.IfNot(ApiFixture.BancoDisponivel, "ATENDIMENTO_TEST_DB nao configurado.");

        var client = await AutenticarAsync();
        var baseId = await BaseAsync(client);
        var codigo = (await client.GetFromJsonAsync<CodigoNovoDto>("/api/pacientes/codigo-novo", Json))!.Codigo;

        var primeira = await AbrirAsync(client, baseId, codigo, "Yesenia Retorno", "Dor de cabeca");
        var segunda = await AbrirAsync(client, baseId, codigo, "Yesenia Retorno", "Tosse");

        // Dois atendimentos, um paciente so: e disso que depende o historico.
        Assert.NotEqual(primeira.Id, segunda.Id);
        Assert.NotEqual(primeira.Codigo, segunda.Codigo);
        Assert.Equal(primeira.Paciente.Id, segunda.Paciente.Id);
        Assert.Equal(codigo, segunda.Paciente.Codigo);
    }

    [SkippableFact]
    public async Task Sem_documento_e_sem_codigo_igual_sao_pessoas_diferentes()
    {
        Skip.IfNot(ApiFixture.BancoDisponivel, "ATENDIMENTO_TEST_DB nao configurado.");

        var client = await AutenticarAsync();
        var baseId = await BaseAsync(client);

        var umCodigo = (await client.GetFromJsonAsync<CodigoNovoDto>("/api/pacientes/codigo-novo", Json))!.Codigo;
        var outroCodigo = (await client.GetFromJsonAsync<CodigoNovoDto>("/api/pacientes/codigo-novo", Json))!.Codigo;

        // Homonimos sem documento sao comuns em campo. Juntar os dois cadastros
        // misturaria prontuario de gente diferente.
        var uma = await AbrirAsync(client, baseId, umCodigo, "Maria Silva", "Febre");
        var outra = await AbrirAsync(client, baseId, outroCodigo, "Maria Silva", "Corte no pe");

        Assert.NotEqual(uma.Paciente.Id, outra.Paciente.Id);
    }

    [SkippableFact]
    public async Task Encontra_o_paciente_pelo_codigo_de_um_atendimento_dele()
    {
        Skip.IfNot(ApiFixture.BancoDisponivel, "ATENDIMENTO_TEST_DB nao configurado.");

        var client = await AutenticarAsync();
        var baseId = await BaseAsync(client);
        var codigo = (await client.GetFromJsonAsync<CodigoNovoDto>("/api/pacientes/codigo-novo", Json))!.Codigo;

        var atendimento = await AbrirAsync(client, baseId, codigo, "Joao Confundido", "Dor lombar");

        // Os dois codigos circulam na mesma fila e alguem vai digitar um pelo
        // outro. Chegar na mesma pessoa e melhor que "nao encontrado".
        var achado = await client.GetFromJsonAsync<PacienteConhecidoDto>(
            $"/api/pacientes/codigo/{atendimento.Codigo}", Json);

        Assert.NotNull(achado);
        Assert.Equal(codigo, achado!.Paciente.Codigo);
        Assert.Equal("Joao Confundido", achado.Paciente.Nome);
    }

    [SkippableFact]
    public async Task Busca_aceita_o_codigo_como_a_pessoa_digita()
    {
        Skip.IfNot(ApiFixture.BancoDisponivel, "ATENDIMENTO_TEST_DB nao configurado.");

        var client = await AutenticarAsync();
        var baseId = await BaseAsync(client);
        var codigo = (await client.GetFromJsonAsync<CodigoNovoDto>("/api/pacientes/codigo-novo", Json))!.Codigo;

        await AbrirAsync(client, baseId, codigo, "Ana Digitada", "Dor de garganta");

        var semHifen = await client.GetFromJsonAsync<PacienteConhecidoDto>(
            $"/api/pacientes/codigo/{codigo.Replace("-", "")}", Json);

        Assert.Equal(codigo, semHifen!.Paciente.Codigo);
    }

    [SkippableFact]
    public async Task Conta_as_visitas_para_a_tela_confirmar_a_pessoa()
    {
        Skip.IfNot(ApiFixture.BancoDisponivel, "ATENDIMENTO_TEST_DB nao configurado.");

        var client = await AutenticarAsync();
        var baseId = await BaseAsync(client);
        var codigo = (await client.GetFromJsonAsync<CodigoNovoDto>("/api/pacientes/codigo-novo", Json))!.Codigo;

        await AbrirAsync(client, baseId, codigo, "Pedro Frequente", "Primeira");
        await AbrirAsync(client, baseId, codigo, "Pedro Frequente", "Segunda");

        var achado = await client.GetFromJsonAsync<PacienteConhecidoDto>(
            $"/api/pacientes/codigo/{codigo}", Json);

        Assert.Equal(2, achado!.TotalAtendimentos);
        Assert.NotNull(achado.UltimoAtendimentoEm);
        Assert.False(string.IsNullOrWhiteSpace(achado.UltimaBase));
    }

    [SkippableFact]
    public async Task Codigo_invalido_nao_abre_atendimento()
    {
        Skip.IfNot(ApiFixture.BancoDisponivel, "ATENDIMENTO_TEST_DB nao configurado.");

        var client = await AutenticarAsync();
        var baseId = await BaseAsync(client);

        var resposta = await client.PostAsJsonAsync("/api/atendimentos", new
        {
            baseId,
            paciente = new
            {
                codigo = "NAO-EHUM",
                nome = "Codigo Torto",
                tipoDocumento = "SemDocumento",
                idadeAproximada = 20,
                sexo = "NaoInformado",
                statusAlergia = "NaoPerguntado",
                consentimentoRegistro = true
            }
        }, Json);

        Assert.Equal(HttpStatusCode.BadRequest, resposta.StatusCode);
    }
}
