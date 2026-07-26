using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using AtendimentoDeCampo.Api.Contratos;

namespace AtendimentoDeCampo.Tests;

/// <summary>
/// Cadastro de bases pela coordenacao. As regras que importam vem todas do
/// codigo de atendimento: o prefixo entra nele e nao pode mudar depois que
/// codigos ja sairam, e base nao se apaga porque o historico aponta para ela.
/// </summary>
[Collection(Colecoes.Api)]
public class BasesTests
{
    private readonly ApiFixture _fixture;

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    public BasesTests(ApiFixture fixture) => _fixture = fixture;

    private async Task<HttpClient> AdministradorAsync()
    {
        var client = _fixture.CreateClient();

        var login = await (await client.PostAsJsonAsync("/api/auth/login", new
        {
            usuario = ApiFixture.AdminUsuario,
            senha = ApiFixture.AdminSenha,
            idioma = "Pt"
        }, Json)).Content.ReadFromJsonAsync<LoginResponse>(Json);

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", login!.Token);
        return client;
    }

    private async Task<HttpClient> EnfermeiroAsync()
    {
        var client = _fixture.CreateClient();
        const string senha = "plantao-2026";
        const string usuario = "bases.enfermeiro";

        var registro = await client.PostAsJsonAsync("/api/auth/registrar", new
        {
            usuario,
            nome = "Bases Enfermeiro",
            funcao = "Enfermeiro",
            registro = "77123",
            senha,
            confirmacaoSenha = senha,
            idioma = "Pt"
        }, Json);

        if (registro.IsSuccessStatusCode)
        {
            var criado = await registro.Content.ReadFromJsonAsync<ProfissionalDto>(Json);
            var admin = await AdministradorAsync();
            (await admin.PostAsJsonAsync($"/api/profissionais/{criado!.Id}/aprovar", new { }, Json))
                .EnsureSuccessStatusCode();
        }

        var login = await (await client.PostAsJsonAsync("/api/auth/login",
            new { usuario, senha, idioma = "Pt" }, Json)).Content.ReadFromJsonAsync<LoginResponse>(Json);

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", login!.Token);
        return client;
    }

    private static async Task<BaseAdminDto> CriarAsync(HttpClient client, string nome, string? prefixo = null)
    {
        var resposta = await client.PostAsJsonAsync("/api/bases", new { nome, prefixoCodigo = prefixo }, Json);
        resposta.EnsureSuccessStatusCode();
        return (await resposta.Content.ReadFromJsonAsync<BaseAdminDto>(Json))!;
    }

    [SkippableFact]
    public async Task So_a_coordenacao_mexe_nas_bases()
    {
        Skip.IfNot(ApiFixture.BancoDisponivel, "ATENDIMENTO_TEST_DB nao configurado.");

        var enfermeiro = await EnfermeiroAsync();

        // A tela some para quem nao e administrador, mas quem garante e o
        // servidor.
        Assert.Equal(HttpStatusCode.Forbidden,
            (await enfermeiro.GetAsync("/api/bases/todas")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden,
            (await enfermeiro.PostAsJsonAsync("/api/bases", new { nome = "Base Pirata" }, Json)).StatusCode);
    }

    [SkippableFact]
    public async Task Cria_derivando_o_prefixo_do_nome()
    {
        Skip.IfNot(ApiFixture.BancoDisponivel, "ATENDIMENTO_TEST_DB nao configurado.");

        var admin = await AdministradorAsync();
        var criada = await CriarAsync(admin, "Quinta Esperanca");

        Assert.Equal("QUI", criada.PrefixoCodigo);
        Assert.True(criada.Ativa);
        Assert.Equal(0, criada.TotalAtendimentos);
        Assert.True(criada.PrefixoEditavel);
    }

    [SkippableFact]
    public async Task Recusa_prefixo_ja_usado_por_outra_base()
    {
        Skip.IfNot(ApiFixture.BancoDisponivel, "ATENDIMENTO_TEST_DB nao configurado.");

        var admin = await AdministradorAsync();
        await CriarAsync(admin, "Refugio Norte", "RFN");

        // Dois codigos iguais apontariam para lugares diferentes.
        var repetida = await admin.PostAsJsonAsync("/api/bases",
            new { nome = "Refugio Nordeste", prefixoCodigo = "RFN" }, Json);

        Assert.Equal(HttpStatusCode.BadRequest, repetida.StatusCode);
    }

    [SkippableFact]
    public async Task Recusa_nome_repetido()
    {
        Skip.IfNot(ApiFixture.BancoDisponivel, "ATENDIMENTO_TEST_DB nao configurado.");

        var admin = await AdministradorAsync();
        await CriarAsync(admin, "Posto Ribeira", "PRI");

        // Duas entradas identicas na selecao e a equipe escolhendo a errada.
        var repetida = await admin.PostAsJsonAsync("/api/bases",
            new { nome = "posto ribeira", prefixoCodigo = "PRB" }, Json);

        Assert.Equal(HttpStatusCode.BadRequest, repetida.StatusCode);
    }

    [SkippableFact]
    public async Task Renomear_e_sempre_permitido()
    {
        Skip.IfNot(ApiFixture.BancoDisponivel, "ATENDIMENTO_TEST_DB nao configurado.");

        var admin = await AdministradorAsync();
        var criada = await CriarAsync(admin, "Nome Antigo", "NMA");

        var resposta = await admin.PutAsJsonAsync($"/api/bases/{criada.Id}",
            new { nome = "Nome Novo", prefixoCodigo = "NMA" }, Json);

        resposta.EnsureSuccessStatusCode();
        var atualizada = await resposta.Content.ReadFromJsonAsync<BaseAdminDto>(Json);

        Assert.Equal("Nome Novo", atualizada!.Nome);
        Assert.Equal("NMA", atualizada.PrefixoCodigo);
    }

    [SkippableFact]
    public async Task Prefixo_trava_depois_do_primeiro_atendimento()
    {
        Skip.IfNot(ApiFixture.BancoDisponivel, "ATENDIMENTO_TEST_DB nao configurado.");

        var admin = await AdministradorAsync();
        var criada = await CriarAsync(admin, "Abrigo Travado", "ABT");

        // Antes de qualquer atendimento, o prefixo ainda pode ser corrigido.
        var antes = await admin.PutAsJsonAsync($"/api/bases/{criada.Id}",
            new { nome = "Abrigo Travado", prefixoCodigo = "ABX" }, Json);
        antes.EnsureSuccessStatusCode();

        var codigo = (await admin.GetFromJsonAsync<CodigoNovoDto>("/api/pacientes/codigo-novo", Json))!.Codigo;

        (await admin.PostAsJsonAsync("/api/atendimentos", new
        {
            baseId = criada.Id,
            paciente = new
            {
                codigo,
                nome = "Paciente Da Base",
                tipoDocumento = "SemDocumento",
                idadeAproximada = 30,
                sexo = "NaoInformado",
                statusAlergia = "NaoPerguntado",
                consentimentoRegistro = true
            }
        }, Json)).EnsureSuccessStatusCode();

        // Agora existe um "ABX-XXXX" impresso em algum papel. Trocar o prefixo
        // faria o papel dizer uma base e o sistema dizer outra.
        var depois = await admin.PutAsJsonAsync($"/api/bases/{criada.Id}",
            new { nome = "Abrigo Travado", prefixoCodigo = "ABZ" }, Json);

        Assert.Equal(HttpStatusCode.BadRequest, depois.StatusCode);

        // Mas o nome continua livre.
        var renomear = await admin.PutAsJsonAsync($"/api/bases/{criada.Id}",
            new { nome = "Abrigo Renomeado", prefixoCodigo = "ABX" }, Json);
        renomear.EnsureSuccessStatusCode();
    }

    [SkippableFact]
    public async Task Nao_desativa_base_com_atendimento_aberto()
    {
        Skip.IfNot(ApiFixture.BancoDisponivel, "ATENDIMENTO_TEST_DB nao configurado.");

        var admin = await AdministradorAsync();
        var criada = await CriarAsync(admin, "Base Com Fila", "BCF");
        var codigo = (await admin.GetFromJsonAsync<CodigoNovoDto>("/api/pacientes/codigo-novo", Json))!.Codigo;

        (await admin.PostAsJsonAsync("/api/atendimentos", new
        {
            baseId = criada.Id,
            paciente = new
            {
                codigo,
                nome = "Ainda Na Fila",
                tipoDocumento = "SemDocumento",
                idadeAproximada = 25,
                sexo = "NaoInformado",
                statusAlergia = "NaoPerguntado",
                consentimentoRegistro = true
            }
        }, Json)).EnsureSuccessStatusCode();

        // A selecao de base so lista bases ativas: desativar agora sumiria com
        // um atendimento que ainda esta na fila.
        var resposta = await admin.PostAsJsonAsync($"/api/bases/{criada.Id}/ativa",
            new { ativa = false }, Json);

        Assert.Equal(HttpStatusCode.BadRequest, resposta.StatusCode);
    }

    [SkippableFact]
    public async Task Desativa_e_reativa_base_sem_fila()
    {
        Skip.IfNot(ApiFixture.BancoDisponivel, "ATENDIMENTO_TEST_DB nao configurado.");

        var admin = await AdministradorAsync();
        var criada = await CriarAsync(admin, "Base Temporaria", "BTP");

        var desativada = await admin.PostAsJsonAsync($"/api/bases/{criada.Id}/ativa",
            new { ativa = false }, Json);
        desativada.EnsureSuccessStatusCode();
        Assert.False((await desativada.Content.ReadFromJsonAsync<BaseAdminDto>(Json))!.Ativa);

        // Some da selecao, mas continua na gestao — nao foi apagada.
        var publicas = await admin.GetFromJsonAsync<List<BaseDto>>("/api/bases", Json);
        Assert.DoesNotContain(publicas!, b => b.Id == criada.Id);

        var todas = await admin.GetFromJsonAsync<List<BaseAdminDto>>("/api/bases/todas", Json);
        Assert.Contains(todas!, b => b.Id == criada.Id);

        var reativada = await admin.PostAsJsonAsync($"/api/bases/{criada.Id}/ativa",
            new { ativa = true }, Json);
        reativada.EnsureSuccessStatusCode();
        Assert.True((await reativada.Content.ReadFromJsonAsync<BaseAdminDto>(Json))!.Ativa);
    }

    [SkippableFact]
    public async Task Nao_deixa_a_operacao_sem_nenhuma_base_ativa()
    {
        Skip.IfNot(ApiFixture.BancoDisponivel, "ATENDIMENTO_TEST_DB nao configurado.");

        var admin = await AdministradorAsync();
        var ativasAntes = (await admin.GetFromJsonAsync<List<BaseAdminDto>>("/api/bases/todas", Json))!
            .Where(b => b.Ativa)
            .ToList();

        // Tenta desativar todas, uma a uma. Quantas cedem nao importa — algumas
        // vao recusar por terem fila aberta. O que se afirma e a invariante: por
        // mais que se insista, sobra pelo menos uma ativa. Sem nenhuma, ninguem
        // escolhe base e o app inteiro para, inclusive esta tela.
        foreach (var b in ativasAntes)
        {
            await admin.PostAsJsonAsync($"/api/bases/{b.Id}/ativa", new { ativa = false }, Json);
        }

        var depois = await admin.GetFromJsonAsync<List<BaseAdminDto>>("/api/bases/todas", Json);

        Assert.Contains(depois!, b => b.Ativa);

        // Devolve o estado para nao atrapalhar os outros testes da colecao.
        foreach (var b in ativasAntes)
        {
            await admin.PostAsJsonAsync($"/api/bases/{b.Id}/ativa", new { ativa = true }, Json);
        }
    }

    [SkippableFact]
    public async Task Sugere_prefixo_livre_quando_o_derivado_esta_tomado()
    {
        Skip.IfNot(ApiFixture.BancoDisponivel, "ATENDIMENTO_TEST_DB nao configurado.");

        var admin = await AdministradorAsync();
        await CriarAsync(admin, "Sucre Central", "SUC");

        // Sugerir um prefixo que ja vai dar erro seria pior que nao sugerir.
        var sugerido = await admin.GetFromJsonAsync<PrefixoSugeridoDto>(
            "/api/bases/prefixo-sugerido?nome=Sucre%20Norte", Json);

        Assert.NotEqual("SUC", sugerido!.Prefixo);
        Assert.Matches("^[A-Z]{3}$", sugerido.Prefixo);
    }

    [SkippableFact]
    public async Task Recusa_prefixo_fora_do_formato()
    {
        Skip.IfNot(ApiFixture.BancoDisponivel, "ATENDIMENTO_TEST_DB nao configurado.");

        var admin = await AdministradorAsync();

        var resposta = await admin.PostAsJsonAsync("/api/bases",
            new { nome = "Base Torta", prefixoCodigo = "A1" }, Json);

        Assert.Equal(HttpStatusCode.BadRequest, resposta.StatusCode);
    }
}
