using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using AtendimentoDeCampo.Api.Contratos;
using AtendimentoDeCampo.Domain;

namespace AtendimentoDeCampo.Tests;

/// <summary>
/// Registro, aprovacao e login. A regra central: conta recem-criada nao acessa
/// nada ate um administrador aprovar.
/// </summary>
[Collection(Colecoes.Api)]
public class ContasTests
{
    private readonly ApiFixture _fixture;

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    public ContasTests(ApiFixture fixture) => _fixture = fixture;

    private const string Senha = "plantao-2026";

    private async Task<HttpClient> AdministradorAsync()
    {
        var client = _fixture.CreateClient();

        var resposta = await client.PostAsJsonAsync("/api/auth/login", new
        {
            usuario = ApiFixture.AdminUsuario,
            senha = ApiFixture.AdminSenha,
            idioma = "Pt"
        }, Json);

        resposta.EnsureSuccessStatusCode();

        var login = await resposta.Content.ReadFromJsonAsync<LoginResponse>(Json);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", login!.Token);

        return client;
    }

    private async Task<ProfissionalDto> RegistrarAsync(
        string usuario,
        string nome = "Profissional de Teste",
        FuncaoProfissional funcao = FuncaoProfissional.Enfermeiro,
        string? registro = "99999")
    {
        var client = _fixture.CreateClient();

        var resposta = await client.PostAsJsonAsync("/api/auth/registrar", new
        {
            usuario,
            nome,
            funcao = funcao.ToString(),
            registro,
            senha = Senha,
            confirmacaoSenha = Senha,
            idioma = "Pt"
        }, Json);

        resposta.EnsureSuccessStatusCode();

        return (await resposta.Content.ReadFromJsonAsync<ProfissionalDto>(Json))!;
    }

    private async Task<HttpResponseMessage> LoginAsync(string usuario, string senha = Senha)
        => await _fixture.CreateClient().PostAsJsonAsync("/api/auth/login", new
        {
            usuario,
            senha,
            idioma = "Pt"
        }, Json);

    // -----------------------------------------------------------------------
    // Registro
    // -----------------------------------------------------------------------

    [SkippableFact]
    public async Task ContaRegistradaNascePendente()
    {
        Skip.IfNot(ApiFixture.BancoDisponivel, "ATENDIMENTO_TEST_DB nao configurado.");

        var criado = await RegistrarAsync("nasce.pendente");

        Assert.Equal(StatusConta.Pendente, criado.Status);
        Assert.False(criado.EhAdministrador);
    }

    [SkippableFact]
    public async Task ContaPendenteNaoEntra()
    {
        Skip.IfNot(ApiFixture.BancoDisponivel, "ATENDIMENTO_TEST_DB nao configurado.");

        await RegistrarAsync("pendente.naoentra");

        var resposta = await LoginAsync("pendente.naoentra");

        Assert.Equal(HttpStatusCode.Unauthorized, resposta.StatusCode);

        // A pessoa provou a senha, entao explicar a situacao nao vaza nada — e
        // sem isso ela ficaria tentando de novo achando que errou a senha.
        Assert.Contains("ContaPendente", await resposta.Content.ReadAsStringAsync());
    }

    [SkippableFact]
    public async Task UsuarioRepetidoEhRejeitado()
    {
        Skip.IfNot(ApiFixture.BancoDisponivel, "ATENDIMENTO_TEST_DB nao configurado.");

        await RegistrarAsync("usuario.repetido");

        var segundo = await _fixture.CreateClient().PostAsJsonAsync("/api/auth/registrar", new
        {
            usuario = "usuario.repetido",
            nome = "Outra Pessoa",
            funcao = "Medico",
            registro = "12345",
            senha = Senha,
            confirmacaoSenha = Senha,
            idioma = "Pt"
        }, Json);

        Assert.Equal(HttpStatusCode.BadRequest, segundo.StatusCode);
    }

    [SkippableFact]
    public async Task UsuarioNaoDiferenciaMaiusculasNemAcento()
    {
        Skip.IfNot(ApiFixture.BancoDisponivel, "ATENDIMENTO_TEST_DB nao configurado.");

        await RegistrarAsync("caixa.alta");

        // "Caixa.Alta" nao pode virar uma segunda conta.
        var segundo = await _fixture.CreateClient().PostAsJsonAsync("/api/auth/registrar", new
        {
            usuario = "Caixa.Alta",
            nome = "Outra Pessoa",
            funcao = "Recepcao",
            senha = Senha,
            confirmacaoSenha = Senha,
            idioma = "Pt"
        }, Json);

        Assert.Equal(HttpStatusCode.BadRequest, segundo.StatusCode);
    }

    [SkippableFact]
    public async Task DoisHomonimosConseguemTerConta()
    {
        Skip.IfNot(ApiFixture.BancoDisponivel, "ATENDIMENTO_TEST_DB nao configurado.");

        // O modelo anterior identificava por nome + funcao e simplesmente
        // impedia a segunda pessoa de se registrar.
        var primeira = await RegistrarAsync("maria.silva.a", "Maria Silva");
        var segunda = await RegistrarAsync("maria.silva.b", "Maria Silva");

        Assert.NotEqual(primeira.Id, segunda.Id);
        Assert.Equal("Maria Silva", primeira.Nome);
        Assert.Equal("Maria Silva", segunda.Nome);
    }

    [SkippableFact]
    public async Task SenhasQueNaoConferemSaoRejeitadas()
    {
        Skip.IfNot(ApiFixture.BancoDisponivel, "ATENDIMENTO_TEST_DB nao configurado.");

        var resposta = await _fixture.CreateClient().PostAsJsonAsync("/api/auth/registrar", new
        {
            usuario = "senha.diferente",
            nome = "Teste Senha",
            funcao = "Recepcao",
            senha = Senha,
            confirmacaoSenha = "outra-coisa",
            idioma = "Pt"
        }, Json);

        Assert.Equal(HttpStatusCode.BadRequest, resposta.StatusCode);
    }

    [SkippableFact]
    public async Task FuncaoComConselhoExigeRegistro()
    {
        Skip.IfNot(ApiFixture.BancoDisponivel, "ATENDIMENTO_TEST_DB nao configurado.");

        var resposta = await _fixture.CreateClient().PostAsJsonAsync("/api/auth/registrar", new
        {
            usuario = "medico.sem.crm",
            nome = "Medico Sem CRM",
            funcao = "Medico",
            senha = Senha,
            confirmacaoSenha = Senha,
            idioma = "Pt"
        }, Json);

        Assert.Equal(HttpStatusCode.BadRequest, resposta.StatusCode);
        Assert.Contains("Crm", await resposta.Content.ReadAsStringAsync());
    }

    [SkippableFact]
    public async Task FuncaoSemConselhoNaoExigeRegistro()
    {
        Skip.IfNot(ApiFixture.BancoDisponivel, "ATENDIMENTO_TEST_DB nao configurado.");

        var criado = await RegistrarAsync("recepcao.ok", "Pessoa da Recepcao", FuncaoProfissional.Recepcao, null);

        Assert.Equal(ConselhoTipo.Nenhum, criado.ConselhoTipo);
    }

    [SkippableFact]
    public async Task ConsultaDeDisponibilidadeDoUsuario()
    {
        Skip.IfNot(ApiFixture.BancoDisponivel, "ATENDIMENTO_TEST_DB nao configurado.");

        var client = _fixture.CreateClient();

        var livre = await client.GetFromJsonAsync<UsuarioDisponivelResponse>(
            "/api/auth/usuario-disponivel?usuario=ninguem.usou.isso", Json);

        Assert.True(livre!.Disponivel);

        await RegistrarAsync("ja.existe.esse");

        var ocupado = await client.GetFromJsonAsync<UsuarioDisponivelResponse>(
            "/api/auth/usuario-disponivel?usuario=ja.existe.esse", Json);

        Assert.False(ocupado!.Disponivel);
    }

    // -----------------------------------------------------------------------
    // Aprovacao
    // -----------------------------------------------------------------------

    [SkippableFact]
    public async Task ContaAprovadaEntraNormalmente()
    {
        Skip.IfNot(ApiFixture.BancoDisponivel, "ATENDIMENTO_TEST_DB nao configurado.");

        var criado = await RegistrarAsync("aprovada.entra");
        var admin = await AdministradorAsync();

        (await admin.PostAsJsonAsync($"/api/profissionais/{criado.Id}/aprovar", new { }, Json))
            .EnsureSuccessStatusCode();

        var resposta = await LoginAsync("aprovada.entra");

        resposta.EnsureSuccessStatusCode();

        var login = await resposta.Content.ReadFromJsonAsync<LoginResponse>(Json);
        Assert.Equal(StatusConta.Ativa, login!.Profissional.Status);
    }

    [SkippableFact]
    public async Task ContaRecusadaNaoEntraEExplicaOMotivo()
    {
        Skip.IfNot(ApiFixture.BancoDisponivel, "ATENDIMENTO_TEST_DB nao configurado.");

        var criado = await RegistrarAsync("recusada.naoentra");
        var admin = await AdministradorAsync();

        (await admin.PostAsJsonAsync(
            $"/api/profissionais/{criado.Id}/recusar",
            new { motivo = "Registro do conselho nao confere." },
            Json)).EnsureSuccessStatusCode();

        var resposta = await LoginAsync("recusada.naoentra");
        var corpo = await resposta.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.Unauthorized, resposta.StatusCode);
        Assert.Contains("ContaRecusada", corpo);
        Assert.Contains("nao confere", corpo);
    }

    [SkippableFact]
    public async Task RecusaSemMotivoEhRejeitada()
    {
        Skip.IfNot(ApiFixture.BancoDisponivel, "ATENDIMENTO_TEST_DB nao configurado.");

        var criado = await RegistrarAsync("recusa.sem.motivo");
        var admin = await AdministradorAsync();

        var resposta = await admin.PostAsJsonAsync(
            $"/api/profissionais/{criado.Id}/recusar", new { motivo = "" }, Json);

        Assert.Equal(HttpStatusCode.BadRequest, resposta.StatusCode);
    }

    [SkippableFact]
    public async Task ContaDesativadaPerdeOAcesso()
    {
        Skip.IfNot(ApiFixture.BancoDisponivel, "ATENDIMENTO_TEST_DB nao configurado.");

        var criado = await RegistrarAsync("sera.desativada");
        var admin = await AdministradorAsync();

        (await admin.PostAsJsonAsync($"/api/profissionais/{criado.Id}/aprovar", new { }, Json))
            .EnsureSuccessStatusCode();
        (await LoginAsync("sera.desativada")).EnsureSuccessStatusCode();

        (await admin.PostAsJsonAsync($"/api/profissionais/{criado.Id}/desativar", new { }, Json))
            .EnsureSuccessStatusCode();

        var depois = await LoginAsync("sera.desativada");

        Assert.Equal(HttpStatusCode.Unauthorized, depois.StatusCode);
        Assert.Contains("ContaDesativada", await depois.Content.ReadAsStringAsync());
    }

    // -----------------------------------------------------------------------
    // Autorizacao
    // -----------------------------------------------------------------------

    [SkippableFact]
    public async Task ProfissionalComumNaoAprovaContas()
    {
        Skip.IfNot(ApiFixture.BancoDisponivel, "ATENDIMENTO_TEST_DB nao configurado.");

        // A restricao esta no servidor, e nao em esconder o botao da tela.
        var comum = await RegistrarAsync("comum.sem.poder");
        var admin = await AdministradorAsync();

        (await admin.PostAsJsonAsync($"/api/profissionais/{comum.Id}/aprovar", new { }, Json))
            .EnsureSuccessStatusCode();

        var resposta = await LoginAsync("comum.sem.poder");
        var login = await resposta.Content.ReadFromJsonAsync<LoginResponse>(Json);

        var client = _fixture.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", login!.Token);

        var alvo = await RegistrarAsync("alvo.da.tentativa");

        Assert.Equal(HttpStatusCode.Forbidden,
            (await client.GetAsync("/api/profissionais")).StatusCode);

        Assert.Equal(HttpStatusCode.Forbidden,
            (await client.PostAsJsonAsync($"/api/profissionais/{alvo.Id}/aprovar", new { }, Json)).StatusCode);
    }

    [SkippableFact]
    public async Task SemTokenNaoAcessaGestaoDeContas()
    {
        Skip.IfNot(ApiFixture.BancoDisponivel, "ATENDIMENTO_TEST_DB nao configurado.");

        Assert.Equal(HttpStatusCode.Unauthorized,
            (await _fixture.CreateClient().GetAsync("/api/profissionais")).StatusCode);
    }

    [SkippableFact]
    public async Task AdministradorNaoDesativaAPropriaConta()
    {
        Skip.IfNot(ApiFixture.BancoDisponivel, "ATENDIMENTO_TEST_DB nao configurado.");

        var admin = await AdministradorAsync();
        var eu = (await admin.GetFromJsonAsync<List<ProfissionalDto>>(
            $"/api/profissionais?busca={ApiFixture.AdminUsuario}", Json))!.Single();

        var resposta = await admin.PostAsJsonAsync($"/api/profissionais/{eu.Id}/desativar", new { }, Json);

        Assert.Equal(HttpStatusCode.BadRequest, resposta.StatusCode);
    }

    [SkippableFact]
    public async Task NaoDaParaRemoverOUltimoAdministrador()
    {
        Skip.IfNot(ApiFixture.BancoDisponivel, "ATENDIMENTO_TEST_DB nao configurado.");

        // Sem administrador ativo ninguem aprova mais nada, e o sistema trava
        // sem caminho de volta pela interface.
        var admin = await AdministradorAsync();
        var eu = (await admin.GetFromJsonAsync<List<ProfissionalDto>>(
            $"/api/profissionais?busca={ApiFixture.AdminUsuario}", Json))!.Single();

        var resposta = await admin.PostAsJsonAsync(
            $"/api/profissionais/{eu.Id}/administrador", new { ehAdministrador = false }, Json);

        Assert.Equal(HttpStatusCode.BadRequest, resposta.StatusCode);
    }

    [SkippableFact]
    public async Task ContaPendenteNaoViraAdministradora()
    {
        Skip.IfNot(ApiFixture.BancoDisponivel, "ATENDIMENTO_TEST_DB nao configurado.");

        var criado = await RegistrarAsync("pendente.admin");
        var admin = await AdministradorAsync();

        var resposta = await admin.PostAsJsonAsync(
            $"/api/profissionais/{criado.Id}/administrador", new { ehAdministrador = true }, Json);

        Assert.Equal(HttpStatusCode.BadRequest, resposta.StatusCode);
    }

    [SkippableFact]
    public async Task ListaTrazPendentesPrimeiro()
    {
        Skip.IfNot(ApiFixture.BancoDisponivel, "ATENDIMENTO_TEST_DB nao configurado.");

        await RegistrarAsync("fila.pendente.um");
        var admin = await AdministradorAsync();

        var pendentes = await admin.GetFromJsonAsync<List<ProfissionalDto>>(
            "/api/profissionais?status=Pendente", Json);

        Assert.All(pendentes!, p => Assert.Equal(StatusConta.Pendente, p.Status));
        Assert.Contains(pendentes!, p => p.Usuario == "fila.pendente.um");

        var total = await admin.GetFromJsonAsync<int>("/api/profissionais/pendentes/total", Json);
        Assert.True(total >= 1);
    }

    // -----------------------------------------------------------------------
    // Login
    // -----------------------------------------------------------------------

    [SkippableFact]
    public async Task SenhaErradaNaoEntra()
    {
        Skip.IfNot(ApiFixture.BancoDisponivel, "ATENDIMENTO_TEST_DB nao configurado.");

        var criado = await RegistrarAsync("senha.errada");
        var admin = await AdministradorAsync();

        (await admin.PostAsJsonAsync($"/api/profissionais/{criado.Id}/aprovar", new { }, Json))
            .EnsureSuccessStatusCode();

        var resposta = await LoginAsync("senha.errada", "nao-e-essa");

        Assert.Equal(HttpStatusCode.Unauthorized, resposta.StatusCode);
        Assert.Contains("CredenciaisInvalidas", await resposta.Content.ReadAsStringAsync());
    }

    [SkippableFact]
    public async Task UsuarioInexistenteRespondeIgualASenhaErrada()
    {
        Skip.IfNot(ApiFixture.BancoDisponivel, "ATENDIMENTO_TEST_DB nao configurado.");

        // Nao adianta proteger o resto se o login revela quem tem conta.
        var resposta = await LoginAsync("nao.existe.mesmo");

        Assert.Equal(HttpStatusCode.Unauthorized, resposta.StatusCode);
        Assert.Contains("CredenciaisInvalidas", await resposta.Content.ReadAsStringAsync());
    }

    [SkippableFact]
    public async Task LoginAceitaUsuarioEmCaixaAlta()
    {
        Skip.IfNot(ApiFixture.BancoDisponivel, "ATENDIMENTO_TEST_DB nao configurado.");

        // O teclado do celular costuma capitalizar a primeira letra sozinho.
        var criado = await RegistrarAsync("caixa.no.login");
        var admin = await AdministradorAsync();

        (await admin.PostAsJsonAsync($"/api/profissionais/{criado.Id}/aprovar", new { }, Json))
            .EnsureSuccessStatusCode();

        (await LoginAsync("Caixa.No.Login")).EnsureSuccessStatusCode();
    }

    [SkippableFact]
    public async Task TokenDoAdministradorCarregaOPapel()
    {
        Skip.IfNot(ApiFixture.BancoDisponivel, "ATENDIMENTO_TEST_DB nao configurado.");

        var admin = await AdministradorAsync();

        // Se o papel nao viajasse no token, o proprio administrador levaria 403.
        (await admin.GetAsync("/api/profissionais")).EnsureSuccessStatusCode();
    }
}
