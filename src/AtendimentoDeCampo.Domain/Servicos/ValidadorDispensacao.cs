namespace AtendimentoDeCampo.Domain.Servicos;

/// <summary>Dados de uma dispensacao antes de virar registro.</summary>
public sealed record EntradaDispensacao
{
    public Guid? ItemId { get; init; }
    public string? DescricaoLivre { get; init; }
    public string? JustificativaItemLivre { get; init; }
    public int Quantidade { get; init; }
    public ViaAdministracao? Via { get; init; }
}

/// <summary>
/// Regras de consistencia da dispensacao de medicamentos e insumos.
///
/// CORRIGE tres defeitos observados no sistema de referencia:
///
///  1. Item como texto livre. O mesmo farmaco aparecia como "Acetaminofen",
///     "Acetaminofeno", "Acetominofen" e "1. Paracetamol 1 gramo" em quatro
///     linhas distintas do relatorio de consumo, e "Ceterizina"/"Cetericina"/
///     "Cetirizina 10 mg" em outras tres. Contagem de consumo inutilizada.
///
///  2. Nota clinica no campo de item. Uma conduta inteira ("Confirmo tamanho de
///     nodulo, oriento paciente quanto seguimento e investigacao com puncao.")
///     foi registrada como item dispensado e passou a contar como insumo.
///
///  3. Via incompativel com a apresentacao. Registros como "Prednisolona 20 mg
///     - Xarope - comprimido" e "Diclofenaco - Oral - 1 ampola" existiam porque
///     via e forma eram escolhidas soltas, sem vinculo entre si.
/// </summary>
public static class ValidadorDispensacao
{
    /// <summary>
    /// Acima deste tamanho, um texto livre deixa de parecer nome de item e passa
    /// a parecer anotacao clinica digitada no campo errado.
    /// </summary>
    public const int TamanhoMaximoDescricaoLivre = 80;

    public static IReadOnlyList<string> Validar(EntradaDispensacao entrada, ItemCatalogo? item)
    {
        var erros = new List<string>();
        var temDescricaoLivre = !string.IsNullOrWhiteSpace(entrada.DescricaoLivre);

        if (entrada.ItemId is null && !temDescricaoLivre)
        {
            erros.Add("Selecione um item do catalogo ou descreva o item dispensado.");
        }

        if (entrada.ItemId is not null && temDescricaoLivre)
        {
            erros.Add("Informe o item do catalogo ou a descricao livre, nunca os dois.");
        }

        if (entrada.Quantidade <= 0)
        {
            erros.Add("Quantidade deve ser maior que zero.");
        }

        if (temDescricaoLivre)
        {
            erros.AddRange(ValidarDescricaoLivre(entrada));
        }

        if (entrada.ItemId is not null)
        {
            erros.AddRange(ValidarItemCatalogo(entrada, item));
        }

        return erros;
    }

    private static IEnumerable<string> ValidarDescricaoLivre(EntradaDispensacao entrada)
    {
        var descricao = entrada.DescricaoLivre!.Trim();

        if (string.IsNullOrWhiteSpace(entrada.JustificativaItemLivre))
        {
            yield return "Item fora do catalogo exige justificativa para revisao da coordenacao.";
        }

        if (descricao.Length > TamanhoMaximoDescricaoLivre)
        {
            yield return
                $"Descricao do item tem {descricao.Length} caracteres. " +
                "Textos longos costumam ser anotacao clinica digitada no campo errado: " +
                "registre a conduta no campo de conduta da consulta.";
        }

        if (ParecerAnotacaoClinica(descricao))
        {
            yield return
                "O texto informado parece uma anotacao clinica, nao o nome de um item. " +
                "Registre a conduta no campo de conduta da consulta.";
        }
    }

    private static IEnumerable<string> ValidarItemCatalogo(EntradaDispensacao entrada, ItemCatalogo? item)
    {
        if (item is null)
        {
            yield return "Item do catalogo nao encontrado.";
            yield break;
        }

        if (!item.Ativo)
        {
            yield return $"Item '{item.Nome}' esta inativo no catalogo.";
        }

        if (entrada.Via is null)
        {
            yield return "Informe a via de administracao.";
            yield break;
        }

        if (item.ViasPermitidas.Count > 0 && !item.ViasPermitidas.Contains(entrada.Via.Value))
        {
            var permitidas = string.Join(", ", item.ViasPermitidas);
            yield return
                $"Via {entrada.Via} e incompativel com a apresentacao de '{item.Nome}' " +
                $"({item.Forma}). Vias validas: {permitidas}.";
        }
    }

    /// <summary>
    /// Heuristica simples para barrar conduta clinica no campo de item. Nao
    /// pretende ser exaustiva: e uma rede de seguranca, e a validacao de
    /// tamanho pega o resto.
    /// </summary>
    private static bool ParecerAnotacaoClinica(string texto)
    {
        var normalizado = texto.ToLowerInvariant();

        // Verbo em primeira pessoa e sinal forte de conduta redigida pelo profissional.
        string[] verbosDeConduta =
        {
            "oriento", "prescrevo", "confirmo", "solicito", "encaminho",
            "realizo", "indico", "avalio", "mantenho", "suspendo", "refere"
        };

        if (verbosDeConduta.Any(v => normalizado.Contains(v)))
        {
            return true;
        }

        // Frase com varias palavras e pontuacao final tambem indica texto corrido.
        var palavras = normalizado.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return palavras.Length > 8 && (normalizado.Contains('.') || normalizado.Contains(','));
    }
}
