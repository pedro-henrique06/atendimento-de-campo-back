using AtendimentoDeCampo.Api.Contratos;
using AtendimentoDeCampo.Api.Servicos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AtendimentoDeCampo.Api.Controllers;

[ApiController]
[AllowAnonymous]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly ServicoAutenticacao _servico;

    public AuthController(ServicoAutenticacao servico) => _servico = servico;

    /// <summary>
    /// Cria uma conta. Ela nasce pendente: quem se registra nao acessa nada ate
    /// um administrador aprovar.
    /// </summary>
    [HttpPost("registrar")]
    public async Task<ActionResult<ProfissionalDto>> Registrar(
        [FromBody] RegistroRequest req,
        CancellationToken ct)
        => Ok(await _servico.RegistrarAsync(req, ct));

    /// <summary>Consulta se um usuario esta livre, enquanto a pessoa digita.</summary>
    [HttpGet("usuario-disponivel")]
    public async Task<ActionResult<UsuarioDisponivelResponse>> UsuarioDisponivel(
        [FromQuery] string usuario,
        CancellationToken ct)
        => Ok(new UsuarioDisponivelResponse(
            usuario,
            await _servico.UsuarioDisponivelAsync(usuario, ct)));

    [HttpPost("login")]
    public async Task<ActionResult<LoginResponse>> Login(
        [FromBody] LoginRequest req,
        CancellationToken ct)
    {
        var resultado = await _servico.AutenticarAsync(req, ct);

        if (resultado.Sucesso)
        {
            return Ok(resultado.Resposta);
        }

        // O motivo viaja como codigo, nao como frase pronta: quem traduz e a
        // interface, no idioma de quem esta lendo.
        return Unauthorized(new
        {
            motivo = resultado.Motivo.ToString(),
            detalhe = resultado.Detalhe
        });
    }
}
