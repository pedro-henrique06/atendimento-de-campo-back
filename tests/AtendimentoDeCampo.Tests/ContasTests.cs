using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using AtendimentoDeCampo.Api.Contratos;
using AtendimentoDeCampo.Domain;

namespace AtendimentoDeCampo.Tests;

/// <summary>
/// Cadastro de profissional, senha provisoria e login.
///
/// A regra central mudou: nao existe mais auto-registro. So a coordenacao
/// cadastra, a conta ja nasce ativa — quem cria e quem aprovaria — e a senha do
/// primeiro acesso e sorteada pelo sistema, nao escolhida por quem cadastra.
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

    private Task<HttpClient> AdministradorAsync() => _fixture.ClienteDoAdministradorAsync();

    /// <summary>Cadastra pela coordenacao e devolve a conta com a senha sorteada.</summary>
    private async Task<ContaCriadaDto> CriarAsync(
        string usuario,
        string nome = "Profissional de Teste",
        FuncaoProfissional funcao = FuncaoProfissional.Enfermeiro,
        string? registro = "99999")
    {
        var admin = await AdministradorAsync();

        var resposta = await admin.PostAsJsonAsync("/api/profissionais", new
        {
            usuario,
            nome,
            funcao = funcao.ToString(),
            registro,
            idioma = "Pt"
        }, Json);

        resposta.EnsureSuccessStatusCode();

        return (await resposta.Content.ReadFromJsonAsync<ContaCriadaDto>(Json))!;
    }

    private async Task<HttpResponseMessage> LoginAsync(string usuario, string senha)
        => await _fixture.CreateClient().PostAsJsonAsync("/api/auth/login", new
        {
            usuario,
            senha,
            idioma = "Pt"
        }, Json);

    /// <summary>Entra e devolve um client com o token, sem trocar a senha.</summary>
    private async Task<HttpClient> EntrarAsync(string usuario, string senha)
    {
        var client = _fixture.CreateClient();
        var resposta = await LoginAsync(usuario, senha);

        resposta.EnsureSuccessStatusCode();

        var login = await resposta.Content.ReadFromJsonAsync<LoginResponse>(Json);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", login!.Token);

        return client;
    }

    // -----------------------------------------------------------------------
    // Cadastro pela coordenacao
    // -----------------------------------------------------------------------

    [SkippableFact]
    public async Task Conta_cadastrada_pela_coordenacao_ja_nasce_ativa()
    {
        Skip.IfNot(ApiFixture.BancoDisponivel, "ATENDIMENTO_TEST_DB nao configurado.");

        var conta = await CriarAsync("nasce.ativa");

        // Deixar pendente exigiria que a coordenacao aprovasse o proprio
        // cadastro: um passo que nao decide nada.
        Assert.Equal(StatusConta.Ativa, conta.Profissional.Status);
        Assert.False(conta.Profissional.EhAdministrador);
    }

    [SkippableFact]
    public async Task Nao_existe_mais_auto_registro()
    {
        Skip.IfNot(ApiFixture.BancoDisponivel, "ATENDIMENTO_TEST_DB nao configurado.");

        // A rota saiu junto com o fluxo. Se voltar sem querer, alguem entra no
        // sistema sem passar pela coordenacao.
        var resposta = await _fixture.CreateClient().PostAsJsonAsync("/api/auth/registrar", new
        {
            usuario = "entrou.sozinho",
            nome = "Entrou Sozinho",
            funcao = "Enfermeiro",
            registro = "1",
            senha = "plantao-2026",
            confirmacaoSenha = "plantao-2026",
            idioma = "Pt"
        }, Json);

        Assert.Equal(HttpStatusCode.NotFound, resposta.StatusCode);
    }

    [SkippableFact]
    public async Task So_a_coordenacao_cadastra()
    {
        Skip.IfNot(ApiFixture.BancoDisponivel, "ATENDIMENTO_TEST_DB nao configurado.");

        var comum = await _fixture.ClienteDeAsync(
            "sem.poder.cadastrar", "Sem Poder", FuncaoProfissional.Enfermeiro, "55555");

        var resposta = await comum.PostAsJsonAsync("/api/profissionais", new
        {
            usuario = "criado.por.quem.nao.pode",
            nome = "Nao Deveria Existir",
            funcao = "Recepcao",
            idioma = "Pt"
        }, Json);

        Assert.Equal(HttpStatusCode.Forbidden, resposta.StatusCode);
    }

    [SkippableFact]
    public async Task A_senha_provisoria_e_sorteada_e_diferente_a_cada_conta()
    {
        Skip.IfNot(ApiFixture.BancoDisponivel, "ATENDIMENTO_TEST_DB nao configurado.");

        var uma = await CriarAsync("sorteio.um");
        var outra = await CriarAsync("sorteio.dois");

        // Se a coordenacao escolhesse, repetiria a mesma senha em todo mundo —
        // e uma senha compartilhada apaga a atribuicao do ato clinico.
        Assert.NotEqual(uma.SenhaProvisoria, outra.SenhaProvisoria);
        Assert.False(string.IsNullOrWhiteSpace(uma.SenhaProvisoria));
    }

    [SkippableFact]
    public async Task A_profissao_decide_a_fila_desde_o_cadastro()
    {
        Skip.IfNot(ApiFixture.BancoDisponivel, "ATENDIMENTO_TEST_DB nao configurado.");

        var conta = await CriarAsync(
            "dentista.cadastrado", "Dentista Cadastrado", FuncaoProfissional.Dentista, "3030");

        Assert.Equal([Especialidade.Odontologia], conta.Profissional.Filas);
    }

    [SkippableFact]
    public async Task Medico_sem_especialidade_nao_e_aceito_em_cadastro_novo()
    {
        Skip.IfNot(ApiFixture.BancoDisponivel, "ATENDIMENTO_TEST_DB nao configurado.");

        var admin = await AdministradorAsync();

        // Aceitar recriaria o problema: uma pessoa caindo em tres filas, e "a
        // fila dele" deixando de existir.
        var resposta = await admin.PostAsJsonAsync("/api/profissionais", new
        {
            usuario = "medico.generico",
            nome = "Medico Generico",
            funcao = "Medico",
            registro = "12345",
            idioma = "Pt"
        }, Json);

        Assert.Equal(HttpStatusCode.BadRequest, resposta.StatusCode);
        Assert.Contains("especialidade", await resposta.Content.ReadAsStringAsync());
    }

    [SkippableFact]
    public async Task Profissao_com_conselho_exige_registro()
    {
        Skip.IfNot(ApiFixture.BancoDisponivel, "ATENDIMENTO_TEST_DB nao configurado.");

        var admin = await AdministradorAsync();

        var resposta = await admin.PostAsJsonAsync("/api/profissionais", new
        {
            usuario = "pediatra.sem.crm",
            nome = "Pediatra Sem CRM",
            funcao = "Pediatra",
            idioma = "Pt"
        }, Json);

        Assert.Equal(HttpStatusCode.BadRequest, resposta.StatusCode);
        Assert.Contains("Crm", await resposta.Content.ReadAsStringAsync());
    }

    [SkippableFact]
    public async Task Profissao_sem_conselho_nao_exige_registro()
    {
        Skip.IfNot(ApiFixture.BancoDisponivel, "ATENDIMENTO_TEST_DB nao configurado.");

        var conta = await CriarAsync(
            "recepcao.ok", "Pessoa da Recepcao", FuncaoProfissional.Recepcao, null);

        Assert.Equal(ConselhoTipo.Nenhum, conta.Profissional.ConselhoTipo);
    }

    [SkippableFact]
    public async Task Usuario_repetido_e_rejeitado()
    {
        Skip.IfNot(ApiFixture.BancoDisponivel, "ATENDIMENTO_TEST_DB nao configurado.");

        await CriarAsync("usuario.repetido");

        var admin = await AdministradorAsync();

        var segundo = await admin.PostAsJsonAsync("/api/profissionais", new
        {
            usuario = "usuario.repetido",
            nome = "Outra Pessoa",
            funcao = "ClinicoGeral",
            registro = "12345",
            idioma = "Pt"
        }, Json);

        Assert.Equal(HttpStatusCode.BadRequest, segundo.StatusCode);
    }

    [SkippableFact]
    public async Task Usuario_nao_diferencia_maiusculas()
    {
        Skip.IfNot(ApiFixture.BancoDisponivel, "ATENDIMENTO_TEST_DB nao configurado.");

        await CriarAsync("caixa.alta");

        var admin = await AdministradorAsync();

        // "Caixa.Alta" nao pode virar uma segunda conta.
        var segundo = await admin.PostAsJsonAsync("/api/profissionais", new
        {
            usuario = "Caixa.Alta",
            nome = "Outra Pessoa",
            funcao = "Recepcao",
            idioma = "Pt"
        }, Json);

        Assert.Equal(HttpStatusCode.BadRequest, segundo.StatusCode);
    }

    [SkippableFact]
    public async Task Dois_homonimos_conseguem_ter_conta()
    {
        Skip.IfNot(ApiFixture.BancoDisponivel, "ATENDIMENTO_TEST_DB nao configurado.");

        // O modelo anterior identificava por nome + funcao e simplesmente
        // impedia a segunda pessoa de existir.
        var primeira = await CriarAsync("maria.silva.a", "Maria Silva");
        var segunda = await CriarAsync("maria.silva.b", "Maria Silva");

        Assert.NotEqual(primeira.Profissional.Id, segunda.Profissional.Id);
        Assert.Equal("Maria Silva", primeira.Profissional.Nome);
        Assert.Equal("Maria Silva", segunda.Profissional.Nome);
    }

    [SkippableFact]
    public async Task Consulta_de_disponibilidade_do_usuario()
    {
        Skip.IfNot(ApiFixture.BancoDisponivel, "ATENDIMENTO_TEST_DB nao configurado.");

        var admin = await AdministradorAsync();

        var livre = await admin.GetFromJsonAsync<UsuarioDisponivelResponse>(
            "/api/profissionais/usuario-disponivel?usuario=ninguem.usou.isso", Json);

        Assert.True(livre!.Disponivel);

        await CriarAsync("ja.existe.esse");

        var ocupado = await admin.GetFromJsonAsync<UsuarioDisponivelResponse>(
            "/api/profissionais/usuario-disponivel?usuario=ja.existe.esse", Json);

        Assert.False(ocupado!.Disponivel);
    }

    [SkippableFact]
    public async Task Consulta_de_disponibilidade_nao_e_publica()
    {
        Skip.IfNot(ApiFixture.BancoDisponivel, "ATENDIMENTO_TEST_DB nao configurado.");

        // Sem auto-registro nao ha motivo para deixar qualquer um descobrir
        // quem tem conta.
        var resposta = await _fixture.CreateClient()
            .GetAsync("/api/profissionais/usuario-disponivel?usuario=qualquer.um");

        Assert.Equal(HttpStatusCode.Unauthorized, resposta.StatusCode);
    }

    // -----------------------------------------------------------------------
    // Senha provisoria
    // -----------------------------------------------------------------------

    [SkippableFact]
    public async Task A_conta_nova_entra_mas_so_pode_trocar_a_senha()
    {
        Skip.IfNot(ApiFixture.BancoDisponivel, "ATENDIMENTO_TEST_DB nao configurado.");

        var conta = await CriarAsync("preso.na.troca");
        var client = await EntrarAsync("preso.na.troca", conta.SenhaProvisoria);

        // Ate a troca, mais de uma pessoa conhece essa senha. Um ato clinico
        // gravado nesse intervalo nao esta atribuido a ninguem com seguranca.
        var tentativa = await client.GetAsync("/api/bases");

        Assert.Equal(HttpStatusCode.Forbidden, tentativa.StatusCode);
        Assert.Contains("PrecisaTrocarSenha", await tentativa.Content.ReadAsStringAsync());
    }

    [SkippableFact]
    public async Task O_login_avisa_que_a_senha_e_provisoria()
    {
        Skip.IfNot(ApiFixture.BancoDisponivel, "ATENDIMENTO_TEST_DB nao configurado.");

        var conta = await CriarAsync("avisa.provisoria");
        var resposta = await LoginAsync("avisa.provisoria", conta.SenhaProvisoria);

        var login = await resposta.Content.ReadFromJsonAsync<LoginResponse>(Json);

        // Sem isso a tela nao teria como levar a pessoa direto para a troca, e
        // ela bateria num 403 sem entender.
        Assert.True(login!.Profissional.PrecisaTrocarSenha);
    }

    [SkippableFact]
    public async Task Depois_de_trocar_a_senha_o_sistema_se_abre()
    {
        Skip.IfNot(ApiFixture.BancoDisponivel, "ATENDIMENTO_TEST_DB nao configurado.");

        var conta = await CriarAsync("abre.depois");
        var client = await EntrarAsync("abre.depois", conta.SenhaProvisoria);

        const string nova = "plantao-do-sabado";

        var troca = await client.PostAsJsonAsync("/api/auth/trocar-senha", new
        {
            senhaAtual = conta.SenhaProvisoria,
            novaSenha = nova,
            confirmacaoSenha = nova
        }, Json);

        troca.EnsureSuccessStatusCode();

        var depois = await troca.Content.ReadFromJsonAsync<LoginResponse>(Json);

        Assert.False(depois!.Profissional.PrecisaTrocarSenha);

        // O token novo vem na resposta: sem ele a pessoa trocaria a senha e
        // continuaria barrada pelo token antigo, que ainda diz "precisa trocar".
        var comTokenNovo = _fixture.CreateClient();
        comTokenNovo.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", depois.Token);

        (await comTokenNovo.GetAsync("/api/bases")).EnsureSuccessStatusCode();
    }

    [SkippableFact]
    public async Task A_troca_exige_a_senha_atual()
    {
        Skip.IfNot(ApiFixture.BancoDisponivel, "ATENDIMENTO_TEST_DB nao configurado.");

        var conta = await CriarAsync("exige.atual");
        var client = await EntrarAsync("exige.atual", conta.SenhaProvisoria);

        // Sem isso, um aparelho deixado destravado no meio do plantao vira uma
        // conta tomada.
        var resposta = await client.PostAsJsonAsync("/api/auth/trocar-senha", new
        {
            senhaAtual = "chute-qualquer",
            novaSenha = "plantao-do-domingo",
            confirmacaoSenha = "plantao-do-domingo"
        }, Json);

        Assert.Equal(HttpStatusCode.BadRequest, resposta.StatusCode);
    }

    [SkippableFact]
    public async Task A_nova_senha_nao_pode_ser_a_provisoria()
    {
        Skip.IfNot(ApiFixture.BancoDisponivel, "ATENDIMENTO_TEST_DB nao configurado.");

        var conta = await CriarAsync("repete.provisoria");
        var client = await EntrarAsync("repete.provisoria", conta.SenhaProvisoria);

        // Repetir deixaria a conta onde estava: com a coordenacao sabendo a senha.
        var resposta = await client.PostAsJsonAsync("/api/auth/trocar-senha", new
        {
            senhaAtual = conta.SenhaProvisoria,
            novaSenha = conta.SenhaProvisoria,
            confirmacaoSenha = conta.SenhaProvisoria
        }, Json);

        Assert.Equal(HttpStatusCode.BadRequest, resposta.StatusCode);
    }

    [SkippableFact]
    public async Task A_coordenacao_redefine_a_senha_de_quem_perdeu()
    {
        Skip.IfNot(ApiFixture.BancoDisponivel, "ATENDIMENTO_TEST_DB nao configurado.");

        var conta = await CriarAsync("perdeu.a.senha");
        var admin = await AdministradorAsync();

        // Em campo nao ha e-mail de recuperacao. Sem esta saida, uma senha
        // esquecida deixaria a conta inutil para sempre.
        var resposta = await admin.PostAsJsonAsync(
            $"/api/profissionais/{conta.Profissional.Id}/redefinir-senha", new { }, Json);

        resposta.EnsureSuccessStatusCode();

        var nova = await resposta.Content.ReadFromJsonAsync<ContaCriadaDto>(Json);

        Assert.NotEqual(conta.SenhaProvisoria, nova!.SenhaProvisoria);
        Assert.True(nova.Profissional.PrecisaTrocarSenha);

        (await LoginAsync("perdeu.a.senha", nova.SenhaProvisoria)).EnsureSuccessStatusCode();
        Assert.Equal(
            HttpStatusCode.Unauthorized,
            (await LoginAsync("perdeu.a.senha", conta.SenhaProvisoria)).StatusCode);
    }

    // -----------------------------------------------------------------------
    // Reclassificacao
    // -----------------------------------------------------------------------

    [SkippableFact]
    public async Task A_coordenacao_reclassifica_a_profissao_e_a_fila_acompanha()
    {
        Skip.IfNot(ApiFixture.BancoDisponivel, "ATENDIMENTO_TEST_DB nao configurado.");

        var conta = await CriarAsync(
            "vira.pediatra", "Vira Pediatra", FuncaoProfissional.ClinicoGeral, "8080");
        var admin = await AdministradorAsync();

        var resposta = await admin.PostAsJsonAsync(
            $"/api/profissionais/{conta.Profissional.Id}/profissao",
            new { funcao = "Pediatra" },
            Json);

        resposta.EnsureSuccessStatusCode();

        var depois = await resposta.Content.ReadFromJsonAsync<ProfissionalDto>(Json);

        Assert.Equal(FuncaoProfissional.Pediatra, depois!.Funcao);
        Assert.Equal([Especialidade.Pediatria], depois.Filas);
    }

    [SkippableFact]
    public async Task Reclassificar_dentro_do_mesmo_conselho_mantem_o_registro()
    {
        Skip.IfNot(ApiFixture.BancoDisponivel, "ATENDIMENTO_TEST_DB nao configurado.");

        var conta = await CriarAsync(
            "mantem.crm", "Mantem CRM", FuncaoProfissional.ClinicoGeral, "7070");
        var admin = await AdministradorAsync();

        // Trocar clinico geral por ortopedista nao muda o CRM da pessoa; pedir
        // o numero de novo seria implicancia.
        var resposta = await admin.PostAsJsonAsync(
            $"/api/profissionais/{conta.Profissional.Id}/profissao",
            new { funcao = "Ortopedista" },
            Json);

        resposta.EnsureSuccessStatusCode();

        var depois = await resposta.Content.ReadFromJsonAsync<ProfissionalDto>(Json);

        Assert.Equal("7070", depois!.Registro);
    }

    [SkippableFact]
    public async Task Reclassificar_para_outro_conselho_exige_o_registro_novo()
    {
        Skip.IfNot(ApiFixture.BancoDisponivel, "ATENDIMENTO_TEST_DB nao configurado.");

        var conta = await CriarAsync(
            "muda.conselho", "Muda Conselho", FuncaoProfissional.ClinicoGeral, "6060");
        var admin = await AdministradorAsync();

        // Um CRM nao vale como CRO. Herdar o numero gravaria registro errado no
        // prontuario.
        var resposta = await admin.PostAsJsonAsync(
            $"/api/profissionais/{conta.Profissional.Id}/profissao",
            new { funcao = "Dentista" },
            Json);

        Assert.Equal(HttpStatusCode.BadRequest, resposta.StatusCode);
    }

    // -----------------------------------------------------------------------
    // Contas pendentes herdadas do fluxo antigo
    // -----------------------------------------------------------------------

    [SkippableFact]
    public async Task Conta_pendente_herdada_nao_entra()
    {
        Skip.IfNot(ApiFixture.BancoDisponivel, "ATENDIMENTO_TEST_DB nao configurado.");

        const string senha = "plantao-2026";
        await _fixture.ContaPendenteHerdadaAsync("herdada.pendente", senha, "Herdada Pendente");

        var resposta = await LoginAsync("herdada.pendente", senha);

        Assert.Equal(HttpStatusCode.Unauthorized, resposta.StatusCode);
        Assert.Contains("ContaPendente", await resposta.Content.ReadAsStringAsync());
    }

    [SkippableFact]
    public async Task Conta_pendente_herdada_ainda_pode_ser_aprovada()
    {
        Skip.IfNot(ApiFixture.BancoDisponivel, "ATENDIMENTO_TEST_DB nao configurado.");

        const string senha = "plantao-2026";
        var id = await _fixture.ContaPendenteHerdadaAsync("herdada.aprovada", senha, "Herdada Aprovada");
        var admin = await AdministradorAsync();

        // Elas existem em producao desde antes desta mudanca. Tirar a aprovacao
        // deixaria essas pessoas trancadas para fora sem caminho de volta.
        (await admin.PostAsJsonAsync($"/api/profissionais/{id}/aprovar", new { }, Json))
            .EnsureSuccessStatusCode();

        var resposta = await LoginAsync("herdada.aprovada", senha);
        resposta.EnsureSuccessStatusCode();

        var login = await resposta.Content.ReadFromJsonAsync<LoginResponse>(Json);

        Assert.Equal(StatusConta.Ativa, login!.Profissional.Status);

        // Ela escolheu a propria senha no registro antigo: ninguem mais a
        // conhece, entao nao ha o que trocar.
        Assert.False(login.Profissional.PrecisaTrocarSenha);
    }

    [SkippableFact]
    public async Task Recusa_sem_motivo_e_rejeitada()
    {
        Skip.IfNot(ApiFixture.BancoDisponivel, "ATENDIMENTO_TEST_DB nao configurado.");

        var id = await _fixture.ContaPendenteHerdadaAsync(
            "recusa.sem.motivo", "plantao-2026", "Recusa Sem Motivo");
        var admin = await AdministradorAsync();

        var resposta = await admin.PostAsJsonAsync(
            $"/api/profissionais/{id}/recusar", new { motivo = "" }, Json);

        Assert.Equal(HttpStatusCode.BadRequest, resposta.StatusCode);
    }

    // -----------------------------------------------------------------------
    // Autorizacao
    // -----------------------------------------------------------------------

    [SkippableFact]
    public async Task Conta_desativada_perde_o_acesso()
    {
        Skip.IfNot(ApiFixture.BancoDisponivel, "ATENDIMENTO_TEST_DB nao configurado.");

        var conta = await CriarAsync("sera.desativada");
        var admin = await AdministradorAsync();

        (await LoginAsync("sera.desativada", conta.SenhaProvisoria)).EnsureSuccessStatusCode();

        (await admin.PostAsJsonAsync(
            $"/api/profissionais/{conta.Profissional.Id}/desativar", new { }, Json))
            .EnsureSuccessStatusCode();

        var depois = await LoginAsync("sera.desativada", conta.SenhaProvisoria);

        Assert.Equal(HttpStatusCode.Unauthorized, depois.StatusCode);
        Assert.Contains("ContaDesativada", await depois.Content.ReadAsStringAsync());
    }

    [SkippableFact]
    public async Task Profissional_comum_nao_administra_contas()
    {
        Skip.IfNot(ApiFixture.BancoDisponivel, "ATENDIMENTO_TEST_DB nao configurado.");

        // A restricao esta no servidor, e nao em esconder o botao da tela.
        var comum = await _fixture.ClienteDeAsync(
            "comum.sem.poder", "Comum Sem Poder", FuncaoProfissional.Enfermeiro, "44444");

        var alvo = await CriarAsync("alvo.da.tentativa");

        Assert.Equal(HttpStatusCode.Forbidden,
            (await comum.GetAsync("/api/profissionais")).StatusCode);

        Assert.Equal(HttpStatusCode.Forbidden,
            (await comum.PostAsJsonAsync(
                $"/api/profissionais/{alvo.Profissional.Id}/redefinir-senha", new { }, Json)).StatusCode);

        Assert.Equal(HttpStatusCode.Forbidden,
            (await comum.PostAsJsonAsync(
                $"/api/profissionais/{alvo.Profissional.Id}/profissao",
                new { funcao = "Recepcao" }, Json)).StatusCode);
    }

    [SkippableFact]
    public async Task Sem_token_nao_acessa_gestao_de_contas()
    {
        Skip.IfNot(ApiFixture.BancoDisponivel, "ATENDIMENTO_TEST_DB nao configurado.");

        Assert.Equal(HttpStatusCode.Unauthorized,
            (await _fixture.CreateClient().GetAsync("/api/profissionais")).StatusCode);
    }

    [SkippableFact]
    public async Task Administrador_nao_desativa_a_propria_conta()
    {
        Skip.IfNot(ApiFixture.BancoDisponivel, "ATENDIMENTO_TEST_DB nao configurado.");

        var admin = await AdministradorAsync();
        var eu = (await admin.GetFromJsonAsync<List<ProfissionalDto>>(
            $"/api/profissionais?busca={ApiFixture.AdminUsuario}", Json))!.Single();

        var resposta = await admin.PostAsJsonAsync($"/api/profissionais/{eu.Id}/desativar", new { }, Json);

        Assert.Equal(HttpStatusCode.BadRequest, resposta.StatusCode);
    }

    [SkippableFact]
    public async Task Nao_da_para_remover_o_ultimo_administrador()
    {
        Skip.IfNot(ApiFixture.BancoDisponivel, "ATENDIMENTO_TEST_DB nao configurado.");

        // Sem administrador ativo ninguem cadastra mais ninguem, e o sistema
        // trava sem caminho de volta pela interface.
        var admin = await AdministradorAsync();
        var eu = (await admin.GetFromJsonAsync<List<ProfissionalDto>>(
            $"/api/profissionais?busca={ApiFixture.AdminUsuario}", Json))!.Single();

        var resposta = await admin.PostAsJsonAsync(
            $"/api/profissionais/{eu.Id}/administrador", new { ehAdministrador = false }, Json);

        Assert.Equal(HttpStatusCode.BadRequest, resposta.StatusCode);
    }

    [SkippableFact]
    public async Task Conta_pendente_nao_vira_administradora()
    {
        Skip.IfNot(ApiFixture.BancoDisponivel, "ATENDIMENTO_TEST_DB nao configurado.");

        var id = await _fixture.ContaPendenteHerdadaAsync(
            "pendente.admin", "plantao-2026", "Pendente Admin");
        var admin = await AdministradorAsync();

        var resposta = await admin.PostAsJsonAsync(
            $"/api/profissionais/{id}/administrador", new { ehAdministrador = true }, Json);

        Assert.Equal(HttpStatusCode.BadRequest, resposta.StatusCode);
    }

    // -----------------------------------------------------------------------
    // Login
    // -----------------------------------------------------------------------

    [SkippableFact]
    public async Task Senha_errada_nao_entra()
    {
        Skip.IfNot(ApiFixture.BancoDisponivel, "ATENDIMENTO_TEST_DB nao configurado.");

        await CriarAsync("senha.errada");

        var resposta = await LoginAsync("senha.errada", "nao-e-essa");

        Assert.Equal(HttpStatusCode.Unauthorized, resposta.StatusCode);
        Assert.Contains("CredenciaisInvalidas", await resposta.Content.ReadAsStringAsync());
    }

    [SkippableFact]
    public async Task Usuario_inexistente_responde_igual_a_senha_errada()
    {
        Skip.IfNot(ApiFixture.BancoDisponivel, "ATENDIMENTO_TEST_DB nao configurado.");

        // Nao adianta proteger o resto se o login revela quem tem conta.
        var resposta = await LoginAsync("nao.existe.mesmo", "plantao-2026");

        Assert.Equal(HttpStatusCode.Unauthorized, resposta.StatusCode);
        Assert.Contains("CredenciaisInvalidas", await resposta.Content.ReadAsStringAsync());
    }

    [SkippableFact]
    public async Task Login_aceita_usuario_em_caixa_alta()
    {
        Skip.IfNot(ApiFixture.BancoDisponivel, "ATENDIMENTO_TEST_DB nao configurado.");

        // O teclado do celular costuma capitalizar a primeira letra sozinho.
        var conta = await CriarAsync("caixa.no.login");

        (await LoginAsync("Caixa.No.Login", conta.SenhaProvisoria)).EnsureSuccessStatusCode();
    }

    [SkippableFact]
    public async Task Token_do_administrador_carrega_o_papel()
    {
        Skip.IfNot(ApiFixture.BancoDisponivel, "ATENDIMENTO_TEST_DB nao configurado.");

        var admin = await AdministradorAsync();

        // Se o papel nao viajasse no token, o proprio administrador levaria 403.
        (await admin.GetAsync("/api/profissionais")).EnsureSuccessStatusCode();
    }
}
