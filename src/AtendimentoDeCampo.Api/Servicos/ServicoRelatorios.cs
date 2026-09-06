using AtendimentoDeCampo.Api.Contratos;
using AtendimentoDeCampo.Domain;
using AtendimentoDeCampo.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace AtendimentoDeCampo.Api.Servicos;

/// <summary>
/// Producao por profissional.
///
/// Le a passagem do paciente pela fila — quem atendeu, quando assumiu e quando
/// terminou —, que e o registro que sobrevive ao paciente voltar para uma fila
/// pela qual ja passou. Sem ela a coordenacao nao tem como responder quantos
/// pacientes cada pessoa atendeu nem quanto tempo cada fila consome, que e o que
/// decide escala de plantao.
/// </summary>
public sealed class ServicoRelatorios
{
    private readonly AtendimentoDbContext _db;

    public ServicoRelatorios(AtendimentoDbContext db) => _db = db;

    /// <summary>Janela padrao quando a tela nao manda periodo.</summary>
    public const int DiasPadrao = 7;

    public async Task<List<ProducaoProfissionalDto>> ProducaoAsync(
        Guid baseId,
        DateTime? de,
        DateTime? ate,
        Guid? somenteEste = null,
        CancellationToken ct = default)
    {
        var fim = ate ?? DateTime.UtcNow;
        var inicio = de ?? fim.AddDays(-DiasPadrao);

        if (inicio > fim)
        {
            throw new RegraDeNegocioException("O inicio do periodo e depois do fim.");
        }

        /*
            A conta e por passagem pela fila, e nao por etapa.

            A diferenca aparece quando o paciente volta: o clinico encaminha para
            a pediatria, a pediatria devolve, e a etapa da clinica geral — que e
            uma so, por indice unico — e reaberta e concluida de novo. Lida pela
            etapa, a segunda consulta apagaria a primeira, e o trabalho do
            primeiro medico sumiria da tabela.

            So passagem atendida e encerrada entra. Passagem aberta nao tem
            duracao: teria a duracao do plantao de quem esqueceu de fechar, e um
            esquecimento so deformaria a mediana de todo mundo. Passagem sem
            profissional e o caso de quem recebeu o paciente na fila errada e
            reencaminhou sem atender — nao e producao de ninguem.
        */
        var query = _db.PassagensFila
            .AsNoTracking()
            .Where(p =>
                p.Atendimento!.BaseId == baseId &&
                p.ProfissionalId != null &&
                p.AssumidaEm != null &&
                p.ConcluidaEm != null &&
                p.ConcluidaEm >= inicio &&
                p.ConcluidaEm <= fim);

        if (somenteEste is not null)
        {
            query = query.Where(p => p.ProfissionalId == somenteEste);
        }

        var etapas = await query
            .Select(p => new EtapaMedida(
                p.ProfissionalId!.Value,
                p.Profissional!.Nome,
                p.Profissional.Funcao,
                p.Profissional.ConselhoTipo,
                p.Profissional.Registro,
                p.Especialidade,
                p.AssumidaEm!.Value,
                p.ConcluidaEm!.Value))
            .ToListAsync(ct);

        return etapas
            .GroupBy(e => e.ProfissionalId)
            .Select(porPessoa =>
            {
                var primeira = porPessoa.First();
                var duracoes = porPessoa.Select(e => e.Minutos).ToList();

                return new ProducaoProfissionalDto(
                    porPessoa.Key,
                    primeira.Nome,
                    primeira.Funcao,
                    primeira.ConselhoTipo,
                    primeira.Registro,
                    porPessoa.Count(),
                    duracoes.Sum(),
                    Mediana(duracoes),
                    porPessoa
                        .GroupBy(e => e.Especialidade)
                        .Select(porFila => new ProducaoPorFilaDto(
                            porFila.Key,
                            porFila.Count(),
                            porFila.Sum(e => e.Minutos)))
                        .OrderByDescending(f => f.Atendimentos)
                        .ToList());
            })
            .OrderByDescending(p => p.Atendimentos)
            .ThenBy(p => p.Nome)
            .ToList();
    }

    /// <summary>Uma etapa concluida, reduzida ao que a conta precisa.</summary>
    private sealed record EtapaMedida(
        Guid ProfissionalId,
        string Nome,
        FuncaoProfissional Funcao,
        ConselhoTipo ConselhoTipo,
        string? Registro,
        Especialidade Especialidade,
        DateTime Inicio,
        DateTime Fim)
    {
        /// <remarks>
        /// Piso de um minuto: atendimento curto existe (uma dispensacao rapida)
        /// e arredondaria para zero, sumindo do total como se nao tivesse
        /// acontecido.
        /// </remarks>
        public int Minutos => Math.Max((int)Math.Round((Fim - Inicio).TotalMinutes), 1);
    }

    /// <summary>
    /// Mediana, e nao media.
    ///
    /// Quem assume uma etapa e sai para uma emergencia deixa a ficha aberta por
    /// horas; um caso desses puxa a media da pessoa para cima e faz a tabela
    /// mentir sobre o dia inteiro. A mediana ignora o extremo e continua
    /// descrevendo o atendimento tipico.
    /// </summary>
    private static int? Mediana(List<int> valores)
    {
        if (valores.Count == 0)
        {
            return null;
        }

        var ordenados = valores.OrderBy(v => v).ToList();
        var meio = ordenados.Count / 2;

        return ordenados.Count % 2 == 1
            ? ordenados[meio]
            : (int)Math.Round((ordenados[meio - 1] + ordenados[meio]) / 2.0);
    }
}
