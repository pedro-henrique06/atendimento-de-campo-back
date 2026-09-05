namespace AtendimentoDeCampo.Domain.Servicos;

/// <summary>
/// O que o cadastro exige a mais quando o paciente e menor de idade.
///
/// Em campo a crianca costuma chegar acompanhada de quem nao e o responsavel
/// legal — vizinha, irma mais velha, alguem que a encontrou. O nome da mae e o
/// endereco sao o que permite reencontrar a familia depois, e sao justamente os
/// campos que ninguem preenche se o sistema nao pedir.
/// </summary>
public static class RegrasDoMenor
{
    /// <summary>Maioridade civil no Brasil, no Panama e na Venezuela.</summary>
    public const int MaioridadeAnos = 18;

    /// <summary>
    /// Idade desconhecida devolve falso, e nao verdadeiro.
    ///
    /// Em campo boa parte dos pacientes chega sem documento e sem saber a
    /// propria idade. Presumir menor ali bloquearia o cadastro de adulto com
    /// dois campos que ninguem sabe responder, e a recepcao aprenderia a
    /// inventar dado para conseguir seguir — que e pior que nao ter o dado.
    /// </summary>
    public static bool EhMenor(int? idade) => idade is not null && idade < MaioridadeAnos;

    /// <summary>
    /// Erros de cadastro para um paciente menor de idade. Lista vazia quando
    /// esta tudo certo ou quando a regra nao se aplica.
    /// </summary>
    public static IReadOnlyList<string> Validar(int? idade, string? nomeDaMae, string? endereco)
    {
        if (!EhMenor(idade))
        {
            return [];
        }

        var erros = new List<string>();

        if (string.IsNullOrWhiteSpace(nomeDaMae))
        {
            erros.Add("Paciente menor de idade: informe o nome da mae.");
        }

        if (string.IsNullOrWhiteSpace(endereco))
        {
            erros.Add("Paciente menor de idade: informe o endereco.");
        }

        return erros;
    }
}
