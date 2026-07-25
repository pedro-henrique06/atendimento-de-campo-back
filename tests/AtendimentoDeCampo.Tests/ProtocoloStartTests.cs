using AtendimentoDeCampo.Domain;
using AtendimentoDeCampo.Domain.Servicos;

namespace AtendimentoDeCampo.Tests;

public class ProtocoloStartTests
{
    [Fact]
    public void QuemDeambulaEhVerde()
    {
        var achados = new AchadosStart { Deambula = true, FrequenciaRespiratoria = 34 };

        var resultado = ProtocoloStart.Avaliar(achados);

        // Deambular e o primeiro no do START e tem precedencia sobre os demais
        // achados, inclusive uma frequencia respiratoria alterada.
        Assert.Equal(ClassificacaoRisco.Verde, resultado.Classificacao);
    }

    [Fact]
    public void SemRespiracaoAposViaAereaEhPreto()
    {
        var achados = new AchadosStart
        {
            Deambula = false,
            RespiraEspontaneamente = false,
            RespiraAposAberturaViaAerea = false
        };

        Assert.Equal(ClassificacaoRisco.Preto, ProtocoloStart.Avaliar(achados).Classificacao);
    }

    [Fact]
    public void RespiraSomenteAposViaAereaEhVermelho()
    {
        var achados = new AchadosStart
        {
            Deambula = false,
            RespiraEspontaneamente = false,
            RespiraAposAberturaViaAerea = true
        };

        Assert.Equal(ClassificacaoRisco.Vermelho, ProtocoloStart.Avaliar(achados).Classificacao);
    }

    [Theory]
    [InlineData(31)]
    [InlineData(45)]
    public void TaquipneiaEhVermelho(int frequencia)
    {
        var achados = new AchadosStart { Deambula = false, FrequenciaRespiratoria = frequencia };

        Assert.Equal(ClassificacaoRisco.Vermelho, ProtocoloStart.Avaliar(achados).Classificacao);
    }

    [Fact]
    public void BradipneiaEhVermelho()
    {
        var achados = new AchadosStart { Deambula = false, FrequenciaRespiratoria = 8 };

        Assert.Equal(ClassificacaoRisco.Vermelho, ProtocoloStart.Avaliar(achados).Classificacao);
    }

    [Fact]
    public void FrequenciaNoLimiteSuperiorToleradoNaoEhVermelho()
    {
        // 30 e o limite: acima disso e imediato, exatamente 30 nao e.
        var achados = new AchadosStart { Deambula = false, FrequenciaRespiratoria = 30 };

        Assert.Equal(ClassificacaoRisco.Amarelo, ProtocoloStart.Avaliar(achados).Classificacao);
    }

    [Fact]
    public void PulsoRadialAusenteEhVermelho()
    {
        var achados = new AchadosStart
        {
            Deambula = false,
            FrequenciaRespiratoria = 20,
            PulsoRadialPresente = false
        };

        Assert.Equal(ClassificacaoRisco.Vermelho, ProtocoloStart.Avaliar(achados).Classificacao);
    }

    [Fact]
    public void EnchimentoCapilarLentificadoEhVermelho()
    {
        var achados = new AchadosStart
        {
            Deambula = false,
            FrequenciaRespiratoria = 20,
            TempoEnchimentoCapilarSegundos = 4
        };

        Assert.Equal(ClassificacaoRisco.Vermelho, ProtocoloStart.Avaliar(achados).Classificacao);
    }

    [Fact]
    public void NaoObedeceComandosEhVermelho()
    {
        var achados = new AchadosStart
        {
            Deambula = false,
            FrequenciaRespiratoria = 18,
            ObedeceComandos = false
        };

        Assert.Equal(ClassificacaoRisco.Vermelho, ProtocoloStart.Avaliar(achados).Classificacao);
    }

    [Fact]
    public void EstavelSemDeambularEhAmarelo()
    {
        var achados = new AchadosStart
        {
            Deambula = false,
            FrequenciaRespiratoria = 18,
            PulsoRadialPresente = true,
            TempoEnchimentoCapilarSegundos = 2,
            ObedeceComandos = true
        };

        Assert.Equal(ClassificacaoRisco.Amarelo, ProtocoloStart.Avaliar(achados).Classificacao);
    }

    [Fact]
    public void MotivoDaSugestaoEhPreenchido()
    {
        var resultado = ProtocoloStart.Avaliar(new AchadosStart { Deambula = true });

        Assert.False(string.IsNullOrWhiteSpace(resultado.Motivo));
    }

    [Fact]
    public void DivergenciaEhDetectadaSemBloquearAEscolha()
    {
        var achados = new AchadosStart { Deambula = true };

        // A sugestao e Verde; o profissional classifica como Amarelo. O sistema
        // apenas sinaliza a divergencia: quem decide e quem esta com o paciente.
        Assert.True(ProtocoloStart.DivergeDaSugestao(achados, ClassificacaoRisco.Amarelo));
        Assert.False(ProtocoloStart.DivergeDaSugestao(achados, ClassificacaoRisco.Verde));
    }
}
