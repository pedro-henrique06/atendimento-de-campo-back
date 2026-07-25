using AtendimentoDeCampo.Domain;

namespace AtendimentoDeCampo.Infrastructure.Servicos;

/// <summary>Alteracao de um campo, no formato usado pelo historico do prontuario.</summary>
public sealed record DiffCampo(string Campo, string? ValorAnterior, string? ValorNovo);

/// <summary>
/// Monta a trilha de auditoria do atendimento.
///
/// O sistema de referencia acertou nesta parte: o "Historico de alteracoes"
/// mostra quem mudou o que, campo a campo, com valor anterior e novo. Mantive o
/// mesmo comportamento e acrescentei a marcacao de edicao apos finalizacao, que
/// la nao existia: um atendimento finalizado as 15:49 aparecia editado as 15:56
/// e as 16:11 sem nenhuma indicacao de que a edicao veio depois do fecho.
/// </summary>
public sealed class RegistradorAuditoria
{
    private readonly AtendimentoDbContext _db;

    public RegistradorAuditoria(AtendimentoDbContext db) => _db = db;

    public async Task RegistrarAsync(
        Guid atendimentoId,
        Guid profissionalId,
        AcaoAuditoria acao,
        Especialidade? especialidade = null,
        CancellationToken ct = default)
    {
        _db.Auditorias.Add(new Auditoria
        {
            AtendimentoId = atendimentoId,
            ProfissionalId = profissionalId,
            Acao = acao,
            Especialidade = especialidade
        });

        await Task.CompletedTask;
    }

    /// <summary>
    /// Registra um conjunto de alteracoes de campo. Campos sem mudanca efetiva
    /// sao descartados para o historico nao encher de ruido.
    /// </summary>
    public void RegistrarDiffs(
        Guid atendimentoId,
        Guid profissionalId,
        IEnumerable<DiffCampo> diffs,
        Especialidade? especialidade,
        bool aposFinalizacao)
    {
        var acao = aposFinalizacao ? AcaoAuditoria.EditouAposFinalizacao : AcaoAuditoria.Editou;

        foreach (var diff in diffs)
        {
            if (string.Equals(diff.ValorAnterior, diff.ValorNovo, StringComparison.Ordinal))
            {
                continue;
            }

            _db.Auditorias.Add(new Auditoria
            {
                AtendimentoId = atendimentoId,
                ProfissionalId = profissionalId,
                Acao = acao,
                Especialidade = especialidade,
                Campo = diff.Campo,
                ValorAnterior = Truncar(diff.ValorAnterior),
                ValorNovo = Truncar(diff.ValorNovo)
            });
        }
    }

    /// <summary>Compara dois estados e devolve apenas os campos que mudaram.</summary>
    public static IReadOnlyList<DiffCampo> Comparar(
        IReadOnlyDictionary<string, string?> antes,
        IReadOnlyDictionary<string, string?> depois)
    {
        var diffs = new List<DiffCampo>();

        foreach (var (campo, valorNovo) in depois)
        {
            antes.TryGetValue(campo, out var valorAnterior);

            if (!string.Equals(valorAnterior, valorNovo, StringComparison.Ordinal))
            {
                diffs.Add(new DiffCampo(campo, valorAnterior, valorNovo));
            }
        }

        return diffs;
    }

    private static string? Truncar(string? valor)
        => valor is { Length: > 2000 } ? valor[..2000] : valor;
}
