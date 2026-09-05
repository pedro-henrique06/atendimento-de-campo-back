using AtendimentoDeCampo.Api.Contratos;
using AtendimentoDeCampo.Api.Servicos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AtendimentoDeCampo.Api.Controllers;

/// <summary>
/// Lista de comunidades. Ler as ativas e para quem cadastra paciente; manter a
/// lista e so para a coordenacao.
/// </summary>
[ApiController]
[Authorize]
[Route("api/comunidades")]
public class ComunidadesController : ControllerBase
{
    private readonly ServicoComunidades _servico;

    public ComunidadesController(ServicoComunidades servico) => _servico = servico;

    /// <summary>Ativas, para o cadastro do paciente escolher.</summary>
    [HttpGet]
    public async Task<ActionResult<List<ComunidadeDto>>> Ativas(CancellationToken ct)
        => Ok(await _servico.AtivasAsync(ct));

    /// <summary>Todas, inclusive as inativas — que e o que a gestao precisa ver.</summary>
    [Authorize(Roles = Papeis.Administrador)]
    [HttpGet("todas")]
    public async Task<ActionResult<List<ComunidadeAdminDto>>> Todas(CancellationToken ct)
        => Ok(await _servico.ListarAsync(ct));

    [Authorize(Roles = Papeis.Administrador)]
    [HttpPost]
    public async Task<ActionResult<ComunidadeAdminDto>> Criar(
        [FromBody] SalvarComunidadeRequest req,
        CancellationToken ct)
        => Ok(await _servico.CriarAsync(req, ct));

    [Authorize(Roles = Papeis.Administrador)]
    [HttpPut("{id:guid}")]
    public async Task<ActionResult<ComunidadeAdminDto>> Renomear(
        Guid id,
        [FromBody] SalvarComunidadeRequest req,
        CancellationToken ct)
        => Ok(await _servico.RenomearAsync(id, req, ct));

    [Authorize(Roles = Papeis.Administrador)]
    [HttpPost("{id:guid}/ativa")]
    public async Task<ActionResult<ComunidadeAdminDto>> DefinirAtiva(
        Guid id,
        [FromBody] DefinirAtivaRequest req,
        CancellationToken ct)
        => Ok(await _servico.DefinirAtivaAsync(id, req.Ativa, ct));
}
