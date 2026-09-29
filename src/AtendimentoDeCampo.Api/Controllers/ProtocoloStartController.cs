using AtendimentoDeCampo.Api.Contratos;
using AtendimentoDeCampo.Domain.Servicos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AtendimentoDeCampo.Api.Controllers;

/// <summary>
/// O algoritmo START avaliado sozinho, sem gravar nada.
///
/// A sugestao so existia como efeito de gravar a triagem: a tela mostrava o que
/// o protocolo diria *depois* que o profissional ja tinha escolhido a cor e
/// apertado salvar. Contestar uma decisao ja tomada e o pior momento possivel
/// para uma sugestao — ou ela chega a tempo de ajudar, ou vira aviso que se
/// aprende a fechar.
///
/// E um endpoint, e nao uma copia do algoritmo no navegador, de proposito:
/// logica clinica em duas linguagens diverge, e a divergencia apareceria como
/// duas classificacoes diferentes para o mesmo paciente — a da tela e a gravada
/// na auditoria. Aqui ha uma implementacao so, e e a mesma que julga a
/// divergencia na hora de gravar.
///
/// Sem banco e sem efeito: a resposta depende so do que veio no corpo.
/// </summary>
[ApiController]
[Authorize]
[Route("api/protocolo-start")]
public class ProtocoloStartController : ControllerBase
{
    /// <summary>
    /// A sugestao do START para estes achados.
    /// </summary>
    /// <remarks>
    /// POST, e nao GET, porque sao sete achados clinicos de um paciente: em
    /// GET eles virariam query string, e query string vai para o log de acesso
    /// de todo servidor e proxy no caminho.
    /// </remarks>
    [HttpPost]
    public ActionResult<SugestaoStartDto> Avaliar([FromBody] AchadosStartRequest req)
    {
        var sugestao = ProtocoloStart.Avaliar(new AchadosStart
        {
            Deambula = req.Deambula,
            RespiraEspontaneamente = req.RespiraEspontaneamente,
            RespiraAposAberturaViaAerea = req.RespiraAposAberturaViaAerea,
            FrequenciaRespiratoria = req.FrequenciaRespiratoria,
            PulsoRadialPresente = req.PulsoRadialPresente,
            TempoEnchimentoCapilarSegundos = req.TempoEnchimentoCapilarSegundos,
            ObedeceComandos = req.ObedeceComandos
        });

        // `Divergente` e falso aqui: ainda nao ha classificacao escolhida com
        // que divergir. Quem decide isso e a gravacao da triagem.
        return Ok(new SugestaoStartDto(sugestao.Classificacao, sugestao.Motivo, false));
    }
}
