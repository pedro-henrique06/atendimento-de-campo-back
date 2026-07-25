namespace AtendimentoDeCampo.Domain.Servicos;

/// <summary>Achados objetivos coletados na triagem, na ordem do algoritmo START.</summary>
public sealed record AchadosStart
{
    /// <summary>Paciente consegue deambular / caminhar sozinho.</summary>
    public bool Deambula { get; init; }

    public bool RespiraEspontaneamente { get; init; } = true;

    /// <summary>Se nao respira espontaneamente, voltou a respirar apos abertura de via aerea.</summary>
    public bool RespiraAposAberturaViaAerea { get; init; }

    public int? FrequenciaRespiratoria { get; init; }

    public bool PulsoRadialPresente { get; init; } = true;

    public int? TempoEnchimentoCapilarSegundos { get; init; }

    /// <summary>Paciente obedece comandos simples (nivel de consciencia).</summary>
    public bool ObedeceComandos { get; init; } = true;
}

/// <summary>Sugestao de classificacao com a justificativa que a produziu.</summary>
public sealed record SugestaoStart(ClassificacaoRisco Classificacao, string Motivo);

/// <summary>
/// Implementacao do algoritmo START (Simple Triage And Rapid Treatment).
///
/// O resultado e uma SUGESTAO. Quem classifica e o profissional de triagem: a
/// classificacao gravada no atendimento e sempre a escolhida por ele, e a
/// sugestao serve para apoiar e para registrar divergencias. Software clinico
/// nao decide no lugar de quem esta com o paciente na frente.
/// </summary>
public static class ProtocoloStart
{
    /// <summary>Frequencia respiratoria acima da qual o START classifica como imediato.</summary>
    public const int FrequenciaRespiratoriaCritica = 30;

    /// <summary>Frequencia respiratoria abaixo da qual ha bradipneia critica.</summary>
    public const int FrequenciaRespiratoriaMinima = 10;

    /// <summary>Tempo de enchimento capilar, em segundos, que indica ma perfusao.</summary>
    public const int EnchimentoCapilarCritico = 2;

    public static SugestaoStart Avaliar(AchadosStart achados)
    {
        // 1. Deambula -> lesao leve, atendimento nao urgente.
        if (achados.Deambula)
        {
            return new SugestaoStart(
                ClassificacaoRisco.Verde,
                "Paciente deambula: classificado como nao urgente pelo START.");
        }

        // 2. Sem respiracao mesmo apos abertura de via aerea.
        if (!achados.RespiraEspontaneamente && !achados.RespiraAposAberturaViaAerea)
        {
            return new SugestaoStart(
                ClassificacaoRisco.Preto,
                "Ausencia de respiracao apos abertura de via aerea.");
        }

        // 3. Voltou a respirar apenas apos manobra de via aerea.
        if (!achados.RespiraEspontaneamente)
        {
            return new SugestaoStart(
                ClassificacaoRisco.Vermelho,
                "Respiracao restabelecida apenas apos abertura de via aerea.");
        }

        // 4. Frequencia respiratoria fora da faixa tolerada.
        if (achados.FrequenciaRespiratoria is int fr)
        {
            if (fr > FrequenciaRespiratoriaCritica)
            {
                return new SugestaoStart(
                    ClassificacaoRisco.Vermelho,
                    $"Frequencia respiratoria {fr} irpm, acima de {FrequenciaRespiratoriaCritica}.");
            }

            if (fr < FrequenciaRespiratoriaMinima)
            {
                return new SugestaoStart(
                    ClassificacaoRisco.Vermelho,
                    $"Frequencia respiratoria {fr} irpm, abaixo de {FrequenciaRespiratoriaMinima}.");
            }
        }

        // 5. Perfusao: pulso radial ausente ou enchimento capilar lentificado.
        if (!achados.PulsoRadialPresente)
        {
            return new SugestaoStart(
                ClassificacaoRisco.Vermelho,
                "Pulso radial ausente.");
        }

        if (achados.TempoEnchimentoCapilarSegundos is int tec && tec > EnchimentoCapilarCritico)
        {
            return new SugestaoStart(
                ClassificacaoRisco.Vermelho,
                $"Enchimento capilar {tec}s, acima de {EnchimentoCapilarCritico}s.");
        }

        // 6. Nivel de consciencia.
        if (!achados.ObedeceComandos)
        {
            return new SugestaoStart(
                ClassificacaoRisco.Vermelho,
                "Paciente nao obedece comandos simples.");
        }

        // 7. Respira, perfunde e responde, mas nao deambula.
        return new SugestaoStart(
            ClassificacaoRisco.Amarelo,
            "Nao deambula, porem com respiracao, perfusao e consciencia preservadas.");
    }

    /// <summary>
    /// Indica se a classificacao escolhida pelo profissional diverge da sugerida.
    /// A divergencia nao bloqueia nada: ela e registrada na auditoria.
    /// </summary>
    public static bool DivergeDaSugestao(AchadosStart achados, ClassificacaoRisco escolhida)
        => Avaliar(achados).Classificacao != escolhida;
}
