namespace AtendimentoDeCampo.Domain.Servicos;

/// <summary>Qual medida saiu da faixa de referencia, numa linha da tabela.</summary>
[Flags]
public enum SinalForaDaFaixa
{
    Nenhum = 0,
    PressaoSistolica = 1,
    FrequenciaCardiaca = 2,
    FrequenciaRespiratoria = 4,
    SaturacaoO2 = 8,
    Temperatura = 16,
    Glicemia = 32
}

/// <summary>
/// Marca as medidas fora da faixa de referencia de adulto.
///
/// Serve para a tabela horaria destacar a linha que merece um segundo olhar —
/// e so isso. Nao e escore, nao classifica risco e nao sugere conduta: quem faz
/// isso e o <see cref="ProtocoloStart"/>, na triagem, contra um protocolo
/// publicado que pode ser conferido.
///
/// So vale para adulto, e por isso <see cref="Avaliar"/> exige a idade e
/// devolve <see cref="SinalForaDaFaixa.Nenhum"/> para crianca. Frequencia
/// cardiaca e respiratoria de crianca sao normalmente mais altas: o corte de
/// adulto marcaria como alterada a frequencia de um bebe saudavel, e uma tabela
/// que acende para todo mundo treina a equipe a ignorar o destaque — que e pior
/// do que nao ter destaque nenhum. Mesma regra que a
/// <see cref="CalculadoraImc"/> ja segue.
/// </summary>
public static class FaixasDeSinaisVitais
{
    /// <summary>Idade a partir da qual as faixas de adulto se aplicam.</summary>
    public const int IdadeMinima = 12;

    public static SinalForaDaFaixa Avaliar(
        int? idade,
        int? pressaoSistolica,
        int? frequenciaCardiaca,
        int? frequenciaRespiratoria,
        int? saturacaoO2,
        double? temperaturaCelsius,
        int? glicemiaCapilar)
    {
        if (idade is null || idade < IdadeMinima)
        {
            return SinalForaDaFaixa.Nenhum;
        }

        var fora = SinalForaDaFaixa.Nenhum;

        if (pressaoSistolica is < 90 or > 180)
        {
            fora |= SinalForaDaFaixa.PressaoSistolica;
        }

        if (frequenciaCardiaca is < 50 or > 120)
        {
            fora |= SinalForaDaFaixa.FrequenciaCardiaca;
        }

        if (frequenciaRespiratoria is < 8 or > 24)
        {
            fora |= SinalForaDaFaixa.FrequenciaRespiratoria;
        }

        if (saturacaoO2 is < 92)
        {
            fora |= SinalForaDaFaixa.SaturacaoO2;
        }

        if (temperaturaCelsius is < 35 or >= 38)
        {
            fora |= SinalForaDaFaixa.Temperatura;
        }

        if (glicemiaCapilar is < 70 or > 200)
        {
            fora |= SinalForaDaFaixa.Glicemia;
        }

        return fora;
    }
}
