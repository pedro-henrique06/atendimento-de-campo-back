using AtendimentoDeCampo.Api.Contratos;
using AtendimentoDeCampo.Api.Servicos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AtendimentoDeCampo.Api.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly ServicoAutenticacao _servico;

    public AuthController(ServicoAutenticacao servico) => _servico = servico;

    /// <summary>
    /// Login de campo. No primeiro acesso, a senha informada e a senha da
    /// equipe e a conta e criada nesse momento.
    /// </summary>
    [AllowAnonymous]
    [HttpPost("login")]
    public async Task<ActionResult<LoginResponse>> Login(
        [FromBody] LoginRequest req,
        CancellationToken ct)
    {
        var resultado = await _servico.AutenticarAsync(req, ct);

        return resultado.Sucesso
            ? Ok(resultado.Resposta)
            : Unauthorized(new { erro = resultado.Erro });
    }
}
