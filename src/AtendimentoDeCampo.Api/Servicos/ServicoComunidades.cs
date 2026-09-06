using AtendimentoDeCampo.Api.Contratos;
using AtendimentoDeCampo.Domain;
using AtendimentoDeCampo.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace AtendimentoDeCampo.Api.Servicos;

/// <summary>
/// Lista de comunidades, mantida pela coordenacao.
///
/// A lista existe para que a contagem por comunidade feche. Digitado a mao no
/// cadastro do paciente, o mesmo lugar viraria "Vila Uniao", "vila uniao" e
/// "V. Uniao" na mesma estatistica — e e essa contagem que orienta onde montar a
/// proxima base.
/// </summary>
public sealed class ServicoComunidades
{
    private readonly AtendimentoDbContext _db;

    public ServicoComunidades(AtendimentoDbContext db) => _db = db;

    /// <summary>
    /// Comunidades ativas, para o cadastro do paciente escolher.
    /// </summary>
    public async Task<List<ComunidadeDto>> AtivasAsync(CancellationToken ct = default)
        => await _db.Comunidades
            .AsNoTracking()
            .Where(c => c.Ativa)
            .OrderBy(c => c.Nome)
            .Select(c => new ComunidadeDto(c.Id, c.Nome, c.Ativa))
            .ToListAsync(ct);

    /// <summary>
    /// Todas, inclusive as inativas, com quantos pacientes cada uma tem — que e
    /// o que a coordenacao olha para decidir se ainda faz sentido manter.
    /// </summary>
    public async Task<List<ComunidadeAdminDto>> ListarAsync(CancellationToken ct = default)
        => await _db.Comunidades
            .AsNoTracking()
            .OrderByDescending(c => c.Ativa)
            .ThenBy(c => c.Nome)
            .Select(c => new ComunidadeAdminDto(
                c.Id,
                c.Nome,
                c.Ativa,
                c.CriadaEm,
                c.Pacientes.Count))
            .ToListAsync(ct);

    public async Task<ComunidadeAdminDto> CriarAsync(
        SalvarComunidadeRequest req,
        CancellationToken ct = default)
    {
        var nome = NomeValidado(req.Nome);

        await ImpedirNomeRepetidoAsync(nome, null, ct);

        var comunidade = new Comunidade { Nome = nome };

        _db.Comunidades.Add(comunidade);
        await _db.SaveChangesAsync(ct);

        return new ComunidadeAdminDto(comunidade.Id, comunidade.Nome, comunidade.Ativa, comunidade.CriadaEm, 0);
    }

    /// <summary>
    /// Renomear e sempre permitido: corrigir a grafia de um lugar nao muda de
    /// que lugar os pacientes ja cadastrados sao.
    /// </summary>
    public async Task<ComunidadeAdminDto> RenomearAsync(
        Guid id,
        SalvarComunidadeRequest req,
        CancellationToken ct = default)
    {
        var comunidade = await CarregarAsync(id, ct);
        var nome = NomeValidado(req.Nome);

        await ImpedirNomeRepetidoAsync(nome, id, ct);

        comunidade.Nome = nome;
        await _db.SaveChangesAsync(ct);

        return await UmAsync(id, ct);
    }

    /// <summary>
    /// Desativar tira do cadastro novo sem apagar historico.
    ///
    /// Nao ha exclusao de verdade de proposito: apagar a comunidade apagaria de
    /// onde vieram os pacientes ja atendidos, e a estatistica do ano passado
    /// deixaria de bater com a de hoje.
    /// </summary>
    public async Task<ComunidadeAdminDto> DefinirAtivaAsync(
        Guid id,
        bool ativa,
        CancellationToken ct = default)
    {
        var comunidade = await CarregarAsync(id, ct);

        comunidade.Ativa = ativa;
        await _db.SaveChangesAsync(ct);

        return await UmAsync(id, ct);
    }

    private async Task<ComunidadeAdminDto> UmAsync(Guid id, CancellationToken ct)
        => (await ListarAsync(ct)).First(c => c.Id == id);

    private async Task<Comunidade> CarregarAsync(Guid id, CancellationToken ct)
        => await _db.Comunidades.FirstOrDefaultAsync(c => c.Id == id, ct)
            ?? throw new RegraDeNegocioException("Comunidade nao encontrada.");

    private static string NomeValidado(string? nome)
    {
        var limpo = nome?.Trim();

        if (string.IsNullOrWhiteSpace(limpo) || limpo.Length < 2)
        {
            throw new RegraDeNegocioException("Informe o nome da comunidade.");
        }

        return limpo;
    }

    private async Task ImpedirNomeRepetidoAsync(string nome, Guid? exceto, CancellationToken ct)
    {
        var repetido = await _db.Comunidades.AnyAsync(
            c => c.Id != exceto && EF.Functions.ILike(c.Nome, nome),
            ct);

        if (repetido)
        {
            // Duas com o mesmo nome devolveriam ao cadastro a ambiguidade que a
            // lista veio eliminar.
            throw new RegraDeNegocioException($"Ja existe uma comunidade chamada \"{nome}\".");
        }
    }
}
