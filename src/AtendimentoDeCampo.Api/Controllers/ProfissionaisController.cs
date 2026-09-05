using System.Security.Claims;
using AtendimentoDeCampo.Api.Contratos;
using AtendimentoDeCampo.Api.Servicos;
using AtendimentoDeCampo.Domain;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AtendimentoDeCampo.Api.Controllers;

/// <summary>
/// Gestao de contas. Todo o controller exige permissao de administrador — a
/// autorizacao fica no servidor, e nao em esconder o botao na tela.
/// </summary>
[ApiController]
[Authorize(Roles = Papeis.Administrador)]
[Route("api/profissionais")]
public class ProfissionaisController : ControllerBase
{
    private readonly ServicoProfissionais _servico;

    public ProfissionaisController(ServicoProfissionais servico) => _servico = servico;

    private Guid AdministradorId
    {
        get
        {
            var sub = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");

            return Guid.TryParse(sub, out var id)
                ? id
                : throw new RegraDeNegocioException("Token sem identificacao do profissional.");
        }
    }

    [HttpGet]
    public async Task<ActionResult<List<ProfissionalDto>>> Listar(
        [FromQuery] StatusConta? status,
        [FromQuery] string? busca,
        CancellationToken ct)
        => Ok(await _servico.ListarAsync(status, busca, ct));

    /// <summary>
    /// Cadastra um profissional. Unica porta de entrada no sistema: nao existe
    /// auto-registro. A resposta traz a senha do primeiro acesso, e essa e a
    /// unica vez em que ela aparece.
    /// </summary>
    [HttpPost]
    public async Task<ActionResult<ContaCriadaDto>> Criar(
        [FromBody] CriarContaRequest req,
        CancellationToken ct)
        => Ok(await _servico.CriarAsync(req, AdministradorId, ct));

    /// <summary>Consulta se um usuario esta livre, enquanto a coordenacao digita.</summary>
    [HttpGet("usuario-disponivel")]
    public async Task<ActionResult<UsuarioDisponivelResponse>> UsuarioDisponivel(
        [FromQuery] string usuario,
        CancellationToken ct)
        => Ok(new UsuarioDisponivelResponse(
            usuario,
            await _servico.UsuarioDisponivelAsync(usuario, ct)));

    /// <summary>Muda a profissao e, com ela, a fila que a pessoa passa a ver.</summary>
    [HttpPost("{id:guid}/profissao")]
    public async Task<ActionResult<ProfissionalDto>> AlterarProfissao(
        Guid id,
        [FromBody] AlterarProfissaoRequest req,
        CancellationToken ct)
        => Ok(await _servico.AlterarProfissaoAsync(id, req.Funcao, req.Registro, ct));

    /// <summary>Sorteia uma senha provisoria nova, para quem perdeu a de acesso.</summary>
    [HttpPost("{id:guid}/redefinir-senha")]
    public async Task<ActionResult<ContaCriadaDto>> RedefinirSenha(Guid id, CancellationToken ct)
        => Ok(await _servico.RedefinirSenhaAsync(id, ct));

    /// <summary>Quantas contas aguardam aprovacao, para o aviso no cabecalho.</summary>
    [HttpGet("pendentes/total")]
    public async Task<ActionResult<int>> ContarPendentes(CancellationToken ct)
        => Ok(await _servico.ContarPendentesAsync(ct));

    [HttpPost("{id:guid}/aprovar")]
    public async Task<ActionResult<ProfissionalDto>> Aprovar(Guid id, CancellationToken ct)
        => Ok(await _servico.AprovarAsync(id, AdministradorId, ct));

    [HttpPost("{id:guid}/recusar")]
    public async Task<ActionResult<ProfissionalDto>> Recusar(
        Guid id,
        [FromBody] RecusarContaRequest req,
        CancellationToken ct)
        => Ok(await _servico.RecusarAsync(id, req.Motivo, AdministradorId, ct));

    [HttpPost("{id:guid}/desativar")]
    public async Task<ActionResult<ProfissionalDto>> Desativar(Guid id, CancellationToken ct)
        => Ok(await _servico.DesativarAsync(id, AdministradorId, ct));

    [HttpPost("{id:guid}/administrador")]
    public async Task<ActionResult<ProfissionalDto>> DefinirAdministrador(
        Guid id,
        [FromBody] DefinirAdministradorRequest req,
        CancellationToken ct)
        => Ok(await _servico.DefinirAdministradorAsync(id, req.EhAdministrador, AdministradorId, ct));
}
