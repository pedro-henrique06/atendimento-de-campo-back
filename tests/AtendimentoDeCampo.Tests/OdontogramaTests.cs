using AtendimentoDeCampo.Domain;
using AtendimentoDeCampo.Domain.Servicos;

namespace AtendimentoDeCampo.Tests;

public class OdontogramaTests
{
    private static MarcacaoDente Marcacao(int dente, EstadoDente estado, params FaceDentaria[] faces)
        => new() { Dente = dente, Estado = estado, Faces = faces.ToList() };

    [Fact]
    public void DenteComCarieEExtracaoIndicadaMantemOsDoisEstados()
    {
        // Este e o caso do dente 38 no prontuario analisado. No sistema antigo o
        // desenho mostrava so a extracao indicada e a carie desaparecia.
        var marcacoes = new[]
        {
            Marcacao(38, EstadoDente.Carie, FaceDentaria.Mesial, FaceDentaria.Oclusal),
            Marcacao(38, EstadoDente.ExtracaoIndicada)
        };

        var agrupado = Odontograma.AgruparPorDente(marcacoes);

        var dente38 = Assert.Single(agrupado);
        Assert.Equal(38, dente38.Dente);
        Assert.Equal(2, dente38.Marcacoes.Count);
        Assert.Contains(dente38.Marcacoes, m => m.Estado == EstadoDente.Carie);
        Assert.Contains(dente38.Marcacoes, m => m.Estado == EstadoDente.ExtracaoIndicada);
    }

    [Fact]
    public void ResumoSegueOFormatoDoProntuario()
    {
        var marcacoes = new[]
        {
            Marcacao(38, EstadoDente.Carie, FaceDentaria.Mesial, FaceDentaria.Oclusal),
            Marcacao(38, EstadoDente.ExtracaoIndicada)
        };

        Assert.Equal("Carie: 38(M,O); Extracao indicada: 38", Odontograma.Resumir(marcacoes));
    }

    [Fact]
    public void CarieEExtracaoIndicadaNaoSaoConsideradasContraditorias()
    {
        var erros = Odontograma.ValidarConjunto(
            38,
            new[] { EstadoDente.Carie, EstadoDente.ExtracaoIndicada });

        Assert.Empty(erros);
    }

    [Fact]
    public void DenteAusenteNaoAceitaOutrosEstados()
    {
        var erros = Odontograma.ValidarConjunto(
            36,
            new[] { EstadoDente.Ausente, EstadoDente.Carie });

        Assert.NotEmpty(erros);
    }

    [Fact]
    public void DenteHigidoNaoAceitaOutrosEstados()
    {
        Assert.NotEmpty(Odontograma.ValidarConjunto(
            11,
            new[] { EstadoDente.Higido, EstadoDente.Restaurado }));
    }

    [Theory]
    [InlineData(11)]
    [InlineData(48)]
    [InlineData(55)]
    [InlineData(85)]
    public void DentesValidosSaoAceitos(int dente)
    {
        Assert.True(Odontograma.DenteValido(dente));
    }

    [Theory]
    [InlineData(10)]
    [InlineData(19)]
    [InlineData(49)]
    [InlineData(56)]
    [InlineData(99)]
    public void DentesForaDaNotacaoFdiSaoRejeitados(int dente)
    {
        Assert.False(Odontograma.DenteValido(dente));
        Assert.NotEmpty(Odontograma.ValidarMarcacao(dente, EstadoDente.Carie, Array.Empty<FaceDentaria>()));
    }

    [Fact]
    public void MolarTemFaceOclusalENaoIncisal()
    {
        var faces = Odontograma.FacesValidas(36);

        Assert.Contains(FaceDentaria.Oclusal, faces);
        Assert.DoesNotContain(FaceDentaria.Incisal, faces);
    }

    [Fact]
    public void IncisivoTemFaceIncisalENaoOclusal()
    {
        var faces = Odontograma.FacesValidas(11);

        Assert.Contains(FaceDentaria.Incisal, faces);
        Assert.DoesNotContain(FaceDentaria.Oclusal, faces);
    }

    [Fact]
    public void FaceIncompativelComODenteEhRejeitada()
    {
        var erros = Odontograma.ValidarMarcacao(
            36,
            EstadoDente.Carie,
            new[] { FaceDentaria.Incisal });

        Assert.NotEmpty(erros);
    }

    [Fact]
    public void DenteAusenteComFacesEhRejeitado()
    {
        var erros = Odontograma.ValidarMarcacao(
            36,
            EstadoDente.Ausente,
            new[] { FaceDentaria.Oclusal });

        Assert.NotEmpty(erros);
    }

    [Fact]
    public void DentesHigidosNaoPoluemOResumo()
    {
        var marcacoes = new[]
        {
            Marcacao(11, EstadoDente.Higido),
            Marcacao(21, EstadoDente.Carie, FaceDentaria.Incisal)
        };

        Assert.Equal("Carie: 21(I)", Odontograma.Resumir(marcacoes));
    }

    [Fact]
    public void NotacaoFdiTemTrintaEDoisPermanentesEVinteDeciduos()
    {
        Assert.Equal(32, Odontograma.DentesPermanentes.Count);
        Assert.Equal(20, Odontograma.DentesDeciduos.Count);
    }
}
