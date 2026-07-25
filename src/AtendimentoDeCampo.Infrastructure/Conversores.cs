using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace AtendimentoDeCampo.Infrastructure;

/// <summary>
/// Converte uma lista de enum para integer[] no Postgres.
///
/// Guardar o valor numerico, e nao o nome, mantem a coluna estavel quando um
/// membro do enum e renomeado. Em troca, a ordem dos membros passa a ser parte
/// do contrato: novos valores entram no fim, nunca no meio.
/// </summary>
public sealed class ListaEnumParaIntArrayConverter<TEnum> : ValueConverter<List<TEnum>, int[]>
    where TEnum : struct, Enum
{
    public ListaEnumParaIntArrayConverter()
        : base(
            lista => lista.Select(e => Convert.ToInt32(e)).ToArray(),
            array => array.Select(i => (TEnum)Enum.ToObject(typeof(TEnum), i)).ToList())
    {
    }
}

/// <summary>
/// Comparador necessario para o EF detectar alteracoes dentro da lista. Sem ele
/// o change tracker compara referencias e edicoes na colecao passam em branco.
/// </summary>
public sealed class ListaEnumComparer<TEnum> : ValueComparer<List<TEnum>>
    where TEnum : struct, Enum
{
    public ListaEnumComparer()
        : base(
            (a, b) => a != null && b != null && a.SequenceEqual(b),
            lista => lista.Aggregate(0, (acc, item) => HashCode.Combine(acc, item.GetHashCode())),
            lista => lista.ToList())
    {
    }
}
