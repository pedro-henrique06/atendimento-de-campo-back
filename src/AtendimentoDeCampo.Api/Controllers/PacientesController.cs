using AtendimentoDeCampo.Api.Contratos;
using AtendimentoDeCampo.Api.Servicos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AtendimentoDeCampo.Api.Controllers;

/// <summary>
/// Identificacao do paciente antes de abrir o atendimento: sortear um codigo
/// para quem chega pela primeira vez, ou reencontrar quem ja tem um.
/// </summary>
[ApiController]
[Authorize]
[Route("api/pacientes")]
public class PacientesController : ControllerBase
{
    private readonly ServicoAtendimento _servico;

    public PacientesController(ServicoAtendimento servico) => _servico = servico;

    /// <summary>
    /// Sorteia um codigo livre para a tela exibir antes do cadastro existir.
    /// Nada e gravado aqui: o cadastro so nasce quando o formulario e salvo,
    /// com o consentimento marcado.
    /// </summary>
    [HttpGet("codigo-novo")]
    public async Task<ActionResult<CodigoNovoDto>> CodigoNovo(CancellationToken ct)
        => Ok(new CodigoNovoDto(await _servico.GerarCodigoPacienteUnicoAsync(ct)));

    /// <summary>Reencontra um paciente pelo codigo dele — ou pelo codigo de um atendimento dele.</summary>
    [HttpGet("codigo/{codigo}")]
    public async Task<ActionResult<PacienteConhecidoDto>> PorCodigo(string codigo, CancellationToken ct)
    {
        var achado = await _servico.ObterPacientePorCodigoAsync(codigo, ct);

        return achado is null
            ? NotFound(new { erro = "Nenhum paciente encontrado com esse codigo." })
            : Ok(achado);
    }
}
