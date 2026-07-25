namespace AtendimentoDeCampo.Domain.Servicos;

/// <summary>Faixas etarias usadas na piramide e nos relatorios demograficos.</summary>
public enum FaixaEtaria
{
    De0a4 = 0,
    De5a14 = 1,
    De15a24 = 2,
    De25a44 = 3,
    De45a64 = 4,
    De65Mais = 5
}

/// <summary>
/// Idade do paciente. Em campo a data de nascimento frequentemente e
/// desconhecida, entao o cadastro aceita idade aproximada e o calculo precisa
/// lidar com as duas origens sem inventar precisao que nao existe.
/// </summary>
public static class CalculadoraIdade
{
    public static int? Calcular(DateOnly? dataNascimento, int? idadeAproximada, DateOnly? hoje = null)
    {
        if (dataNascimento is null)
        {
            return idadeAproximada;
        }

        var referencia = hoje ?? DateOnly.FromDateTime(DateTime.UtcNow);
        var nascimento = dataNascimento.Value;

        if (nascimento > referencia)
        {
            return null;
        }

        var idade = referencia.Year - nascimento.Year;

        // Ainda nao fez aniversario neste ano.
        if (referencia < nascimento.AddYears(idade))
        {
            idade--;
        }

        return idade;
    }

    public static FaixaEtaria? Faixa(int? idade) => idade switch
    {
        null => null,
        < 0 => null,
        <= 4 => FaixaEtaria.De0a4,
        <= 14 => FaixaEtaria.De5a14,
        <= 24 => FaixaEtaria.De15a24,
        <= 44 => FaixaEtaria.De25a44,
        <= 64 => FaixaEtaria.De45a64,
        _ => FaixaEtaria.De65Mais
    };

    public static string Rotular(FaixaEtaria faixa) => faixa switch
    {
        FaixaEtaria.De0a4 => "0-4",
        FaixaEtaria.De5a14 => "5-14",
        FaixaEtaria.De15a24 => "15-24",
        FaixaEtaria.De25a44 => "25-44",
        FaixaEtaria.De45a64 => "45-64",
        FaixaEtaria.De65Mais => "65+",
        _ => "-"
    };
}
