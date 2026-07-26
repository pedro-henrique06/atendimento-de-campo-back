using AtendimentoDeCampo.Domain.Servicos;

namespace AtendimentoDeCampo.Tests;

public class CodigoPacienteTests
{
    [Fact]
    public void Gera_no_formato_de_dois_blocos()
    {
        Assert.Matches(@"^[A-Z2-9]{4}-[A-Z2-9]{4}$", GeradorCodigoPaciente.Gerar());
    }

    [Fact]
    public void Nunca_usa_caractere_que_se_confunde_a_mao()
    {
        // O paciente leva o codigo anotado num papel que pode passar semanas no
        // bolso. I/O/S e 0/1/5 trocados na leitura fariam perder o cadastro.
        for (var i = 0; i < 500; i++)
        {
            Assert.DoesNotContain(GeradorCodigoPaciente.Gerar(), c => "IOS015".Contains(c));
        }
    }

    [Fact]
    public void Nao_se_confunde_com_o_codigo_do_atendimento()
    {
        // Os dois circulam na mesma fila. Formatos distintos deixam a tela dizer
        // qual e qual em vez de responder "nao encontrado".
        var doPaciente = GeradorCodigoPaciente.Gerar();

        Assert.False(GeradorCodigoAtendimento.EhValido(doPaciente));
        Assert.Null(GeradorCodigoPaciente.Normalizar(GeradorCodigoAtendimento.Gerar("ACA")));
    }

    [Theory]
    [InlineData("4K7Z-2YAP", "4K7Z-2YAP")]
    [InlineData("4k7z-2yap", "4K7Z-2YAP")]
    [InlineData("4K7Z2YAP", "4K7Z-2YAP")]
    [InlineData(" 4K7Z 2YAP ", "4K7Z-2YAP")]
    public void Aceita_o_codigo_como_a_pessoa_digita(string digitado, string esperado)
    {
        // No celular o hifen escapa e o teclado sobe a caixa sozinho. Recusar
        // por isso seria implicancia com quem esta atendendo.
        Assert.Equal(esperado, GeradorCodigoPaciente.Normalizar(digitado));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("4K7Z")]
    [InlineData("4K7Z-2YAPX")]
    [InlineData("4K7Z-2YA0")]
    public void Recusa_o_que_nao_e_codigo(string? invalido)
    {
        Assert.Null(GeradorCodigoPaciente.Normalizar(invalido));
        Assert.False(GeradorCodigoPaciente.EhValido(invalido));
    }

    [Fact]
    public void Sorteia_valores_diferentes()
    {
        var sorteados = Enumerable.Range(0, 200).Select(_ => GeradorCodigoPaciente.Gerar()).ToHashSet();

        Assert.Equal(200, sorteados.Count);
    }
}
