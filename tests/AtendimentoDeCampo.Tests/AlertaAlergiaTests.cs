using AtendimentoDeCampo.Domain;
using AtendimentoDeCampo.Domain.Servicos;

namespace AtendimentoDeCampo.Tests;

/// <summary>
/// Testes do defeito de seguranca clinica mais grave encontrado no sistema de
/// referencia: o alerta vermelho de alergia disparava para qualquer texto
/// preenchido, inclusive "Nega alergia medicamentosa".
/// </summary>
public class AlertaAlergiaTests
{
    [Fact]
    public void PacienteQueNegaAlergiaNaoDisparaAlerta()
    {
        var resultado = AlertaAlergia.Avaliar(StatusAlergia.SemAlergiaConhecida, null);

        Assert.False(resultado.DeveExibirAlerta);
        Assert.Null(resultado.Texto);
    }

    [Fact]
    public void TextoNegandoAlergiaNaoViraAlerta()
    {
        // Este e o caso exato do prontuario analisado: o registro dizia
        // "Nega alergia medicamentosa" e a tela mostrava alerta vermelho.
        var resultado = AlertaAlergia.Avaliar(
            StatusAlergia.SemAlergiaConhecida,
            "Nega alergia medicamentosa");

        Assert.False(resultado.DeveExibirAlerta);
    }

    [Fact]
    public void NaoPerguntadoNaoDisparaAlerta()
    {
        Assert.False(AlertaAlergia.Avaliar(StatusAlergia.NaoPerguntado, null).DeveExibirAlerta);
    }

    [Fact]
    public void AlergiaRealDisparaAlertaComADescricao()
    {
        var resultado = AlertaAlergia.Avaliar(StatusAlergia.PossuiAlergia, "Dipirona, penicilina");

        Assert.True(resultado.DeveExibirAlerta);
        Assert.Equal("Dipirona, penicilina", resultado.Texto);
    }

    [Fact]
    public void AlergiaSemDescricaoAindaAlerta()
    {
        // Falta de descricao nao pode apagar o alerta: na duvida, alerta.
        var resultado = AlertaAlergia.Avaliar(StatusAlergia.PossuiAlergia, "   ");

        Assert.True(resultado.DeveExibirAlerta);
        Assert.False(string.IsNullOrWhiteSpace(resultado.Texto));
    }

    [Fact]
    public void PossuiAlergiaSemDescricaoEhRejeitadoNaEntrada()
    {
        var erros = AlertaAlergia.Validar(StatusAlergia.PossuiAlergia, null);

        Assert.NotEmpty(erros);
    }

    [Fact]
    public void DescricaoComEstadoQueNegaAlergiaEhRejeitada()
    {
        // Impede exatamente a combinacao que gerava o falso alerta.
        var erros = AlertaAlergia.Validar(StatusAlergia.SemAlergiaConhecida, "Nega alergia medicamentosa");

        Assert.NotEmpty(erros);
    }

    [Fact]
    public void CombinacoesCoerentesPassam()
    {
        Assert.Empty(AlertaAlergia.Validar(StatusAlergia.SemAlergiaConhecida, null));
        Assert.Empty(AlertaAlergia.Validar(StatusAlergia.NaoPerguntado, null));
        Assert.Empty(AlertaAlergia.Validar(StatusAlergia.PossuiAlergia, "Penicilina"));
    }
}
