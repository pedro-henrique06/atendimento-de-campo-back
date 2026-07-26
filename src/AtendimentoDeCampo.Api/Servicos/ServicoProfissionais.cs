using AtendimentoDeCampo.Api.Contratos;
using AtendimentoDeCampo.Domain;
using AtendimentoDeCampo.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace AtendimentoDeCampo.Api.Servicos;

/// <summary>
/// Gestao de contas pelo administrador: aprovar, recusar, desativar e conceder
/// permissao de administracao.
/// </summary>
public sealed class ServicoProfissionais
{
    private readonly AtendimentoDbContext _db;

    public ServicoProfissionais(AtendimentoDbContext db) => _db = db;

    public async Task<List<ProfissionalDto>> ListarAsync(
        StatusConta? status,
        string? busca,
        CancellationToken ct = default)
    {
        var query = _db.Profissionais.AsNoTracking();

        if (status is not null)
        {
            query = query.Where(p => p.Status == status);
        }

        if (!string.IsNullOrWhiteSpace(busca))
        {
            var termo = busca.Trim();
            query = query.Where(p =>
                EF.Functions.ILike(p.Nome, $"%{termo}%") ||
                EF.Functions.ILike(p.Usuario, $"%{termo}%") ||
                (p.Registro != null && EF.Functions.ILike(p.Registro, $"%{termo}%")));
        }

        var lista = await query
            // Pendentes primeiro: e a fila de trabalho do administrador.
            .OrderBy(p => p.Status == StatusConta.Pendente ? 0 : 1)
            .ThenByDescending(p => p.CriadoEm)
            .Take(200)
            .ToListAsync(ct);

        return lista.Select(ServicoAutenticacao.ParaDto).ToList();
    }

    public async Task<int> ContarPendentesAsync(CancellationToken ct = default)
        => await _db.Profissionais.CountAsync(p => p.Status == StatusConta.Pendente, ct);

    public async Task<ProfissionalDto> AprovarAsync(
        Guid id,
        Guid administradorId,
        CancellationToken ct = default)
    {
        var profissional = await CarregarAsync(id, ct);

        if (profissional.Status == StatusConta.Ativa)
        {
            throw new RegraDeNegocioException("Esta conta ja esta ativa.");
        }

        profissional.Status = StatusConta.Ativa;
        profissional.MotivoRecusa = null;
        profissional.RevisadoPorId = administradorId;
        profissional.RevisadoEm = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);

        return ServicoAutenticacao.ParaDto(profissional);
    }

    public async Task<ProfissionalDto> RecusarAsync(
        Guid id,
        string? motivo,
        Guid administradorId,
        CancellationToken ct = default)
    {
        var profissional = await CarregarAsync(id, ct);

        if (string.IsNullOrWhiteSpace(motivo))
        {
            // Sem motivo a pessoa fica sem saber se errou algum dado ou se foi
            // recusada de proposito, e volta a tentar criar conta.
            throw new RegraDeNegocioException("Informe o motivo da recusa.");
        }

        ImpedirAutoAlteracao(id, administradorId, "recusar a propria conta");

        profissional.Status = StatusConta.Recusada;
        profissional.MotivoRecusa = motivo.Trim();
        profissional.RevisadoPorId = administradorId;
        profissional.RevisadoEm = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);

        return ServicoAutenticacao.ParaDto(profissional);
    }

    public async Task<ProfissionalDto> DesativarAsync(
        Guid id,
        Guid administradorId,
        CancellationToken ct = default)
    {
        var profissional = await CarregarAsync(id, ct);

        ImpedirAutoAlteracao(id, administradorId, "desativar a propria conta");
        await ImpedirRemoverUltimoAdministradorAsync(profissional, ct);

        profissional.Status = StatusConta.Desativada;
        profissional.RevisadoPorId = administradorId;
        profissional.RevisadoEm = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);

        return ServicoAutenticacao.ParaDto(profissional);
    }

    public async Task<ProfissionalDto> DefinirAdministradorAsync(
        Guid id,
        bool ehAdministrador,
        Guid administradorId,
        CancellationToken ct = default)
    {
        var profissional = await CarregarAsync(id, ct);

        if (!ehAdministrador)
        {
            ImpedirAutoAlteracao(id, administradorId, "remover a propria permissao de administrador");
            await ImpedirRemoverUltimoAdministradorAsync(profissional, ct);
        }

        if (ehAdministrador && profissional.Status != StatusConta.Ativa)
        {
            throw new RegraDeNegocioException("Aprove a conta antes de torna-la administradora.");
        }

        profissional.EhAdministrador = ehAdministrador;
        await _db.SaveChangesAsync(ct);

        return ServicoAutenticacao.ParaDto(profissional);
    }

    private async Task<Profissional> CarregarAsync(Guid id, CancellationToken ct)
        => await _db.Profissionais.FirstOrDefaultAsync(p => p.Id == id, ct)
            ?? throw new RegraDeNegocioException("Profissional nao encontrado.");

    private static void ImpedirAutoAlteracao(Guid id, Guid administradorId, string acao)
    {
        if (id == administradorId)
        {
            throw new RegraDeNegocioException($"Voce nao pode {acao}.");
        }
    }

    /// <summary>
    /// Sem administrador ativo ninguem aprova mais nada, e o sistema trava sem
    /// caminho de volta pela interface.
    /// </summary>
    private async Task ImpedirRemoverUltimoAdministradorAsync(Profissional alvo, CancellationToken ct)
    {
        if (!alvo.EhAdministrador)
        {
            return;
        }

        var outros = await _db.Profissionais.CountAsync(
            p => p.EhAdministrador && p.Status == StatusConta.Ativa && p.Id != alvo.Id,
            ct);

        if (outros == 0)
        {
            throw new RegraDeNegocioException(
                "Este e o unico administrador ativo. Promova outro antes de remove-lo.");
        }
    }
}
