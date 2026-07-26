using AtendimentoDeCampo.Api.Contratos;
using AtendimentoDeCampo.Api.Servicos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AtendimentoDeCampo.Api.Controllers;

/// <summary>
/// Cadastro das bases. Ler e para quem esta autenticado; criar, editar e
/// desativar e so para a coordenacao.
/// </summary>
[ApiController]
[Authorize(Roles = Papeis.Administrador)]
[Route("api/bases")]
public class BasesController : ControllerBase
{
    private readonly ServicoBases _servico;

    public BasesController(ServicoBases servico) => _servico = servico;

    /// <summary>Todas as bases, inclusive as inativas — que e o que a gestao precisa ver.</summary>
    [HttpGet("todas")]
    public async Task<ActionResult<List<BaseAdminDto>>> Todas(CancellationToken ct)
        => Ok(await _servico.ListarAsync(ct));

    [HttpGet("prefixo-sugerido")]
    public async Task<ActionResult<PrefixoSugeridoDto>> PrefixoSugerido(
        [FromQuery] string nome,
        CancellationToken ct)
        => Ok(new PrefixoSugeridoDto(await _servico.SugerirPrefixoAsync(nome, ct)));

    [HttpPost]
    public async Task<ActionResult<BaseAdminDto>> Criar(
        [FromBody] SalvarBaseRequest req,
        CancellationToken ct)
        => Ok(await _servico.CriarAsync(req, ct));

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<BaseAdminDto>> Atualizar(
        Guid id,
        [FromBody] SalvarBaseRequest req,
        CancellationToken ct)
        => Ok(await _servico.AtualizarAsync(id, req, ct));

    [HttpPost("{id:guid}/ativa")]
    public async Task<ActionResult<BaseAdminDto>> DefinirAtiva(
        Guid id,
        [FromBody] DefinirAtivaRequest req,
        CancellationToken ct)
        => Ok(await _servico.DefinirAtivaAsync(id, req.Ativa, ct));
}
