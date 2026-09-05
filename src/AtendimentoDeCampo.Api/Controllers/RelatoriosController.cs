using System.Security.Claims;
using AtendimentoDeCampo.Api.Contratos;
using AtendimentoDeCampo.Api.Servicos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AtendimentoDeCampo.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/relatorios")]
public class RelatoriosController : ControllerBase
{
    private readonly ServicoRelatorios _servico;

    public RelatoriosController(ServicoRelatorios servico) => _servico = servico;

    private Guid ProfissionalId
    {
        get
        {
            var sub = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");

            return Guid.TryParse(sub, out var id)
                ? id
                : throw new RegraDeNegocioException("Token sem identificacao do profissional.");
        }
    }

    /// <summary>
    /// Producao por profissional na base, no periodo.
    ///
    /// A coordenacao ve a equipe inteira; qualquer outra pessoa ve so a propria
    /// producao. Nao e escondida na tela e sim recusada aqui: producao alheia e
    /// avaliacao de desempenho, e quem responde por isso e a coordenacao.
    /// </summary>
    [HttpGet("producao")]
    public async Task<ActionResult<List<ProducaoProfissionalDto>>> Producao(
        [FromQuery] Guid baseId,
        [FromQuery] DateTime? de,
        [FromQuery] DateTime? ate,
        CancellationToken ct)
        => Ok(await _servico.ProducaoAsync(
            baseId,
            de,
            ate,
            User.IsInRole(Papeis.Administrador) ? null : ProfissionalId,
            ct));
}
