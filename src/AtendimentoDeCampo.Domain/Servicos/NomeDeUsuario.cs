using System.Globalization;
using System.Text;

namespace AtendimentoDeCampo.Domain.Servicos;

/// <summary>
/// Regras do nome de usuario.
///
/// E o que a pessoa digita a cada plantao, no celular, muitas vezes com a mao
/// suja ou com luva. Precisa ser curto, sem acento e sem ambiguidade de caixa —
/// "Claudia.Luz" e "claudia.luz" nao podem ser contas diferentes.
/// </summary>
public static class NomeDeUsuario
{
    public const int TamanhoMinimo = 3;
    public const int TamanhoMaximo = 40;

    /// <summary>
    /// Forma canonica: minusculo, sem acento e sem espaco nas pontas.
    /// A comparacao no banco usa sempre esta forma.
    /// </summary>
    public static string Normalizar(string? usuario)
    {
        if (string.IsNullOrWhiteSpace(usuario))
        {
            return string.Empty;
        }

        var semAcento = new StringBuilder();

        foreach (var c in usuario.Trim().Normalize(NormalizationForm.FormD))
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
            {
                semAcento.Append(c);
            }
        }

        return semAcento.ToString().Normalize(NormalizationForm.FormC).ToLowerInvariant();
    }

    public static IReadOnlyList<string> Validar(string? usuario)
    {
        var erros = new List<string>();
        var normalizado = Normalizar(usuario);

        if (normalizado.Length < TamanhoMinimo)
        {
            erros.Add($"Usuario deve ter ao menos {TamanhoMinimo} caracteres.");
            return erros;
        }

        if (normalizado.Length > TamanhoMaximo)
        {
            erros.Add($"Usuario deve ter no maximo {TamanhoMaximo} caracteres.");
        }

        foreach (var c in normalizado)
        {
            if (!char.IsAsciiLetterLower(c) && !char.IsAsciiDigit(c) && c is not ('.' or '-' or '_'))
            {
                erros.Add("Usuario aceita apenas letras, numeros, ponto, hifen e sublinhado.");
                break;
            }
        }

        if (!char.IsAsciiLetterLower(normalizado[0]))
        {
            erros.Add("Usuario deve comecar com uma letra.");
        }

        return erros;
    }

    /// <summary>
    /// Sugestao a partir do nome completo: "Claudia Cândido da Luz" vira
    /// "claudia.luz". Serve para preencher o campo automaticamente na tela de
    /// registro, sem impedir que a pessoa troque.
    /// </summary>
    public static string Sugerir(string nomeCompleto)
    {
        var partes = Normalizar(nomeCompleto)
            .Split(' ', StringSplitOptions.RemoveEmptyEntries)
            // Preposicoes nao ajudam a identificar ninguem.
            .Where(p => p is not ("de" or "da" or "do" or "das" or "dos" or "e"))
            .ToList();

        if (partes.Count == 0)
        {
            return string.Empty;
        }

        var bruto = partes.Count == 1 ? partes[0] : $"{partes[0]}.{partes[^1]}";
        var limpo = new string(bruto.Where(c => char.IsAsciiLetterLower(c) || char.IsAsciiDigit(c) || c == '.').ToArray());

        return limpo.Length > TamanhoMaximo ? limpo[..TamanhoMaximo] : limpo;
    }
}
