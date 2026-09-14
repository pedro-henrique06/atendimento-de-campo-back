using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using AtendimentoDeCampo.Api.Contratos;
using AtendimentoDeCampo.Domain;

namespace AtendimentoDeCampo.Tests;

/// <summary>
/// O tipo da operacao: missao programada ou catastrofe.
///
/// Sao dois formularios de papel e duas operacoes diferentes — na programada a
/// equipe vai a uma comunidade combinada, com agenda; na catastrofe ela monta
/// base onde deu.
/// </summary>
[Collection(Colecoes.Api)]
public class TipoDaMissaoTests
{
    private readonly ApiFixture _fixture;

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    public TipoDaMissaoTests(ApiFixture fixture) => _fixture = fixture;

    /// <summary>
    /// Quem cria e edita base e quem administra acessos — que e um eixo proprio,
    /// e nao a funcao Coordenacao.
    /// </summary>
    private Task<HttpClient> AdministradorAsync()
        => _fixture.ClienteDoAdministradorAsync();

    private static async Task<BaseAdminDto> CriarBaseAsync(
        HttpClient client, string nome, string prefixo, TipoMissao? tipo)
    {
        var resposta = await client.PostAsJsonAsync("/api/bases", new
        {
            nome,
            prefixoCodigo = prefixo,
            tipoMissao = tipo?.ToString()
        }, Json);

        resposta.EnsureSuccessStatusCode();
        return (await resposta.Content.ReadFromJsonAsync<BaseAdminDto>(Json))!;
    }

    private static async Task<ProntuarioDto> AbrirAsync(HttpClient client, Guid baseId, string nome)
    {
        var codigo = (await client.GetFromJsonAsync<CodigoNovoDto>("/api/pacientes/codigo-novo", Json))!.Codigo;

        var resposta = await client.PostAsJsonAsync("/api/atendimentos", new
        {
            baseId,
            paciente = new
            {
                codigo,
                nome,
                tipoDocumento = "SemDocumento",
                idadeAproximada = 30,
                sexo = "NaoInformado",
                statusAlergia = "NaoPerguntado",
                consentimentoRegistro = true
            }
        }, Json);

        resposta.EnsureSuccessStatusCode();
        return (await resposta.Content.ReadFromJsonAsync<ProntuarioDto>(Json))!;
    }

    /// <summary>
    /// Um prefixo de tres letras, unico nesta execucao.
    ///
    /// Sorteado, e nao fixo, porque o banco de teste sobrevive entre execucoes e
    /// o prefixo e unico por base: fixo, a segunda execucao ja falharia.
    /// </summary>
    private static readonly Dictionary<string, string> PrefixosDoTeste = new();
    private static readonly Random Sorteio = new();

    private static string Prefixo(string semente)
    {
        lock (PrefixosDoTeste)
        {
            if (!PrefixosDoTeste.TryGetValue(semente, out var prefixo))
            {
                prefixo = new string(Enumerable
                    .Range(0, 3)
                    .Select(_ => (char)('A' + Sorteio.Next(26)))
                    .ToArray());

                PrefixosDoTeste[semente] = prefixo;
            }

            return prefixo;
        }
    }

    [SkippableFact]
    public async Task A_base_guarda_em_que_operacao_esta()
    {
        Skip.IfNot(ApiFixture.BancoDisponivel, "ATENDIMENTO_TEST_DB nao configurado.");

        var client = await AdministradorAsync();

        var criada = await CriarBaseAsync(
            client, $"Abrigo Da Enchente {Prefixo("enchente")}", Prefixo("enchente"), TipoMissao.Catastrofe);

        Assert.Equal(TipoMissao.Catastrofe, criada.TipoMissao);
    }

    [SkippableFact]
    public async Task Base_anterior_ao_campo_fica_sem_tipo_em_vez_de_receber_um_chutado()
    {
        Skip.IfNot(ApiFixture.BancoDisponivel, "ATENDIMENTO_TEST_DB nao configurado.");

        var client = await AdministradorAsync();

        // Nao informar nao pode virar "Programada": chamaria de missao
        // programada uma base montada numa enchente.
        var criada = await CriarBaseAsync(
            client, $"Base Sem Tipo {Prefixo("semtipo")}", Prefixo("semtipo"), null);

        Assert.Null(criada.TipoMissao);
    }

    [SkippableFact]
    public async Task O_atendimento_guarda_a_operacao_em_que_aconteceu()
    {
        Skip.IfNot(ApiFixture.BancoDisponivel, "ATENDIMENTO_TEST_DB nao configurado.");

        var client = await AdministradorAsync();

        var baseDaMissao = await CriarBaseAsync(
            client, $"Aldeia Programada {Prefixo("programada")}", Prefixo("programada"), TipoMissao.Programada);

        var atendimento = await AbrirAsync(client, baseDaMissao.Id, "Paciente Da Missao");

        Assert.Equal(TipoMissao.Programada, atendimento.TipoMissao);
    }

    [SkippableFact]
    public async Task Mudar_o_tipo_da_base_nao_reescreve_os_atendimentos_anteriores()
    {
        Skip.IfNot(ApiFixture.BancoDisponivel, "ATENDIMENTO_TEST_DB nao configurado.");

        var client = await AdministradorAsync();

        var escola = await CriarBaseAsync(
            client, $"Escola Municipal {Prefixo("escola")}", Prefixo("escola"), TipoMissao.Programada);

        var naMissao = await AbrirAsync(client, escola.Id, "Paciente De Marco");

        // A mesma escola vira base de enchente em novembro.
        (await client.PutAsJsonAsync($"/api/bases/{escola.Id}", new
        {
            nome = $"Escola Municipal {Prefixo("escola")}",
            prefixoCodigo = escola.PrefixoCodigo,
            tipoMissao = "Catastrofe"
        }, Json)).EnsureSuccessStatusCode();

        var naEnchente = await AbrirAsync(client, escola.Id, "Paciente De Novembro");

        var antigo = await client.GetFromJsonAsync<ProntuarioDto>(
            $"/api/atendimentos/{naMissao.Id}", Json);

        // O de marco continua sendo da missao programada. Lido da base na hora
        // de mostrar, ele passaria a contar como atendimento de catastrofe.
        Assert.Equal(TipoMissao.Programada, antigo!.TipoMissao);
        Assert.Equal(TipoMissao.Catastrofe, naEnchente.TipoMissao);

        // E a base, essa sim, esta na operacao de agora.
        Assert.Equal(TipoMissao.Catastrofe, antigo.Base.TipoMissao);
    }

    [SkippableFact]
    public async Task Editar_a_base_sem_mandar_o_tipo_nao_apaga_o_que_esta_gravado()
    {
        Skip.IfNot(ApiFixture.BancoDisponivel, "ATENDIMENTO_TEST_DB nao configurado.");

        var client = await AdministradorAsync();

        var criada = await CriarBaseAsync(
            client, $"Ponto De Apoio {Prefixo("apoio")}", Prefixo("apoio"), TipoMissao.Catastrofe);

        // Uma tela que so corrige o nome nao pode limpar o tipo de quebra.
        var resposta = await client.PutAsJsonAsync($"/api/bases/{criada.Id}", new
        {
            nome = $"Ponto De Apoio Norte {Prefixo("apoio")}",
            prefixoCodigo = criada.PrefixoCodigo
        }, Json);

        resposta.EnsureSuccessStatusCode();

        var depois = (await resposta.Content.ReadFromJsonAsync<BaseAdminDto>(Json))!;

        Assert.Equal(TipoMissao.Catastrofe, depois.TipoMissao);
        Assert.Equal($"Ponto De Apoio Norte {Prefixo("apoio")}", depois.Nome);
    }
}
