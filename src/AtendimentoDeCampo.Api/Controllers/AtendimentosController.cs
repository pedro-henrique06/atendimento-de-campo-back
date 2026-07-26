using System.Security.Claims;
using AtendimentoDeCampo.Api.Contratos;
using AtendimentoDeCampo.Api.Servicos;
using AtendimentoDeCampo.Domain;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AtendimentoDeCampo.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/atendimentos")]
public class AtendimentosController : ControllerBase
{
    private readonly ServicoAtendimento _servico;

    public AtendimentosController(ServicoAtendimento servico) => _servico = servico;

    private Guid ProfissionalId
    {
        get
        {
            var sub = User.FindFirstValue(ClaimTypes.NameIdentifier)
                ?? User.FindFirstValue("sub");

            return Guid.TryParse(sub, out var id)
                ? id
                : throw new RegraDeNegocioException("Token sem identificacao do profissional.");
        }
    }

    private bool EhAdministrador => User.IsInRole(Papeis.Administrador);

    /// <summary>Lista atendimentos da base, com filtro por fila, risco e busca livre.</summary>
    [HttpGet]
    public async Task<ActionResult<List<AtendimentoResumoDto>>> Listar(
        [FromQuery] Guid baseId,
        [FromQuery] Especialidade? fila,
        [FromQuery] ClassificacaoRisco? risco,
        [FromQuery] string? busca,
        [FromQuery] bool meus,
        [FromQuery] bool ocultarAssumidos,
        CancellationToken ct)
        => Ok(await _servico.ListarAsync(
            baseId,
            fila,
            risco,
            busca,
            meus ? ProfissionalId : null,
            ocultarAssumidos,
            ProfissionalId,
            ct));

    /// <summary>Assume a etapa: ela sai da fila de quem esta livre.</summary>
    [HttpPost("{id:guid}/etapas/{especialidade}/assumir")]
    public async Task<ActionResult<AtendimentoResumoDto>> Assumir(
        Guid id,
        Especialidade especialidade,
        CancellationToken ct)
        => Ok(await _servico.AssumirEtapaAsync(id, especialidade, ProfissionalId, ct));

    /// <summary>Devolve a etapa para a fila.</summary>
    [HttpPost("{id:guid}/etapas/{especialidade}/liberar")]
    public async Task<ActionResult<AtendimentoResumoDto>> Liberar(
        Guid id,
        Especialidade especialidade,
        CancellationToken ct)
        => Ok(await _servico.LiberarEtapaAsync(id, especialidade, ProfissionalId, EhAdministrador, ct));

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ProntuarioDto>> Obter(Guid id, CancellationToken ct)
        => Ok(await _servico.ObterProntuarioAsync(id, ct));

    /// <summary>Busca pelo codigo curto, que e o identificador usado na fila.</summary>
    [HttpGet("codigo/{codigo}")]
    public async Task<ActionResult<ProntuarioDto>> ObterPorCodigo(string codigo, CancellationToken ct)
    {
        var id = await _servico.ResolverPorCodigoAsync(codigo, ct);

        return id is null
            ? NotFound(new { erro = "Atendimento nao encontrado." })
            : Ok(await _servico.ObterProntuarioAsync(id.Value, ct));
    }

    [HttpPost]
    public async Task<ActionResult<ProntuarioDto>> Criar(
        [FromBody] CriarAtendimentoRequest req,
        CancellationToken ct)
    {
        var prontuario = await _servico.CriarAsync(req, ProfissionalId, ct);
        return CreatedAtAction(nameof(Obter), new { id = prontuario.Id }, prontuario);
    }

    [HttpPut("{id:guid}/triagem")]
    public async Task<ActionResult<SugestaoStartDto?>> RegistrarTriagem(
        Guid id,
        [FromBody] RegistrarTriagemRequest req,
        CancellationToken ct)
        => Ok(await _servico.RegistrarTriagemAsync(id, req, ProfissionalId, ct));

    [HttpPut("{id:guid}/consulta")]
    public async Task<IActionResult> RegistrarConsulta(
        Guid id,
        [FromBody] RegistrarConsultaRequest req,
        CancellationToken ct)
    {
        await _servico.RegistrarConsultaAsync(id, req, ProfissionalId, ct);
        return NoContent();
    }

    [HttpPut("{id:guid}/odontologia")]
    public async Task<IActionResult> RegistrarOdontologia(
        Guid id,
        [FromBody] RegistrarOdontologiaRequest req,
        CancellationToken ct)
    {
        await _servico.RegistrarOdontologiaAsync(id, req, ProfissionalId, ct);
        return NoContent();
    }

    [HttpPut("{id:guid}/enfermagem")]
    public async Task<IActionResult> RegistrarEnfermagem(
        Guid id,
        [FromBody] RegistrarEnfermagemRequest req,
        CancellationToken ct)
    {
        await _servico.RegistrarEnfermagemAsync(id, req, ProfissionalId, ct);
        return NoContent();
    }

    [HttpPost("{id:guid}/finalizar")]
    public async Task<IActionResult> Finalizar(
        Guid id,
        [FromBody] FinalizarAtendimentoRequest req,
        CancellationToken ct)
    {
        await _servico.FinalizarAsync(id, req, ProfissionalId, ct);
        return NoContent();
    }

    /// <summary>Reabre um atendimento finalizado. Exige justificativa.</summary>
    [HttpPost("{id:guid}/reabrir")]
    public async Task<IActionResult> Reabrir(
        Guid id,
        [FromBody] FinalizarAtendimentoRequest req,
        CancellationToken ct)
    {
        await _servico.ReabrirAsync(id, req, ProfissionalId, ct);
        return NoContent();
    }
}
