namespace AtendimentoDeCampo.Domain.Servicos;

/// <summary>Tempo que o paciente passou em uma fila.</summary>
public sealed record EsperaNaFila(
    Especialidade Especialidade,
    DateTime EntrouEm,
    DateTime? SaiuEm,
    TimeSpan? Espera)
{
    /// <summary>Fila ainda em aberto: o paciente nao foi chamado ate agora.</summary>
    public bool EmAberto => SaiuEm is null;
}

/// <summary>
/// Calculo dos tempos de espera exibidos no prontuario e agregados no relatorio
/// "Tempo de espera" por area.
/// </summary>
public static class TempoDeFila
{
    public static EsperaNaFila Calcular(PassagemFila passagem, DateTime? agora = null)
    {
        var fim = passagem.SaiuEm ?? agora;
        var espera = fim is null ? (TimeSpan?)null : fim.Value - passagem.EntrouEm;

        // Relogios de dispositivos em campo podem estar dessincronizados; uma
        // espera negativa e erro de relogio, nao informacao clinica.
        if (espera is { Ticks: < 0 })
        {
            espera = TimeSpan.Zero;
        }

        return new EsperaNaFila(passagem.Especialidade, passagem.EntrouEm, passagem.SaiuEm, espera);
    }

    public static IReadOnlyList<EsperaNaFila> Calcular(
        IEnumerable<PassagemFila> passagens,
        DateTime? agora = null)
        => passagens
            .OrderBy(p => p.EntrouEm)
            .Select(p => Calcular(p, agora))
            .ToList();

    /// <summary>
    /// Espera mediana por especialidade, considerando apenas filas concluidas.
    ///
    /// Mediana, e nao media: em campo um unico atendimento esquecido em aberto
    /// por horas distorce a media e faz o painel mentir sobre a fila.
    /// </summary>
    public static IReadOnlyDictionary<Especialidade, TimeSpan> MedianaPorEspecialidade(
        IEnumerable<PassagemFila> passagens)
    {
        return passagens
            .Where(p => p.SaiuEm is not null)
            .Select(p => Calcular(p))
            .Where(e => e.Espera is not null)
            .GroupBy(e => e.Especialidade)
            .ToDictionary(
                g => g.Key,
                g => Mediana(g.Select(e => e.Espera!.Value).ToList()));
    }

    private static TimeSpan Mediana(List<TimeSpan> valores)
    {
        valores.Sort();
        var meio = valores.Count / 2;

        return valores.Count % 2 == 1
            ? valores[meio]
            : TimeSpan.FromTicks((valores[meio - 1].Ticks + valores[meio].Ticks) / 2);
    }
}
