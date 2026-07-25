namespace AtendimentoDeCampo.Domain.Servicos;

public sealed record ResultadoAlertaAlergia(bool DeveExibirAlerta, string? Texto);

/// <summary>
/// Regra do alerta de alergia exibido no topo do prontuario.
///
/// CORRIGE um defeito de seguranca clinica do sistema de referencia: la a
/// alergia era um campo de texto livre e a interface exibia o alerta vermelho
/// para qualquer valor nao vazio. Pacientes cujo registro dizia literalmente
/// "Nega alergia medicamentosa" apareciam com alerta vermelho de alergia.
///
/// O problema nao e estetico. Alerta que aparece em todo mundo deixa de ser
/// lido, e o paciente realmente alergico perde a protecao que o alerta existe
/// para dar. Aqui o alerta so dispara quando ha alergia efetivamente registrada.
/// </summary>
public static class AlertaAlergia
{
    public static ResultadoAlertaAlergia Avaliar(StatusAlergia status, string? descricao)
    {
        switch (status)
        {
            case StatusAlergia.PossuiAlergia:
                var texto = string.IsNullOrWhiteSpace(descricao)
                    ? "Alergia registrada sem descricao"
                    : descricao.Trim();
                return new ResultadoAlertaAlergia(true, texto);

            case StatusAlergia.SemAlergiaConhecida:
            case StatusAlergia.NaoPerguntado:
            default:
                return new ResultadoAlertaAlergia(false, null);
        }
    }

    /// <summary>
    /// Coerencia entre estado e descricao, aplicada na entrada da API.
    /// Impede que "possui alergia" fique sem descricao e que uma descricao
    /// sobreviva a um estado que afirma ausencia de alergia.
    /// </summary>
    public static IReadOnlyList<string> Validar(StatusAlergia status, string? descricao)
    {
        var erros = new List<string>();
        var temDescricao = !string.IsNullOrWhiteSpace(descricao);

        if (status == StatusAlergia.PossuiAlergia && !temDescricao)
        {
            erros.Add("Informe quais alergias o paciente possui.");
        }

        if (status != StatusAlergia.PossuiAlergia && temDescricao)
        {
            erros.Add(
                "Ha descricao de alergia, mas o estado informado nao e 'possui alergia'. " +
                "Ajuste o estado ou limpe a descricao.");
        }

        return erros;
    }
}
