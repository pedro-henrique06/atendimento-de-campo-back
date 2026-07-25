using AtendimentoDeCampo.Domain;
using AtendimentoDeCampo.Domain.Servicos;

namespace AtendimentoDeCampo.Tests;

public class GeradorCodigoAtendimentoTests
{
    [Fact]
    public void CodigoGeradoTemOFormatoEsperado()
    {
        var codigo = GeradorCodigoAtendimento.Gerar("ACA");

        Assert.StartsWith("ACA-", codigo);
        Assert.Equal(8, codigo.Length);
        Assert.True(GeradorCodigoAtendimento.EhValido(codigo));
    }

    [Fact]
    public void AlfabetoNaoTemCaracteresAmbiguos()
    {
        // O codigo e lido em voz alta e anotado a mao na fila.
        foreach (var ambiguo in new[] { 'I', 'O', '0', '1', 'S', '5' })
        {
            Assert.DoesNotContain(ambiguo, GeradorCodigoAtendimento.Alfabeto);
        }
    }

    [Fact]
    public void CodigosGeradosNaoSeRepetemEmVolumeRazoavel()
    {
        var codigos = Enumerable.Range(0, 500)
            .Select(_ => GeradorCodigoAtendimento.Gerar("ACA"))
            .ToHashSet();

        // 30^4 = 810.000 combinacoes; 500 sorteios praticamente nao colidem.
        Assert.True(codigos.Count > 490, $"Apenas {codigos.Count} codigos distintos em 500.");
    }

    [Theory]
    [InlineData("Acampamento Panama", "ACA")]
    [InlineData("Escuela Zoe", "ESC")]
    [InlineData("Lo Coco", "LOC")]
    [InlineData("IB 10 de Marzo", "IBD")]
    public void PrefixoEhDerivadoDoNomeDaBase(string nome, string esperado)
    {
        // Digitos e espacos sao descartados, letras nao: "IB 10 de Marzo"
        // vira "IBD". O prefixo precisa continuar reconhecivel pela equipe.
        Assert.Equal(esperado, GeradorCodigoAtendimento.DerivarPrefixo(nome));
    }

    [Fact]
    public void NomeCurtoEhCompletadoAteTresCaracteres()
    {
        Assert.Equal(3, GeradorCodigoAtendimento.DerivarPrefixo("Zoe").Length);
        Assert.Equal(3, GeradorCodigoAtendimento.DerivarPrefixo("A").Length);
    }

    [Fact]
    public void AcentoEhNormalizadoNoPrefixo()
    {
        Assert.Equal("PAN", GeradorCodigoAtendimento.DerivarPrefixo("Panamá"));
    }

    [Theory]
    [InlineData("")]
    [InlineData("AC")]
    [InlineData("ACAM")]
    [InlineData("A0A")]
    [InlineData("A1A")]
    public void PrefixoInvalidoEhRejeitado(string prefixo)
    {
        Assert.Throws<ArgumentException>(() => GeradorCodigoAtendimento.Gerar(prefixo));
    }

    [Theory]
    [InlineData("ACA4K7Z")]
    [InlineData("AC-4K7Z")]
    [InlineData("ACA-4K7")]
    [InlineData("ACA-4K70")]
    [InlineData("")]
    public void CodigosMalformadosSaoInvalidos(string codigo)
    {
        Assert.False(GeradorCodigoAtendimento.EhValido(codigo));
    }
}

public class CalculadoraIdadeTests
{
    private static readonly DateOnly Hoje = new(2026, 7, 23);

    [Fact]
    public void IdadeEhCalculadaPelaDataDeNascimento()
    {
        // Caso do prontuario analisado: 07/12/1965 exibido como 60 anos.
        Assert.Equal(60, CalculadoraIdade.Calcular(new DateOnly(1965, 12, 7), null, Hoje));
    }

    [Fact]
    public void AniversarioAindaNaoOcorridoNaoContaOAnoCorrente()
    {
        // Vespera do aniversario: ainda nao completou.
        Assert.Equal(54, CalculadoraIdade.Calcular(new DateOnly(1971, 7, 24), null, Hoje));
        // Aniversario e hoje: ja completou.
        Assert.Equal(55, CalculadoraIdade.Calcular(new DateOnly(1971, 7, 23), null, Hoje));
    }

    [Fact]
    public void SemDataUsaIdadeAproximada()
    {
        // Em campo a data de nascimento costuma ser desconhecida.
        Assert.Equal(30, CalculadoraIdade.Calcular(null, 30, Hoje));
    }

    [Fact]
    public void SemNenhumaOrigemRetornaNulo()
    {
        Assert.Null(CalculadoraIdade.Calcular(null, null, Hoje));
    }

    [Fact]
    public void DataFuturaNaoProduzIdadeNegativa()
    {
        Assert.Null(CalculadoraIdade.Calcular(new DateOnly(2030, 1, 1), null, Hoje));
    }

    [Theory]
    [InlineData(2, FaixaEtaria.De0a4)]
    [InlineData(4, FaixaEtaria.De0a4)]
    [InlineData(5, FaixaEtaria.De5a14)]
    [InlineData(14, FaixaEtaria.De5a14)]
    [InlineData(15, FaixaEtaria.De15a24)]
    [InlineData(24, FaixaEtaria.De15a24)]
    [InlineData(25, FaixaEtaria.De25a44)]
    [InlineData(44, FaixaEtaria.De25a44)]
    [InlineData(45, FaixaEtaria.De45a64)]
    [InlineData(64, FaixaEtaria.De45a64)]
    [InlineData(65, FaixaEtaria.De65Mais)]
    [InlineData(97, FaixaEtaria.De65Mais)]
    public void FaixasEtariasSeguemOsCortesDoRelatorio(int idade, FaixaEtaria esperada)
    {
        Assert.Equal(esperada, CalculadoraIdade.Faixa(idade));
    }
}

public class TempoDeFilaTests
{
    private static readonly DateTime Base = new(2026, 7, 23, 15, 20, 0, DateTimeKind.Utc);

    [Fact]
    public void EsperaEhADiferencaEntreEntradaESaida()
    {
        // Caso do prontuario: entrou 15:26, saiu 15:49, esperou 23 min.
        var passagem = new PassagemFila
        {
            Especialidade = Especialidade.Odontologia,
            EntrouEm = Base.AddMinutes(6),
            SaiuEm = Base.AddMinutes(29)
        };

        var espera = TempoDeFila.Calcular(passagem);

        Assert.Equal(TimeSpan.FromMinutes(23), espera.Espera);
        Assert.False(espera.EmAberto);
    }

    [Fact]
    public void FilaSemSaidaUsaOAgoraInformado()
    {
        var passagem = new PassagemFila
        {
            Especialidade = Especialidade.Triagem,
            EntrouEm = Base
        };

        var espera = TempoDeFila.Calcular(passagem, Base.AddMinutes(12));

        Assert.Equal(TimeSpan.FromMinutes(12), espera.Espera);
        Assert.True(espera.EmAberto);
    }

    [Fact]
    public void RelogioDessincronizadoNaoGeraEsperaNegativa()
    {
        // Dispositivos em campo nem sempre tem o relogio certo.
        var passagem = new PassagemFila
        {
            Especialidade = Especialidade.Triagem,
            EntrouEm = Base,
            SaiuEm = Base.AddMinutes(-5)
        };

        Assert.Equal(TimeSpan.Zero, TempoDeFila.Calcular(passagem).Espera);
    }

    [Fact]
    public void MedianaIgnoraFilasEmAberto()
    {
        var passagens = new[]
        {
            new PassagemFila { Especialidade = Especialidade.Triagem, EntrouEm = Base, SaiuEm = Base.AddMinutes(4) },
            new PassagemFila { Especialidade = Especialidade.Triagem, EntrouEm = Base, SaiuEm = Base.AddMinutes(6) },
            new PassagemFila { Especialidade = Especialidade.Triagem, EntrouEm = Base, SaiuEm = Base.AddMinutes(8) },
            // Atendimento esquecido em aberto: nao entra no calculo.
            new PassagemFila { Especialidade = Especialidade.Triagem, EntrouEm = Base }
        };

        var medianas = TempoDeFila.MedianaPorEspecialidade(passagens);

        Assert.Equal(TimeSpan.FromMinutes(6), medianas[Especialidade.Triagem]);
    }

    [Fact]
    public void MedianaResisteAOutlier()
    {
        // A media aqui seria 62 min e faria o painel mentir sobre a fila.
        var passagens = new[]
        {
            new PassagemFila { Especialidade = Especialidade.ClinicaGeral, EntrouEm = Base, SaiuEm = Base.AddMinutes(5) },
            new PassagemFila { Especialidade = Especialidade.ClinicaGeral, EntrouEm = Base, SaiuEm = Base.AddMinutes(7) },
            new PassagemFila { Especialidade = Especialidade.ClinicaGeral, EntrouEm = Base, SaiuEm = Base.AddMinutes(9) },
            new PassagemFila { Especialidade = Especialidade.ClinicaGeral, EntrouEm = Base, SaiuEm = Base.AddMinutes(240) }
        };

        var mediana = TempoDeFila.MedianaPorEspecialidade(passagens)[Especialidade.ClinicaGeral];

        Assert.Equal(TimeSpan.FromMinutes(8), mediana);
    }
}
