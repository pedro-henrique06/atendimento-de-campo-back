using AtendimentoDeCampo.Api.Contratos;
using AtendimentoDeCampo.Domain;
using AtendimentoDeCampo.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace AtendimentoDeCampo.Api.Servicos;

/// <summary>
/// Producao por profissional.
///
/// Nao acrescenta dado nenhum ao banco: cada etapa ja gravava quem atendeu,
/// quando comecou e quando terminou. O que faltava era a leitura — e sem ela a
/// coordenacao nao tinha como responder quantos pacientes cada pessoa atendeu
/// nem quanto tempo cada fila consome, que e o que decide escala de plantao.
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
            So etapas concluidas entram. Uma etapa aberta nao tem duracao — teria
            a duracao do plantao inteiro de quem esqueceu de fechar, e um unico
            esquecimento assim deformaria a media de todo mundo.

            Etapas canceladas tambem ficam de fora: elas sao o caso de quem
            recebeu o paciente e encaminhou sem atender, e contar isso como
            producao infla a especialidade com atendimento que nao aconteceu.
        */
        var query = _db.Etapas
            .AsNoTracking()
            .Where(e =>
                e.Atendimento!.BaseId == baseId &&
                e.Status == StatusEtapa.Concluida &&
                e.ProfissionalId != null &&
                e.IniciadaEm != null &&
                e.ConcluidaEm != null &&
                e.ConcluidaEm >= inicio &&
                e.ConcluidaEm <= fim);

        if (somenteEste is not null)
        {
            query = query.Where(e => e.ProfissionalId == somenteEste);
        }

        var etapas = await query
            .Select(e => new EtapaMedida(
                e.ProfissionalId!.Value,
                e.Profissional!.Nome,
                e.Profissional.Funcao,
                e.Profissional.ConselhoTipo,
                e.Profissional.Registro,
                e.Especialidade,
                e.IniciadaEm!.Value,
                e.ConcluidaEm!.Value))
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
