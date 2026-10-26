using AtendimentoDeCampo.Domain;
using AtendimentoDeCampo.Domain.Servicos;

namespace AtendimentoDeCampo.Tests;

public class ValidadorDispensacaoTests
{
    /// <summary>
    /// Material de entrega: gaze nao se administra, entao nao tem via nenhuma.
    /// </summary>
    private static ItemCatalogo Gaze() => new()
    {
        Nome = "Gaze esteril",
        Forma = FormaFarmaceutica.Insumo,
        Unidade = UnidadeDispensacao.Unidade,
        Categoria = CategoriaItem.Insumo,
        ViasPermitidas = new List<ViaAdministracao>()
    };

    private static ItemCatalogo Comprimido() => new()
    {
        Nome = "Ibuprofeno",
        Concentracao = "400 mg",
        Forma = FormaFarmaceutica.Comprimido,
        Unidade = UnidadeDispensacao.Comprimido,
        ViasPermitidas = new List<ViaAdministracao> { ViaAdministracao.Oral }
    };

    private static ItemCatalogo Ampola() => new()
    {
        Nome = "Diclofenaco",
        Concentracao = "75 mg/3 mL",
        Forma = FormaFarmaceutica.Ampola,
        Unidade = UnidadeDispensacao.Ampola,
        ViasPermitidas = new List<ViaAdministracao>
        {
            ViaAdministracao.Intramuscular,
            ViaAdministracao.Intravenosa
        }
    };

    [Fact]
    public void DispensacaoValidaPassa()
    {
        var entrada = new EntradaDispensacao
        {
            ItemId = Guid.NewGuid(),
            Quantidade = 10,
            Via = ViaAdministracao.Oral
        };

        Assert.Empty(ValidadorDispensacao.Validar(entrada, Comprimido()));
    }

    [Fact]
    public void ComprimidoPorViaIncompativelEhRejeitado()
    {
        // Reproduz "Prednisolona 20 mg - Xarope - comprimido" do relatorio real.
        var entrada = new EntradaDispensacao
        {
            ItemId = Guid.NewGuid(),
            Quantidade = 1,
            Via = ViaAdministracao.Intramuscular
        };

        var erros = ValidadorDispensacao.Validar(entrada, Comprimido());

        Assert.Contains(erros, e => e.Contains("incompativel"));
    }

    [Fact]
    public void AmpolaPorViaOralEhRejeitada()
    {
        // Reproduz "Diclofenaco - Oral - 1 ampola" do relatorio real.
        var entrada = new EntradaDispensacao
        {
            ItemId = Guid.NewGuid(),
            Quantidade = 1,
            Via = ViaAdministracao.Oral
        };

        Assert.Contains(ValidadorDispensacao.Validar(entrada, Ampola()), e => e.Contains("incompativel"));
    }

    [Fact]
    public void ItemDoCatalogoSemViaEhRejeitado()
    {
        var entrada = new EntradaDispensacao { ItemId = Guid.NewGuid(), Quantidade = 1 };

        Assert.Contains(ValidadorDispensacao.Validar(entrada, Comprimido()), e => e.Contains("via"));
    }

    [Fact]
    public void SemItemNemDescricaoEhRejeitado()
    {
        var erros = ValidadorDispensacao.Validar(new EntradaDispensacao { Quantidade = 1 }, null);

        Assert.NotEmpty(erros);
    }

    [Fact]
    public void ItemLivreSemJustificativaEhRejeitado()
    {
        var entrada = new EntradaDispensacao
        {
            DescricaoLivre = "Otocirix",
            Quantidade = 1
        };

        Assert.Contains(ValidadorDispensacao.Validar(entrada, null), e => e.Contains("justificativa"));
    }

    [Fact]
    public void ItemLivreComJustificativaPassa()
    {
        var entrada = new EntradaDispensacao
        {
            DescricaoLivre = "Otocirix",
            JustificativaItemLivre = "Doacao recebida em campo, ainda nao cadastrada.",
            Quantidade = 1
        };

        Assert.Empty(ValidadorDispensacao.Validar(entrada, null));
    }

    [Fact]
    public void NotaClinicaNoCampoDeItemEhRejeitada()
    {
        // Caso real: esta frase inteira estava registrada como item dispensado
        // no relatorio de consumo do sistema de referencia.
        var entrada = new EntradaDispensacao
        {
            DescricaoLivre =
                "Confirmo tamanho de nodulo, oriento paciente quanto seguimento e investigacao com puncao.",
            JustificativaItemLivre = "qualquer",
            Quantidade = 1
        };

        var erros = ValidadorDispensacao.Validar(entrada, null);

        Assert.NotEmpty(erros);
        Assert.Contains(erros, e => e.Contains("conduta"));
    }

    [Fact]
    public void VerboDeCondutaCurtoTambemEhBarrado()
    {
        var entrada = new EntradaDispensacao
        {
            DescricaoLivre = "Oriento repouso",
            JustificativaItemLivre = "qualquer",
            Quantidade = 1
        };

        Assert.Contains(ValidadorDispensacao.Validar(entrada, null), e => e.Contains("conduta"));
    }

    [Fact]
    public void ItemEDescricaoLivreJuntosEhRejeitado()
    {
        var entrada = new EntradaDispensacao
        {
            ItemId = Guid.NewGuid(),
            DescricaoLivre = "Ibuprofeno",
            Quantidade = 1,
            Via = ViaAdministracao.Oral
        };

        Assert.Contains(ValidadorDispensacao.Validar(entrada, Comprimido()), e => e.Contains("nunca os dois"));
    }

    [Fact]
    public void QuantidadeZeroEhRejeitada()
    {
        var entrada = new EntradaDispensacao
        {
            ItemId = Guid.NewGuid(),
            Quantidade = 0,
            Via = ViaAdministracao.Oral
        };

        Assert.Contains(ValidadorDispensacao.Validar(entrada, Comprimido()), e => e.Contains("Quantidade"));
    }

    [Fact]
    public void ItemInativoEhRejeitado()
    {
        var item = Comprimido();
        item.Ativo = false;

        var entrada = new EntradaDispensacao
        {
            ItemId = Guid.NewGuid(),
            Quantidade = 1,
            Via = ViaAdministracao.Oral
        };

        Assert.Contains(ValidadorDispensacao.Validar(entrada, item), e => e.Contains("inativo"));
    }

    /*
        CORRIGE: a via era exigida de todo item do catalogo, e a lista de vias
        permitidas so era conferida quando existia. Juntas, as duas regras
        davam o pior dos dois lados — gaze esteril nao podia ser dispensada de
        forma nenhuma, e quem contornasse escolhendo qualquer via passava,
        porque a checagem de compatibilidade nao rodava com a lista vazia.
        "Gaze esteril - Oral" era aceito, que e exatamente o tipo de registro
        que este validador existe para impedir.
    */

    [Fact]
    public void MaterialSemViaEhDispensadoSemVia()
    {
        var entrada = new EntradaDispensacao { ItemId = Guid.NewGuid(), Quantidade = 4 };

        Assert.Empty(ValidadorDispensacao.Validar(entrada, Gaze()));
    }

    [Fact]
    public void MaterialSemViaNaoAceitaViaNenhuma()
    {
        var entrada = new EntradaDispensacao
        {
            ItemId = Guid.NewGuid(),
            Quantidade = 4,
            Via = ViaAdministracao.Oral
        };

        Assert.Contains(
            ValidadorDispensacao.Validar(entrada, Gaze()),
            e => e.Contains("nao tem via de administracao"));
    }

    [Fact]
    public void MedicamentoContinuaExigindoVia()
    {
        var entrada = new EntradaDispensacao { ItemId = Guid.NewGuid(), Quantidade = 1 };

        Assert.Contains(
            ValidadorDispensacao.Validar(entrada, Comprimido()),
            e => e.Contains("Informe a via"));
    }
}
