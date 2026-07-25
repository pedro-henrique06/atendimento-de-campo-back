using System.Web;
using Npgsql;

namespace AtendimentoDeCampo.Api.Servicos;

/// <summary>
/// Adapta a configuracao ao formato que plataformas de hospedagem entregam.
///
/// Railway, Heroku, Render e similares injetam a URL do banco no formato URI
/// (`postgresql://usuario:senha@host:porta/banco`) e a porta HTTP em `PORT`.
/// Nenhum dos dois formatos e entendido nativamente pelo ASP.NET Core ou pelo
/// Npgsql, entao a traducao acontece aqui, no boot.
/// </summary>
public static class ConfiguracaoDeAmbiente
{
    /// <summary>
    /// Converte a URL de conexao do Postgres para o formato de palavras-chave
    /// que o Npgsql entende.
    ///
    /// `postgresql://joao:s3nha@host.railway.internal:5432/railway` vira
    /// `Host=host.railway.internal;Port=5432;Database=railway;Username=joao;Password=s3nha;...`
    ///
    /// Uma string que ja esteja no formato de palavras-chave passa intacta, para
    /// que a mesma variavel de ambiente sirva os dois casos.
    /// </summary>
    public static string? NormalizarConexaoPostgres(string? valor)
    {
        if (string.IsNullOrWhiteSpace(valor))
        {
            return null;
        }

        var texto = valor.Trim();

        // Ja esta no formato do Npgsql.
        if (!texto.StartsWith("postgres://", StringComparison.OrdinalIgnoreCase) &&
            !texto.StartsWith("postgresql://", StringComparison.OrdinalIgnoreCase))
        {
            return texto;
        }

        var uri = new Uri(texto);
        var credenciais = uri.UserInfo.Split(':', 2);

        var construtor = new NpgsqlConnectionStringBuilder
        {
            Host = uri.Host,
            // A URI omite a porta quando ela e a padrao do protocolo.
            Port = uri.IsDefaultPort ? 5432 : uri.Port,
            Database = Uri.UnescapeDataString(uri.AbsolutePath.TrimStart('/')),
            Username = Uri.UnescapeDataString(credenciais[0]),
            Password = credenciais.Length > 1 ? Uri.UnescapeDataString(credenciais[1]) : null
        };

        // Rede interna da plataforma nao exige TLS; o proxy publico exige.
        // Prefer atende os dois: usa TLS quando o servidor oferece e nao valida
        // a cadeia do certificado, que no provedor nao e verificavel pela raiz
        // padrao do container. (No Npgsql 8 a validacao so acontece em VerifyCA
        // e VerifyFull, entao TrustServerCertificate deixou de ser necessario.)
        construtor.SslMode = SslMode.Prefer;

        AplicarParametrosDaQuery(uri, construtor);

        return construtor.ConnectionString;
    }

    /// <summary>
    /// Parametros passados na query string da URL vencem os padroes acima, para
    /// que o provedor consiga exigir TLS quando precisar.
    /// </summary>
    private static void AplicarParametrosDaQuery(Uri uri, NpgsqlConnectionStringBuilder construtor)
    {
        if (string.IsNullOrEmpty(uri.Query))
        {
            return;
        }

        var query = HttpUtility.ParseQueryString(uri.Query);

        foreach (var chave in query.AllKeys)
        {
            if (chave is null)
            {
                continue;
            }

            var valor = query[chave];

            if (string.Equals(chave, "sslmode", StringComparison.OrdinalIgnoreCase) &&
                Enum.TryParse<SslMode>(valor, ignoreCase: true, out var modo))
            {
                construtor.SslMode = modo;
            }
        }
    }

    /// <summary>
    /// Conexao efetiva com o Postgres, ja normalizada.
    ///
    /// DATABASE_URL vence a connection string do appsettings, e nao o
    /// contrario: so a plataforma de hospedagem define essa variavel, entao
    /// quando ela existe e porque ha um banco de verdade apontado por ela. Na
    /// ordem inversa o valor de desenvolvimento commitado no appsettings nunca
    /// e nulo e ganharia sempre, fazendo o deploy tentar `localhost:5432`.
    /// </summary>
    public static string? ConexaoPostgres(IConfiguration config)
        => NormalizarConexaoPostgres(
            config["DATABASE_URL"] ?? config.GetConnectionString("Postgres"));

    /// <summary>
    /// Endereco em que a aplicacao escuta.
    ///
    /// A plataforma escolhe a porta e a informa em `PORT`; escutar em outra faz
    /// o deploy subir sem nunca responder ao roteador. Precisa ser `0.0.0.0`, e
    /// nao `localhost`, para aceitar conexao de fora do container.
    /// </summary>
    public static string? EnderecoDeEscuta(IConfiguration config)
    {
        // Uma configuracao explicita de URLs continua tendo prioridade.
        if (!string.IsNullOrWhiteSpace(config["ASPNETCORE_URLS"]) ||
            !string.IsNullOrWhiteSpace(config["urls"]))
        {
            return null;
        }

        var porta = config["PORT"];

        return string.IsNullOrWhiteSpace(porta) ? null : $"http://0.0.0.0:{porta}";
    }

    /// <summary>
    /// Origens liberadas no CORS.
    ///
    /// Aceita tanto a lista do appsettings quanto uma variavel de ambiente com
    /// valores separados por virgula, porque painel de plataforma so oferece
    /// campo de texto simples — nao da para digitar `Cors__Origens__0` sem erro.
    /// </summary>
    public static string[] OrigensCors(IConfiguration config)
    {
        var daLista = config.GetSection("Cors:Origens").Get<string[]>() ?? Array.Empty<string>();
        var doTexto = config["Cors:OrigensTexto"] ?? string.Empty;

        return daLista
            .Concat(doTexto.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            .Select(o => o.TrimEnd('/'))
            .Where(o => !string.IsNullOrWhiteSpace(o))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }
}
