namespace AtendimentoDeCampo.Domain.Servicos;

/// <summary>Faixa de IMC pelos cortes da OMS para adultos.</summary>
public enum FaixaImc
{
    Baixo = 0,
    Adequado = 1,
    Sobrepeso = 2,
    Obesidade = 3
}

/// <summary>
/// IMC a partir do peso e da altura anotados na triagem.
///
/// A classificacao so vale para adulto. Em crianca o IMC se le em curva por
/// idade e sexo, e o corte de adulto diria "baixo peso" para uma crianca
/// perfeitamente saudavel — por isso <see cref="Classificar"/> exige a idade e
/// devolve nulo para menor de 20 anos, em vez de dar uma resposta errada.
/// </summary>
public static class CalculadoraImc
{
    /// <summary>Idade a partir da qual o corte de adulto da OMS se aplica.</summary>
    public const int IdadeMinimaParaClassificar = 20;

    /// <summary>IMC arredondado a uma casa, ou nulo se faltar peso ou altura.</summary>
    public static double? Calcular(double? pesoKg, int? alturaCm)
    {
        if (pesoKg is not > 0 || alturaCm is not > 0)
        {
            return null;
        }

        var metros = alturaCm.Value / 100.0;

        return Math.Round(pesoKg.Value / (metros * metros), 1);
    }

    public static FaixaImc? Classificar(double? imc, int? idade)
    {
        if (imc is null || idade is null || idade < IdadeMinimaParaClassificar)
        {
            return null;
        }

        return imc switch
        {
            < 18.5 => FaixaImc.Baixo,
            < 25 => FaixaImc.Adequado,
            < 30 => FaixaImc.Sobrepeso,
            _ => FaixaImc.Obesidade
        };
    }
}
