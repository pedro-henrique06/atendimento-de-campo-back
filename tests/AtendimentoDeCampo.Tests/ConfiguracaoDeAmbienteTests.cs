using AtendimentoDeCampo.Api.Servicos;
using Microsoft.Extensions.Configuration;
using Npgsql;

namespace AtendimentoDeCampo.Tests;

/// <summary>
/// Estas conversoes so falham em producao, no primeiro deploy, com mensagem
/// pouco util. Testa-las aqui e mais barato que descobrir com o app no ar.
/// </summary>
public class ConfiguracaoDeAmbienteTests
{
    private static IConfiguration Config(params (string Chave, string Valor)[] valores)
        => new ConfigurationBuilder()
            .AddInMemoryCollection(valores.Select(v =>
                new KeyValuePair<string, string?>(v.Chave, v.Valor)))
            .Build();

    // -----------------------------------------------------------------------
    // Conexao com o banco
    // -----------------------------------------------------------------------

    [Fact]
    public void UrlDoPostgresViraFormatoDoNpgsql()
    {
        var resultado = ConfiguracaoDeAmbiente.NormalizarConexaoPostgres(
            "postgresql://joao:s3nha@monorail.proxy.rlwy.net:41234/railway");

        var construtor = new NpgsqlConnectionStringBuilder(resultado);

        Assert.Equal("monorail.proxy.rlwy.net", construtor.Host);
        Assert.Equal(41234, construtor.Port);
        Assert.Equal("railway", construtor.Database);
        Assert.Equal("joao", construtor.Username);
        Assert.Equal("s3nha", construtor.Password);
    }

    [Fact]
    public void EsquemaPostgresCurtoTambemEhAceito()
    {
        // Algumas plataformas usam `postgres://` em vez de `postgresql://`.
        var resultado = ConfiguracaoDeAmbiente.NormalizarConexaoPostgres(
            "postgres://usuario:senha@host.interno:5432/banco");

        Assert.Equal("host.interno", new NpgsqlConnectionStringBuilder(resultado).Host);
    }

    [Fact]
    public void SenhaComCaractereEspecialEhDecodificada()
    {
        // Senha gerada por provedor costuma ter simbolos, e eles chegam
        // percent-encoded na URL. Sem decodificar, a autenticacao falha.
        var resultado = ConfiguracaoDeAmbiente.NormalizarConexaoPostgres(
            "postgresql://usuario:s%40nha%3Fdif%C3%ADcil@host:5432/banco");

        Assert.Equal("s@nha?difícil", new NpgsqlConnectionStringBuilder(resultado).Password);
    }

    [Fact]
    public void PortaOmitidaUsaAPadraoDoPostgres()
    {
        var resultado = ConfiguracaoDeAmbiente.NormalizarConexaoPostgres(
            "postgresql://usuario:senha@host/banco");

        Assert.Equal(5432, new NpgsqlConnectionStringBuilder(resultado).Port);
    }

    [Fact]
    public void TlsEhTolerantePorPadrao()
    {
        // A rede interna da plataforma nao usa TLS e o proxy publico usa, com
        // certificado que a cadeia padrao do container nao verifica. Prefer
        // atende os dois casos sem validar a cadeia.
        var construtor = new NpgsqlConnectionStringBuilder(
            ConfiguracaoDeAmbiente.NormalizarConexaoPostgres(
                "postgresql://usuario:senha@host:5432/banco"));

        Assert.Equal(SslMode.Prefer, construtor.SslMode);
    }

    [Fact]
    public void SslModeDaQueryStringVenceOPadrao()
    {
        var construtor = new NpgsqlConnectionStringBuilder(
            ConfiguracaoDeAmbiente.NormalizarConexaoPostgres(
                "postgresql://usuario:senha@host:5432/banco?sslmode=require"));

        Assert.Equal(SslMode.Require, construtor.SslMode);
    }

    [Fact]
    public void StringNoFormatoDoNpgsqlPassaIntacta()
    {
        // A mesma variavel de ambiente precisa servir para desenvolvimento
        // local, onde a conexao ja vem no formato de palavras-chave.
        const string original = "Host=localhost;Port=5432;Database=x;Username=y;Password=z";

        Assert.Equal(original, ConfiguracaoDeAmbiente.NormalizarConexaoPostgres(original));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void ConexaoVaziaViraNulo(string? valor)
    {
        Assert.Null(ConfiguracaoDeAmbiente.NormalizarConexaoPostgres(valor));
    }

    [Fact]
    public void DatabaseUrlVenceAConnectionStringDoAppsettings()
    {
        // Regressao de um bug que so aparece em producao: o appsettings tem uma
        // connection string de desenvolvimento commitada, que nunca e nula. Se
        // ela tivesse prioridade, o deploy ignoraria o banco da plataforma e
        // morreria tentando localhost.
        var conexao = ConfiguracaoDeAmbiente.ConexaoPostgres(Config(
            ("ConnectionStrings:Postgres", "Host=localhost;Port=5432;Database=dev;Username=u;Password=p"),
            ("DATABASE_URL", "postgresql://prod:senha@banco.plataforma:5432/producao")));

        var construtor = new NpgsqlConnectionStringBuilder(conexao);

        Assert.Equal("banco.plataforma", construtor.Host);
        Assert.Equal("producao", construtor.Database);
    }

    [Fact]
    public void SemDatabaseUrlUsaAConnectionStringLocal()
    {
        var conexao = ConfiguracaoDeAmbiente.ConexaoPostgres(Config(
            ("ConnectionStrings:Postgres", "Host=localhost;Port=5432;Database=dev;Username=u;Password=p")));

        Assert.Equal("localhost", new NpgsqlConnectionStringBuilder(conexao).Host);
    }

    [Fact]
    public void SemNenhumaConfiguracaoDeBancoRetornaNulo()
    {
        Assert.Null(ConfiguracaoDeAmbiente.ConexaoPostgres(Config()));
    }

    // -----------------------------------------------------------------------
    // Porta de escuta
    // -----------------------------------------------------------------------

    [Fact]
    public void EscutaNaPortaInformadaPelaPlataforma()
    {
        var endereco = ConfiguracaoDeAmbiente.EnderecoDeEscuta(Config(("PORT", "8080")));

        // Precisa ser 0.0.0.0: em localhost o container nao aceita conexao
        // vinda do roteador da plataforma.
        Assert.Equal("http://0.0.0.0:8080", endereco);
    }

    [Fact]
    public void SemPortaNaoForcaEndereco()
    {
        Assert.Null(ConfiguracaoDeAmbiente.EnderecoDeEscuta(Config()));
    }

    [Fact]
    public void ConfiguracaoExplicitaDeUrlsTemPrioridade()
    {
        var endereco = ConfiguracaoDeAmbiente.EnderecoDeEscuta(
            Config(("PORT", "8080"), ("ASPNETCORE_URLS", "http://0.0.0.0:5080")));

        Assert.Null(endereco);
    }

    // -----------------------------------------------------------------------
    // CORS
    // -----------------------------------------------------------------------

    [Fact]
    public void OrigensVemDaListaEDoTextoSeparadoPorVirgula()
    {
        // Painel de plataforma so oferece campo de texto simples; ninguem vai
        // digitar `Cors__Origens__0` sem errar.
        var origens = ConfiguracaoDeAmbiente.OrigensCors(Config(
            ("Cors:Origens:0", "http://localhost:5173"),
            ("Cors:OrigensTexto", "https://app.exemplo.org, https://outro.exemplo.org")));

        Assert.Equal(3, origens.Length);
        Assert.Contains("https://app.exemplo.org", origens);
        Assert.Contains("https://outro.exemplo.org", origens);
    }

    [Fact]
    public void BarraFinalEhRemovidaDaOrigem()
    {
        // O navegador envia a origem sem barra final; com ela, o CORS falha e o
        // erro no console nao explica o motivo.
        var origens = ConfiguracaoDeAmbiente.OrigensCors(
            Config(("Cors:OrigensTexto", "https://app.exemplo.org/")));

        Assert.Equal(new[] { "https://app.exemplo.org" }, origens);
    }

    [Fact]
    public void OrigemRepetidaAparaceUmaVezSo()
    {
        var origens = ConfiguracaoDeAmbiente.OrigensCors(Config(
            ("Cors:Origens:0", "https://app.exemplo.org"),
            ("Cors:OrigensTexto", "https://app.exemplo.org/")));

        Assert.Single(origens);
    }

    [Fact]
    public void SemConfiguracaoNenhumaOrigemEhLiberada()
    {
        Assert.Empty(ConfiguracaoDeAmbiente.OrigensCors(Config()));
    }
}
