using System.Security.Cryptography;
using System.Text;

namespace AtendimentoDeCampo.Domain.Servicos;

/// <summary>
/// Gera o codigo curto do atendimento, no formato "PRE-XXXX".
///
/// As duas metades tem exigencias diferentes, entao usam alfabetos diferentes:
///
///  - O prefixo identifica a base e precisa ser reconhecivel. Usa A-Z inteiro,
///    para que "Escuela Zoe" vire ESC e nao ECU. Como e fixo e conhecido pela
///    equipe, ambiguidade de leitura nao chega a ser um risco ali.
///
///  - O sufixo e sorteado, ninguem consegue deduzi-lo pelo contexto, e e lido
///    em voz alta e anotado a mao na fila. Por isso evita caracteres que se
///    confundem no papel: I, O, S e os digitos 0, 1 e 5.
/// </summary>
public static class GeradorCodigoAtendimento
{
    /// <summary>Alfabeto do sufixo, sem caracteres que se confundem escritos a mao.</summary>
    public const string Alfabeto = "ABCDEFGHJKLMNPQRTUVWXYZ2346789";

    /// <summary>Alfabeto do prefixo: todas as letras, para preservar o mnemonico da base.</summary>
    public const string AlfabetoPrefixo = "ABCDEFGHIJKLMNOPQRSTUVWXYZ";

    public const int TamanhoSufixo = 4;

    public static string Gerar(string prefixoBase)
    {
        var prefixo = NormalizarPrefixo(prefixoBase);
        var sufixo = new StringBuilder(TamanhoSufixo);

        for (var i = 0; i < TamanhoSufixo; i++)
        {
            sufixo.Append(Alfabeto[RandomNumberGenerator.GetInt32(Alfabeto.Length)]);
        }

        return $"{prefixo}-{sufixo}";
    }

    /// <summary>
    /// Deriva um prefixo de 3 letras a partir do nome da base.
    /// "Acampamento Panama" vira "ACA"; "Escuela Zoe" vira "ESC".
    /// </summary>
    public static string DerivarPrefixo(string nomeBase)
    {
        var letras = new StringBuilder();

        foreach (var c in nomeBase.ToUpperInvariant())
        {
            var normalizado = RemoverAcento(c);
            if (AlfabetoPrefixo.Contains(normalizado))
            {
                letras.Append(normalizado);
            }

            if (letras.Length == 3)
            {
                break;
            }
        }

        while (letras.Length < 3)
        {
            letras.Append('X');
        }

        return letras.ToString();
    }

    public static bool EhValido(string codigo)
    {
        if (string.IsNullOrWhiteSpace(codigo))
        {
            return false;
        }

        var partes = codigo.Split('-');
        if (partes.Length != 2 || partes[0].Length != 3 || partes[1].Length != TamanhoSufixo)
        {
            return false;
        }

        return partes[0].All(AlfabetoPrefixo.Contains) && partes[1].All(Alfabeto.Contains);
    }

    private static string NormalizarPrefixo(string prefixo)
    {
        if (string.IsNullOrWhiteSpace(prefixo))
        {
            throw new ArgumentException("Prefixo da base e obrigatorio.", nameof(prefixo));
        }

        var limpo = new string(prefixo.ToUpperInvariant().Where(AlfabetoPrefixo.Contains).ToArray());

        if (limpo.Length != 3)
        {
            throw new ArgumentException(
                "Prefixo da base deve ter exatamente 3 letras de A a Z.",
                nameof(prefixo));
        }

        return limpo;
    }

    private static char RemoverAcento(char c) => c switch
    {
        'Á' or 'À' or 'Â' or 'Ã' or 'Ä' => 'A',
        'É' or 'È' or 'Ê' or 'Ë' => 'E',
        'Í' or 'Ì' or 'Î' or 'Ï' => 'I',
        'Ó' or 'Ò' or 'Ô' or 'Õ' or 'Ö' => 'O',
        'Ú' or 'Ù' or 'Û' or 'Ü' => 'U',
        'Ç' => 'C',
        'Ñ' => 'N',
        _ => c
    };
}
