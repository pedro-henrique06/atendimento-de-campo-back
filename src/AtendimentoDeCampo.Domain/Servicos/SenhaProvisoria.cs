using System.Security.Cryptography;
using System.Text;

namespace AtendimentoDeCampo.Domain.Servicos;

/// <summary>
/// Senha do primeiro acesso, sorteada pelo sistema quando a coordenacao cria a
/// conta.
///
/// Quem cadastra nao escolhe a senha de outra pessoa. Isso importa mais aqui do
/// que pareceria: num prontuario cada ato clinico fica atribuido a um nome, e a
/// atribuicao so vale se ninguem mais puder entrar como aquela pessoa. Sortear e
/// o que impede a coordenacao de repetir a mesma senha em todo mundo, e a troca
/// obrigatoria no primeiro acesso e o que devolve a conta ao dono.
///
/// O formato segue o codigo do paciente pelo mesmo motivo: em campo isso vai ser
/// lido em voz alta e anotado a mao. Alfabeto sem caracteres ambiguos, em blocos
/// de quatro.
/// </summary>
public static class SenhaProvisoria
{
    public const string Alfabeto = GeradorCodigoAtendimento.Alfabeto;

    public const int TamanhoBloco = 4;
    public const int Blocos = 3;

    /// <summary>
    /// Sorteia uma senha no formato "XXXX-XXXX-XXXX".
    ///
    /// Doze caracteres do alfabeto sem ambiguidade passam com folga do minimo da
    /// <see cref="PoliticaDeSenha"/>, e o sorteio nao produz nenhuma das senhas
    /// proibidas nem o nome de quem esta sendo cadastrado.
    /// </summary>
    public static string Gerar()
    {
        var partes = new string[Blocos];

        for (var bloco = 0; bloco < Blocos; bloco++)
        {
            var letras = new StringBuilder(TamanhoBloco);

            for (var i = 0; i < TamanhoBloco; i++)
            {
                letras.Append(Alfabeto[RandomNumberGenerator.GetInt32(Alfabeto.Length)]);
            }

            partes[bloco] = letras.ToString();
        }

        return string.Join('-', partes);
    }
}
