using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using AtendimentoDeCampo.Api.Contratos;
using AtendimentoDeCampo.Domain;
using AtendimentoDeCampo.Infrastructure;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;

namespace AtendimentoDeCampo.Tests;

/// <summary>
/// Sobe a API contra o Postgres de teste. A connection string vem de
/// ATENDIMENTO_TEST_DB; sem ela os testes sao pulados, para que a suite
/// continue rodando em maquinas sem banco.
/// </summary>
public sealed class ApiFixture : WebApplicationFactory<Program>, IAsyncLifetime
{
    public static string? ConnectionString =>
        Environment.GetEnvironmentVariable("ATENDIMENTO_TEST_DB");

    public static bool BancoDisponivel => !string.IsNullOrWhiteSpace(ConnectionString);

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.ConfigureAppConfiguration((_, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:Postgres"] = ConnectionString,
                ["Jwt:Chave"] = "chave-de-teste-com-mais-de-32-caracteres-para-hmac",
                ["Jwt:Emissor"] = "atendimento-de-campo",
                ["Jwt:Audiencia"] = "atendimento-de-campo-app",
                ["Auth:SenhaEquipe"] = "Voluntario",
                ["Banco:MigrarNoBoot"] = "true"
            });
        });
    }

    public async Task InitializeAsync()
    {
        if (!BancoDisponivel)
        {
            return;
        }

        // Base limpa a cada execucao da suite.
        using var escopo = Services.CreateScope();
        var db = escopo.ServiceProvider.GetRequiredService<AtendimentoDbContext>();
        await db.Database.EnsureDeletedAsync();
        await db.Database.MigrateAsync();
        await Seed.ExecutarAsync(db);
    }

    async Task IAsyncLifetime.DisposeAsync() => await Task.CompletedTask;
}

public class FluxoAtendimentoTests : IClassFixture<ApiFixture>
{
    private readonly ApiFixture _fixture;

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    public FluxoAtendimentoTests(ApiFixture fixture) => _fixture = fixture;

    private async Task<HttpClient> AutenticarAsync(
        string nome = "Claudia Candido da Luz",
        FuncaoProfissional funcao = FuncaoProfissional.Enfermeiro,
        string registro = "52728")
    {
        var client = _fixture.CreateClient();

        var resposta = await client.PostAsJsonAsync("/api/auth/login", new
        {
            nome,
            funcao = funcao.ToString(),
            registro,
            senha = "Voluntario",
            idioma = "Pt"
        }, Json);

        resposta.EnsureSuccessStatusCode();

        var login = await resposta.Content.ReadFromJsonAsync<LoginResponse>(Json);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", login!.Token);

        return client;
    }

    private async Task<Guid> PrimeiraBaseAsync(HttpClient client)
    {
        var bases = await client.GetFromJsonAsync<List<BaseDto>>("/api/bases", Json);
        return bases!.First().Id;
    }

    [SkippableFact]
    public async Task PrimeiroAcessoCriaAContaComASenhaDaEquipe()
    {
        Skip.IfNot(ApiFixture.BancoDisponivel, "ATENDIMENTO_TEST_DB nao configurado.");

        var client = _fixture.CreateClient();

        var resposta = await client.PostAsJsonAsync("/api/auth/login", new
        {
            nome = "Fabio Primeiro Acesso",
            funcao = "Medico",
            registro = "12345",
            senha = "Voluntario",
            idioma = "Pt"
        }, Json);

        resposta.EnsureSuccessStatusCode();
        var login = await resposta.Content.ReadFromJsonAsync<LoginResponse>(Json);

        Assert.True(login!.ContaCriadaAgora);
        Assert.Equal(ConselhoTipo.Crm, login.Profissional.ConselhoTipo);
        Assert.False(string.IsNullOrWhiteSpace(login.Token));
    }

    [SkippableFact]
    public async Task SenhaErradaNaoAutentica()
    {
        Skip.IfNot(ApiFixture.BancoDisponivel, "ATENDIMENTO_TEST_DB nao configurado.");

        var client = _fixture.CreateClient();

        var resposta = await client.PostAsJsonAsync("/api/auth/login", new
        {
            nome = "Alguem",
            funcao = "Medico",
            registro = "1",
            senha = "senha-errada",
            idioma = "Pt"
        }, Json);

        Assert.Equal(HttpStatusCode.Unauthorized, resposta.StatusCode);
    }

    [SkippableFact]
    public async Task SemTokenNaoAcessaAtendimentos()
    {
        Skip.IfNot(ApiFixture.BancoDisponivel, "ATENDIMENTO_TEST_DB nao configurado.");

        var client = _fixture.CreateClient();
        var resposta = await client.GetAsync($"/api/atendimentos?baseId={Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.Unauthorized, resposta.StatusCode);
    }

    [SkippableFact]
    public async Task FluxoCompletoDeUmPaciente()
    {
        Skip.IfNot(ApiFixture.BancoDisponivel, "ATENDIMENTO_TEST_DB nao configurado.");

        var client = await AutenticarAsync();
        var baseId = await PrimeiraBaseAsync(client);

        // 1. Cadastro do paciente e abertura do atendimento.
        var criacao = await client.PostAsJsonAsync("/api/atendimentos", new
        {
            baseId,
            queixaPrincipal = "Dor de dente ha uma semana",
            latitude = 10.60169,
            longitude = -66.92648,
            precisaoMetros = 14.0,
            paciente = new
            {
                nome = "Yesenia Teste",
                tipoDocumento = "CedulaIdentidade",
                numeroDocumento = "14140742",
                dataNascimento = "1990-03-15",
                sexo = "Feminino",
                statusAlergia = "SemAlergiaConhecida",
                consentimentoRegistro = true
            }
        }, Json);

        criacao.EnsureSuccessStatusCode();
        var prontuario = await criacao.Content.ReadFromJsonAsync<ProntuarioDto>(Json);

        Assert.NotNull(prontuario);
        Assert.Matches(@"^[A-Z]{3}-[A-Z2-9]{4}$", prontuario!.Codigo);
        Assert.Equal(StatusAtendimento.Aberto, prontuario.Status);
        Assert.NotNull(prontuario.Localizacao);

        // Paciente que nega alergia nao pode disparar o alerta vermelho.
        Assert.False(prontuario.Paciente.Alerta.Exibir);

        var id = prontuario.Id;

        // 2. Triagem com achados que sustentam a classificacao.
        var triagem = await client.PutAsJsonAsync($"/api/atendimentos/{id}/triagem", new
        {
            pressaoSistolica = 122,
            pressaoDiastolica = 79,
            frequenciaCardiaca = 68,
            frequenciaRespiratoria = 18,
            saturacaoO2 = 96,
            sintomas = new[] { "Febre", "Dor" },
            outroSintoma = "Algia no dente ha uma semana",
            medicamentosEmUso = "Nenhum",
            statusAlergia = "SemAlergiaConhecida",
            classificacaoRisco = "Verde",
            encaminhamento = "Odontologia",
            achadosStart = new { deambula = true }
        }, Json);

        triagem.EnsureSuccessStatusCode();
        var sugestao = await triagem.Content.ReadFromJsonAsync<SugestaoStartDto>(Json);

        Assert.Equal(ClassificacaoRisco.Verde, sugestao!.Sugerida);
        Assert.False(sugestao.Divergente);

        // 3. Odontologia com odontograma, incluindo o caso do dente 38.
        var odonto = await client.PutAsJsonAsync($"/api/atendimentos/{id}/odontologia", new
        {
            queixa = "Dor no elemento 38",
            cid10Codigo = "K02.9",
            procedimentos = new[] { "ProfilaxiaLimpeza", "OrientacaoHigieneBucal" },
            desfecho = "Alta",
            odontograma = new object[]
            {
                new { dente = 38, estado = "Carie", faces = new[] { "Mesial", "Oclusal" } },
                new { dente = 38, estado = "ExtracaoIndicada", faces = Array.Empty<string>() }
            },
            dispensacoes = Array.Empty<object>()
        }, Json);

        odonto.EnsureSuccessStatusCode();

        // 4. Finalizacao.
        var finalizar = await client.PostAsJsonAsync(
            $"/api/atendimentos/{id}/finalizar", new { }, Json);

        finalizar.EnsureSuccessStatusCode();

        // 5. Prontuario consolidado.
        var final = await client.GetFromJsonAsync<ProntuarioDto>($"/api/atendimentos/{id}", Json);

        Assert.Equal(StatusAtendimento.Finalizado, final!.Status);
        Assert.NotNull(final.FinalizadoEm);
        Assert.Equal(ClassificacaoRisco.Verde, final.ClassificacaoRisco);

        // O dente 38 conserva os dois estados, que era o que o sistema antigo perdia.
        Assert.NotNull(final.Odontologia);
        var dente38 = final.Odontologia!.Odontograma.Where(m => m.Dente == 38).ToList();
        Assert.Equal(2, dente38.Count);
        Assert.Equal("Carie: 38(M,O); Extracao indicada: 38", final.Odontologia.ResumoOdontograma);

        // Tempo nas filas registrado para triagem e odontologia.
        Assert.Contains(final.TempoNasFilas, t => t.Especialidade == Especialidade.Triagem);
        Assert.Contains(final.TempoNasFilas, t => t.Especialidade == Especialidade.Odontologia);
        Assert.All(final.TempoNasFilas, t => Assert.NotNull(t.SaiuEm));

        // Auditoria cobre criacao, etapas e finalizacao.
        Assert.Contains(final.Historico, h => h.Acao == AcaoAuditoria.CriouAtendimento);
        Assert.Contains(final.Historico, h => h.Acao == AcaoAuditoria.FinalizouAtendimento);
        Assert.Contains(final.Historico, h => h.Campo == "Odontograma");
        Assert.Contains(final.Historico, h => h.Campo == "Classificacao de risco (START)");
    }

    [SkippableFact]
    public async Task PacienteComAlergiaRealDisparaOAlerta()
    {
        Skip.IfNot(ApiFixture.BancoDisponivel, "ATENDIMENTO_TEST_DB nao configurado.");

        var client = await AutenticarAsync();
        var baseId = await PrimeiraBaseAsync(client);

        var resposta = await client.PostAsJsonAsync("/api/atendimentos", new
        {
            baseId,
            paciente = new
            {
                nome = "Paciente Alergico",
                tipoDocumento = "SemDocumento",
                idadeAproximada = 40,
                sexo = "Masculino",
                statusAlergia = "PossuiAlergia",
                alergias = "Dipirona",
                consentimentoRegistro = true
            }
        }, Json);

        resposta.EnsureSuccessStatusCode();
        var prontuario = await resposta.Content.ReadFromJsonAsync<ProntuarioDto>(Json);

        Assert.True(prontuario!.Paciente.Alerta.Exibir);
        Assert.Equal("Dipirona", prontuario.Paciente.Alerta.Texto);
    }

    [SkippableFact]
    public async Task DescricaoQueNegaAlergiaEhRejeitadaNaCriacao()
    {
        Skip.IfNot(ApiFixture.BancoDisponivel, "ATENDIMENTO_TEST_DB nao configurado.");

        var client = await AutenticarAsync();
        var baseId = await PrimeiraBaseAsync(client);

        var resposta = await client.PostAsJsonAsync("/api/atendimentos", new
        {
            baseId,
            paciente = new
            {
                nome = "Paciente Incoerente",
                tipoDocumento = "SemDocumento",
                idadeAproximada = 30,
                sexo = "Feminino",
                statusAlergia = "SemAlergiaConhecida",
                alergias = "Nega alergia medicamentosa",
                consentimentoRegistro = true
            }
        }, Json);

        Assert.Equal(HttpStatusCode.BadRequest, resposta.StatusCode);
    }

    [SkippableFact]
    public async Task AtendimentoSemConsentimentoEhRejeitado()
    {
        Skip.IfNot(ApiFixture.BancoDisponivel, "ATENDIMENTO_TEST_DB nao configurado.");

        var client = await AutenticarAsync();
        var baseId = await PrimeiraBaseAsync(client);

        var resposta = await client.PostAsJsonAsync("/api/atendimentos", new
        {
            baseId,
            paciente = new
            {
                nome = "Sem Consentimento",
                tipoDocumento = "SemDocumento",
                idadeAproximada = 20,
                sexo = "Feminino",
                statusAlergia = "NaoPerguntado",
                consentimentoRegistro = false
            }
        }, Json);

        Assert.Equal(HttpStatusCode.BadRequest, resposta.StatusCode);
    }

    [SkippableFact]
    public async Task NotaClinicaNoCampoDeMedicamentoEhRejeitada()
    {
        Skip.IfNot(ApiFixture.BancoDisponivel, "ATENDIMENTO_TEST_DB nao configurado.");

        var client = await AutenticarAsync();
        var baseId = await PrimeiraBaseAsync(client);
        var id = await CriarAtendimentoSimplesAsync(client, baseId, "Paciente Conduta");

        var resposta = await client.PutAsJsonAsync($"/api/atendimentos/{id}/consulta", new
        {
            especialidade = "ClinicaGeral",
            sintomasDescricao = "Nodulo em mama direita",
            cid10Codigo = "Z00.0",
            desfecho = "Alta",
            dispensacoes = new[]
            {
                new
                {
                    descricaoLivre =
                        "Confirmo tamanho de nodulo, oriento paciente quanto seguimento e investigacao com puncao.",
                    justificativaItemLivre = "nao encontrei no catalogo",
                    quantidade = 1
                }
            }
        }, Json);

        Assert.Equal(HttpStatusCode.BadRequest, resposta.StatusCode);

        var corpo = await resposta.Content.ReadAsStringAsync();
        Assert.Contains("conduta", corpo, StringComparison.OrdinalIgnoreCase);
    }

    [SkippableFact]
    public async Task ViaIncompativelComAApresentacaoEhRejeitada()
    {
        Skip.IfNot(ApiFixture.BancoDisponivel, "ATENDIMENTO_TEST_DB nao configurado.");

        var client = await AutenticarAsync();
        var baseId = await PrimeiraBaseAsync(client);
        var id = await CriarAtendimentoSimplesAsync(client, baseId, "Paciente Via");

        var itens = await client.GetFromJsonAsync<List<ItemCatalogoDto>>(
            "/api/catalogo/itens?busca=Ibuprofeno", Json);

        var comprimido = itens!.Single(i =>
            i.Forma == FormaFarmaceutica.Comprimido && i.Concentracao == "400 mg");

        var resposta = await client.PutAsJsonAsync($"/api/atendimentos/{id}/consulta", new
        {
            especialidade = "ClinicaGeral",
            cid10Codigo = "M79.1",
            desfecho = "Alta",
            dispensacoes = new[]
            {
                new { itemId = comprimido.Id, quantidade = 10, via = "Intramuscular" }
            }
        }, Json);

        Assert.Equal(HttpStatusCode.BadRequest, resposta.StatusCode);
        Assert.Contains("incompativel", await resposta.Content.ReadAsStringAsync(), StringComparison.OrdinalIgnoreCase);
    }

    [SkippableFact]
    public async Task DispensacaoValidaUsaAUnidadeDoCatalogo()
    {
        Skip.IfNot(ApiFixture.BancoDisponivel, "ATENDIMENTO_TEST_DB nao configurado.");

        var client = await AutenticarAsync();
        var baseId = await PrimeiraBaseAsync(client);
        var id = await CriarAtendimentoSimplesAsync(client, baseId, "Paciente Dispensacao");

        var itens = await client.GetFromJsonAsync<List<ItemCatalogoDto>>(
            "/api/catalogo/itens?busca=Ibuprofeno", Json);

        var comprimido = itens!.Single(i =>
            i.Forma == FormaFarmaceutica.Comprimido && i.Concentracao == "400 mg");

        var resposta = await client.PutAsJsonAsync($"/api/atendimentos/{id}/consulta", new
        {
            especialidade = "ClinicaGeral",
            cid10Codigo = "M79.1",
            desfecho = "Alta",
            dispensacoes = new[]
            {
                new { itemId = comprimido.Id, quantidade = 10, via = "Oral", posologia = "1 cp 8/8h" }
            }
        }, Json);

        resposta.EnsureSuccessStatusCode();

        var prontuario = await client.GetFromJsonAsync<ProntuarioDto>($"/api/atendimentos/{id}", Json);
        var dispensacao = Assert.Single(prontuario!.Consultas.Single().Dispensacoes);

        Assert.Equal(UnidadeDispensacao.Comprimido, dispensacao.Unidade);
        Assert.False(dispensacao.ForaDoCatalogo);
        Assert.Equal("Ibuprofeno 400 mg", dispensacao.Item);
    }

    [SkippableFact]
    public async Task ConsultaComAltaSemCidEhRejeitada()
    {
        Skip.IfNot(ApiFixture.BancoDisponivel, "ATENDIMENTO_TEST_DB nao configurado.");

        var client = await AutenticarAsync();
        var baseId = await PrimeiraBaseAsync(client);
        var id = await CriarAtendimentoSimplesAsync(client, baseId, "Paciente Sem CID");

        var resposta = await client.PutAsJsonAsync($"/api/atendimentos/{id}/consulta", new
        {
            especialidade = "ClinicaGeral",
            sintomasDescricao = "Dor de cabeca",
            desfecho = "Alta"
        }, Json);

        Assert.Equal(HttpStatusCode.BadRequest, resposta.StatusCode);
    }

    [SkippableFact]
    public async Task OdontogramaComDenteForaDaNotacaoFdiEhRejeitado()
    {
        Skip.IfNot(ApiFixture.BancoDisponivel, "ATENDIMENTO_TEST_DB nao configurado.");

        var client = await AutenticarAsync();
        var baseId = await PrimeiraBaseAsync(client);
        var id = await CriarAtendimentoSimplesAsync(client, baseId, "Paciente Odontograma");

        var resposta = await client.PutAsJsonAsync($"/api/atendimentos/{id}/odontologia", new
        {
            queixa = "teste",
            desfecho = "Retorno",
            odontograma = new[]
            {
                new { dente = 49, estado = "Carie", faces = Array.Empty<string>() }
            }
        }, Json);

        Assert.Equal(HttpStatusCode.BadRequest, resposta.StatusCode);
    }

    [SkippableFact]
    public async Task EdicaoAposFinalizacaoExigeReaberturaComJustificativa()
    {
        Skip.IfNot(ApiFixture.BancoDisponivel, "ATENDIMENTO_TEST_DB nao configurado.");

        var client = await AutenticarAsync();
        var baseId = await PrimeiraBaseAsync(client);
        var id = await CriarAtendimentoSimplesAsync(client, baseId, "Paciente Reabertura");

        (await client.PutAsJsonAsync($"/api/atendimentos/{id}/triagem", new
        {
            statusAlergia = "NaoPerguntado",
            classificacaoRisco = "Verde",
            encaminhamento = "ClinicaGeral"
        }, Json)).EnsureSuccessStatusCode();

        (await client.PutAsJsonAsync($"/api/atendimentos/{id}/consulta", new
        {
            especialidade = "ClinicaGeral",
            cid10Codigo = "J00",
            desfecho = "Alta"
        }, Json)).EnsureSuccessStatusCode();

        (await client.PostAsJsonAsync($"/api/atendimentos/{id}/finalizar", new { }, Json))
            .EnsureSuccessStatusCode();

        // Reabrir sem justificativa nao pode passar.
        var semJustificativa = await client.PostAsJsonAsync(
            $"/api/atendimentos/{id}/reabrir", new { }, Json);

        Assert.Equal(HttpStatusCode.BadRequest, semJustificativa.StatusCode);

        // Com justificativa, reabre e a edicao seguinte fica marcada.
        var comJustificativa = await client.PostAsJsonAsync(
            $"/api/atendimentos/{id}/reabrir",
            new { justificativa = "CID lancado errado durante o plantao." },
            Json);

        comJustificativa.EnsureSuccessStatusCode();

        await client.PutAsJsonAsync($"/api/atendimentos/{id}/consulta", new
        {
            especialidade = "ClinicaGeral",
            cid10Codigo = "J11",
            desfecho = "Alta"
        }, Json);

        var prontuario = await client.GetFromJsonAsync<ProntuarioDto>($"/api/atendimentos/{id}", Json);

        Assert.Contains(prontuario!.Historico, h => h.Acao == AcaoAuditoria.ReabriuAtendimento);
        Assert.Contains(prontuario.Historico, h => h.Acao == AcaoAuditoria.EditouAposFinalizacao);
    }

    [SkippableFact]
    public async Task FinalizarComEtapaPendenteEhRejeitado()
    {
        Skip.IfNot(ApiFixture.BancoDisponivel, "ATENDIMENTO_TEST_DB nao configurado.");

        var client = await AutenticarAsync();
        var baseId = await PrimeiraBaseAsync(client);
        var id = await CriarAtendimentoSimplesAsync(client, baseId, "Paciente Pendente");

        // A fila de triagem continua aberta.
        var resposta = await client.PostAsJsonAsync($"/api/atendimentos/{id}/finalizar", new { }, Json);

        Assert.Equal(HttpStatusCode.BadRequest, resposta.StatusCode);
        Assert.Contains("pendentes", await resposta.Content.ReadAsStringAsync(), StringComparison.OrdinalIgnoreCase);
    }

    [SkippableFact]
    public async Task DivergenciaDoStartEhRegistradaSemBloquear()
    {
        Skip.IfNot(ApiFixture.BancoDisponivel, "ATENDIMENTO_TEST_DB nao configurado.");

        var client = await AutenticarAsync();
        var baseId = await PrimeiraBaseAsync(client);
        var id = await CriarAtendimentoSimplesAsync(client, baseId, "Paciente Divergente");

        // Achados sugerem Verde; a enfermeira classifica como Amarelo.
        var resposta = await client.PutAsJsonAsync($"/api/atendimentos/{id}/triagem", new
        {
            frequenciaRespiratoria = 18,
            statusAlergia = "NaoPerguntado",
            classificacaoRisco = "Amarelo",
            encaminhamento = "ClinicaGeral",
            achadosStart = new { deambula = true }
        }, Json);

        resposta.EnsureSuccessStatusCode();
        var sugestao = await resposta.Content.ReadFromJsonAsync<SugestaoStartDto>(Json);

        Assert.Equal(ClassificacaoRisco.Verde, sugestao!.Sugerida);
        Assert.True(sugestao.Divergente);

        // A classificacao gravada e a do profissional, nao a do algoritmo.
        var prontuario = await client.GetFromJsonAsync<ProntuarioDto>($"/api/atendimentos/{id}", Json);
        Assert.Equal(ClassificacaoRisco.Amarelo, prontuario!.ClassificacaoRisco);
    }

    [SkippableFact]
    public async Task ListagemFiltraPorFilaERisco()
    {
        Skip.IfNot(ApiFixture.BancoDisponivel, "ATENDIMENTO_TEST_DB nao configurado.");

        var client = await AutenticarAsync();
        var baseId = await PrimeiraBaseAsync(client);
        var id = await CriarAtendimentoSimplesAsync(client, baseId, "Paciente Filtro");

        await client.PutAsJsonAsync($"/api/atendimentos/{id}/triagem", new
        {
            statusAlergia = "NaoPerguntado",
            classificacaoRisco = "Vermelho",
            encaminhamento = "Ortopedia",
            achadosStart = new { deambula = false, pulsoRadialPresente = false }
        }, Json);

        var vermelhos = await client.GetFromJsonAsync<List<AtendimentoResumoDto>>(
            $"/api/atendimentos?baseId={baseId}&risco=Vermelho", Json);

        Assert.Contains(vermelhos!, a => a.Id == id);

        var naFilaOrtopedia = await client.GetFromJsonAsync<List<AtendimentoResumoDto>>(
            $"/api/atendimentos?baseId={baseId}&fila=Ortopedia", Json);

        Assert.Contains(naFilaOrtopedia!, a => a.Id == id);

        // Ja saiu da triagem, entao nao deve aparecer naquela fila.
        var naFilaTriagem = await client.GetFromJsonAsync<List<AtendimentoResumoDto>>(
            $"/api/atendimentos?baseId={baseId}&fila=Triagem", Json);

        Assert.DoesNotContain(naFilaTriagem!, a => a.Id == id);
    }

    [SkippableFact]
    public async Task BuscaPeloCodigoEncontraOAtendimento()
    {
        Skip.IfNot(ApiFixture.BancoDisponivel, "ATENDIMENTO_TEST_DB nao configurado.");

        var client = await AutenticarAsync();
        var baseId = await PrimeiraBaseAsync(client);
        var id = await CriarAtendimentoSimplesAsync(client, baseId, "Paciente Codigo");

        var prontuario = await client.GetFromJsonAsync<ProntuarioDto>($"/api/atendimentos/{id}", Json);
        var porCodigo = await client.GetFromJsonAsync<ProntuarioDto>(
            $"/api/atendimentos/codigo/{prontuario!.Codigo}", Json);

        Assert.Equal(id, porCodigo!.Id);
    }

    [SkippableFact]
    public async Task PacienteComMesmoDocumentoReaproveitaOCadastro()
    {
        Skip.IfNot(ApiFixture.BancoDisponivel, "ATENDIMENTO_TEST_DB nao configurado.");

        var client = await AutenticarAsync();
        var baseId = await PrimeiraBaseAsync(client);

        async Task<ProntuarioDto> AbrirAsync()
        {
            var resposta = await client.PostAsJsonAsync("/api/atendimentos", new
            {
                baseId,
                paciente = new
                {
                    nome = "Paciente Recorrente",
                    tipoDocumento = "Passaporte",
                    numeroDocumento = "PA-998877",
                    idadeAproximada = 35,
                    sexo = "Masculino",
                    statusAlergia = "NaoPerguntado",
                    consentimentoRegistro = true
                }
            }, Json);

            resposta.EnsureSuccessStatusCode();
            return (await resposta.Content.ReadFromJsonAsync<ProntuarioDto>(Json))!;
        }

        var primeiro = await AbrirAsync();
        var segundo = await AbrirAsync();

        // Dois atendimentos distintos, um unico paciente.
        Assert.NotEqual(primeiro.Id, segundo.Id);
        Assert.Equal(primeiro.Paciente.Id, segundo.Paciente.Id);
    }

    private async Task<Guid> CriarAtendimentoSimplesAsync(HttpClient client, Guid baseId, string nome)
    {
        var resposta = await client.PostAsJsonAsync("/api/atendimentos", new
        {
            baseId,
            paciente = new
            {
                nome,
                tipoDocumento = "SemDocumento",
                idadeAproximada = 30,
                sexo = "NaoInformado",
                statusAlergia = "NaoPerguntado",
                consentimentoRegistro = true
            }
        }, Json);

        resposta.EnsureSuccessStatusCode();
        var prontuario = await resposta.Content.ReadFromJsonAsync<ProntuarioDto>(Json);

        return prontuario!.Id;
    }
}
