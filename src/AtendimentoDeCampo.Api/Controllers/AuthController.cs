using System.Security.Claims;
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
    /// Troca a propria senha. Unica rota liberada enquanto a senha ainda e a
    /// provisoria que a coordenacao entregou.
    /// </summary>
    [Authorize]
    [HttpPost("trocar-senha")]
    public async Task<ActionResult<LoginResponse>> TrocarSenha(
        [FromBody] TrocarSenhaRequest req,
        CancellationToken ct)
    {
        var sub = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");

        if (!Guid.TryParse(sub, out var id))
        {
            throw new RegraDeNegocioException("Token sem identificacao do profissional.");
        }

        return Ok(await _servico.TrocarSenhaAsync(id, req, ct));
    }

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
