namespace AtendimentoDeCampo.Domain.Servicos;

/// <summary>
/// Politica de senha.
///
/// Deliberadamente modesta. O sistema e operado por voluntarios em campo, muitas
/// vezes em aparelho compartilhado e com pressa; exigir simbolo e caixa alta
/// produziria senha anotada em papel colado no celular, que e pior que uma senha
/// simples. O comprimento minimo faz mais pelo risco real do que a complexidade,
/// e a barreira principal do sistema e outra: conta so entra em uso depois de
/// aprovada por um administrador.
/// </summary>
public static class PoliticaDeSenha
{
    public const int TamanhoMinimo = 8;
    public const int TamanhoMaximo = 128;

    /// <summary>
    /// Senhas obvias demais para o contexto. Lista curta de proposito: bloquear
    /// muito ali so empurra a pessoa para variacoes igualmente fracas.
    /// </summary>
    private static readonly HashSet<string> Proibidas = new(StringComparer.OrdinalIgnoreCase)
    {
        "12345678", "123456789", "1234567890",
        "password", "senha123", "12341234",
        "atendimento", "voluntario", "voluntario123", "hospital"
    };

    public static IReadOnlyList<string> Validar(string? senha, string? usuario = null, string? nome = null)
    {
        var erros = new List<string>();

        if (string.IsNullOrWhiteSpace(senha))
        {
            erros.Add("Informe uma senha.");
            return erros;
        }

        if (senha.Length < TamanhoMinimo)
        {
            erros.Add($"A senha deve ter ao menos {TamanhoMinimo} caracteres.");
        }

        if (senha.Length > TamanhoMaximo)
        {
            erros.Add($"A senha deve ter no maximo {TamanhoMaximo} caracteres.");
        }

        if (Proibidas.Contains(senha))
        {
            erros.Add("Essa senha e facil demais de adivinhar. Escolha outra.");
        }

        // Senha igual ao proprio usuario e o primeiro palpite de qualquer um.
        if (!string.IsNullOrWhiteSpace(usuario) &&
            senha.Contains(usuario, StringComparison.OrdinalIgnoreCase))
        {
            erros.Add("A senha nao pode conter o nome de usuario.");
        }

        if (!string.IsNullOrWhiteSpace(nome))
        {
            var primeiroNome = nome.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();

            if (primeiroNome is { Length: >= 4 } &&
                senha.Contains(primeiroNome, StringComparison.OrdinalIgnoreCase))
            {
                erros.Add("A senha nao pode conter o seu nome.");
            }
        }

        return erros;
    }
}
