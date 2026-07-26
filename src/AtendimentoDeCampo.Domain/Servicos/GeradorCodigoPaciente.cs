using System.Security.Cryptography;
using System.Text;

namespace AtendimentoDeCampo.Domain.Servicos;

/// <summary>
/// Gera o codigo do paciente, no formato "XXXX-XXXX".
///
/// E o unico jeito de reencontrar quem ja foi atendido e nao tem documento — a
/// maioria em campo. O paciente leva o codigo anotado e, na visita seguinte,
/// informa-lo recupera o cadastro e o historico, mesmo em outra base.
///
/// Usa o mesmo alfabeto sem caracteres ambiguos do codigo de atendimento: ele e
/// lido em voz alta e anotado a mao, as vezes num papel que passa semanas no
/// bolso.
///
/// O formato e deliberadamente diferente do codigo de atendimento ("PRE-XXXX",
/// tres letras antes do hifen). Os dois circulam na mesma fila e alguem vai
/// digitar um no lugar do outro; formatos distintos permitem dizer qual e qual
/// em vez de responder "nao encontrado".
/// </summary>
public static class GeradorCodigoPaciente
{
    public const string Alfabeto = GeradorCodigoAtendimento.Alfabeto;

    public const int TamanhoBloco = 4;

    public static string Gerar()
    {
        var bruto = new StringBuilder(TamanhoBloco * 2);

        for (var i = 0; i < TamanhoBloco * 2; i++)
        {
            bruto.Append(Alfabeto[RandomNumberGenerator.GetInt32(Alfabeto.Length)]);
        }

        return $"{bruto.ToString(0, TamanhoBloco)}-{bruto.ToString(TamanhoBloco, TamanhoBloco)}";
    }

    /// <summary>
    /// Aceita o codigo com ou sem hifen, em qualquer caixa, e devolve a forma
    /// canonica. Quem digita no celular erra o hifen e o teclado sobe a caixa
    /// sozinho; recusar por isso seria implicancia.
    /// </summary>
    public static string? Normalizar(string? codigo)
    {
        if (string.IsNullOrWhiteSpace(codigo))
        {
            return null;
        }

        var limpo = new string(codigo.ToUpperInvariant().Where(Alfabeto.Contains).ToArray());

        return limpo.Length == TamanhoBloco * 2
            ? $"{limpo[..TamanhoBloco]}-{limpo[TamanhoBloco..]}"
            : null;
    }

    public static bool EhValido(string? codigo) => Normalizar(codigo) is not null;
}
