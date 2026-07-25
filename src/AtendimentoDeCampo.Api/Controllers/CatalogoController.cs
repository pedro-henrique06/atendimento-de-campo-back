using System.Security.Claims;
using AtendimentoDeCampo.Api.Contratos;
using AtendimentoDeCampo.Api.Servicos;
using AtendimentoDeCampo.Domain;
using AtendimentoDeCampo.Infrastructure;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AtendimentoDeCampo.Api.Controllers;

[ApiController]
[Authorize]
[Route("api")]
public class CatalogoController : ControllerBase
{
    private readonly AtendimentoDbContext _db;

    public CatalogoController(AtendimentoDbContext db) => _db = db;

    private Idioma IdiomaAtual
        => Enum.TryParse<Idioma>(User.FindFirstValue("idioma"), out var i) ? i : Idioma.Pt;

    [AllowAnonymous]
    [HttpGet("bases")]
    public async Task<ActionResult<List<BaseDto>>> Bases(CancellationToken ct)
        => Ok(await _db.Bases
            .AsNoTracking()
            .Where(b => b.Ativa)
            .OrderBy(b => b.Nome)
            .Select(b => new BaseDto(b.Id, b.Nome, b.PrefixoCodigo, b.Ativa))
            .ToListAsync(ct));

    /// <summary>
    /// Catalogo de itens dispensaveis. A busca alimenta o autocomplete do
    /// formulario, que e o que impede o mesmo farmaco de ser digitado de cinco
    /// formas diferentes.
    /// </summary>
    [HttpGet("catalogo/itens")]
    public async Task<ActionResult<List<ItemCatalogoDto>>> Itens(
        [FromQuery] string? busca,
        [FromQuery] CategoriaItem? categoria,
        CancellationToken ct)
    {
        var query = _db.ItensCatalogo.AsNoTracking().Where(i => i.Ativo);

        if (categoria is not null)
        {
            query = query.Where(i => i.Categoria == categoria);
        }

        if (!string.IsNullOrWhiteSpace(busca))
        {
            var termo = busca.Trim();
            query = query.Where(i =>
                EF.Functions.ILike(i.Nome, $"%{termo}%") ||
                (i.PrincipioAtivo != null && EF.Functions.ILike(i.PrincipioAtivo, $"%{termo}%")));
        }

        // Ordem total. So por Nome nao basta: "Ibuprofeno 400 mg" e
        // "Ibuprofeno 600 mg" compartilham o nome, e sem desempate o banco
        // devolve os dois em ordem arbitraria, fazendo a lista do autocomplete
        // embaralhar entre uma digitacao e outra.
        var itens = await query
            .OrderBy(i => i.Nome)
            .ThenBy(i => i.Concentracao)
            .ThenBy(i => i.Forma)
            .Take(50)
            .ToListAsync(ct);
        return Ok(itens.Select(Mapeadores.ParaDto).ToList());
    }

    [HttpGet("catalogo/cid10")]
    public async Task<ActionResult<List<Cid10Dto>>> Cid10(
        [FromQuery] string? busca,
        CancellationToken ct)
    {
        var idioma = IdiomaAtual;
        var query = _db.Cid10s.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(busca))
        {
            var termo = busca.Trim();
            query = query.Where(c =>
                EF.Functions.ILike(c.Codigo, $"{termo}%") ||
                EF.Functions.ILike(c.DescricaoPt, $"%{termo}%") ||
                EF.Functions.ILike(c.DescricaoEs, $"%{termo}%") ||
                EF.Functions.ILike(c.DescricaoEn, $"%{termo}%"));
        }

        var lista = await query.OrderBy(c => c.Codigo).Take(50).ToListAsync(ct);
        return Ok(lista.Select(c => Mapeadores.ParaDto(c, idioma)).ToList());
    }
}
