namespace AtendimentoDeCampo.Domain.Servicos;

/// <summary>Estados de um mesmo dente, agrupados para exibicao.</summary>
public sealed record DenteResumo(int Dente, IReadOnlyList<MarcacaoDente> Marcacoes);

/// <summary>
/// Notacao FDI e montagem do resumo do odontograma.
///
/// CORRIGE: no odontograma de referencia um dente carregava um unico estado
/// visivel. O dente 38 do prontuario analisado tinha carie (faces M e O) e
/// extracao indicada ao mesmo tempo, mas era pintado de uma cor so: o amarelo
/// de "extracao indicada" cobria o rosa de "carie", e quem olhasse o desenho
/// perdia a informacao de carie. Ela sobrevivia apenas no resumo em texto.
///
/// Aqui um dente pode acumular varias marcacoes, e o resumo lista todas.
/// </summary>
public static class Odontograma
{
    /// <summary>Dentes permanentes: quadrantes 1 a 4, posicoes 1 a 8.</summary>
    public static readonly IReadOnlyList<int> DentesPermanentes =
        Enumerable.Range(1, 4)
            .SelectMany(q => Enumerable.Range(1, 8).Select(p => q * 10 + p))
            .ToList();

    /// <summary>Dentes deciduos: quadrantes 5 a 8, posicoes 1 a 5.</summary>
    public static readonly IReadOnlyList<int> DentesDeciduos =
        Enumerable.Range(5, 4)
            .SelectMany(q => Enumerable.Range(1, 5).Select(p => q * 10 + p))
            .ToList();

    public static bool DenteValido(int dente)
        => DentesPermanentes.Contains(dente) || DentesDeciduos.Contains(dente);

    /// <summary>
    /// Faces que fazem sentido para o dente. Anteriores (posicoes 1 a 3) tem
    /// incisal; posteriores tem oclusal.
    /// </summary>
    public static IReadOnlyList<FaceDentaria> FacesValidas(int dente)
    {
        if (!DenteValido(dente))
        {
            return Array.Empty<FaceDentaria>();
        }

        var posicao = dente % 10;
        var comuns = new List<FaceDentaria>
        {
            FaceDentaria.Mesial,
            FaceDentaria.Distal,
            FaceDentaria.Vestibular,
            FaceDentaria.Lingual,
            FaceDentaria.Cervical
        };

        comuns.Add(posicao <= 3 ? FaceDentaria.Incisal : FaceDentaria.Oclusal);
        return comuns;
    }

    public static IReadOnlyList<string> ValidarMarcacao(int dente, EstadoDente estado, IEnumerable<FaceDentaria> faces)
    {
        var erros = new List<string>();

        if (!DenteValido(dente))
        {
            erros.Add($"Dente {dente} nao existe na notacao FDI.");
            return erros;
        }

        var facesValidas = FacesValidas(dente);
        foreach (var face in faces.Distinct())
        {
            if (!facesValidas.Contains(face))
            {
                erros.Add($"Face {face} nao se aplica ao dente {dente}.");
            }
        }

        // Dente ausente nao tem face para marcar.
        if (estado == EstadoDente.Ausente && faces.Any())
        {
            erros.Add($"Dente {dente} marcado como ausente nao deve ter faces.");
        }

        return erros;
    }

    /// <summary>
    /// Estados que nao podem coexistir no mesmo dente por serem contraditorios.
    /// Repare que Carie + ExtracaoIndicada NAO esta aqui: essa combinacao e
    /// clinicamente comum e era exatamente a que o sistema antigo perdia.
    /// </summary>
    public static IReadOnlyList<string> ValidarConjunto(int dente, IEnumerable<EstadoDente> estados)
    {
        var lista = estados.Distinct().ToList();
        var erros = new List<string>();

        if (lista.Contains(EstadoDente.Ausente) && lista.Count > 1)
        {
            erros.Add($"Dente {dente} ausente nao pode ter outros estados.");
        }

        if (lista.Contains(EstadoDente.Higido) && lista.Count > 1)
        {
            erros.Add($"Dente {dente} higido nao pode ter outros estados.");
        }

        return erros;
    }

    /// <summary>
    /// Resumo textual do odontograma, no formato "Carie: 38(M,O); Extracao
    /// indicada: 38" usado no prontuario e no historico de alteracoes.
    /// </summary>
    public static string Resumir(IEnumerable<MarcacaoDente> marcacoes)
    {
        var porEstado = marcacoes
            .Where(m => m.Estado != EstadoDente.Higido)
            .GroupBy(m => m.Estado)
            .OrderBy(g => g.Key)
            .Select(g =>
            {
                var dentes = g
                    .OrderBy(m => m.Dente)
                    .Select(FormatarDente);
                return $"{RotularEstado(g.Key)}: {string.Join(", ", dentes)}";
            });

        return string.Join("; ", porEstado);
    }

    /// <summary>Agrupa marcacoes por dente, preservando todos os estados de cada um.</summary>
    public static IReadOnlyList<DenteResumo> AgruparPorDente(IEnumerable<MarcacaoDente> marcacoes)
        => marcacoes
            .GroupBy(m => m.Dente)
            .OrderBy(g => g.Key)
            .Select(g => new DenteResumo(g.Key, g.ToList()))
            .ToList();

    private static string FormatarDente(MarcacaoDente marcacao)
    {
        if (marcacao.Faces.Count == 0)
        {
            return marcacao.Dente.ToString();
        }

        var faces = marcacao.Faces
            .OrderBy(f => f)
            .Select(AbreviarFace);

        return $"{marcacao.Dente}({string.Join(",", faces)})";
    }

    private static string AbreviarFace(FaceDentaria face) => face switch
    {
        FaceDentaria.Mesial => "M",
        FaceDentaria.Distal => "D",
        FaceDentaria.Oclusal => "O",
        FaceDentaria.Vestibular => "V",
        FaceDentaria.Lingual => "L",
        FaceDentaria.Incisal => "I",
        FaceDentaria.Cervical => "C",
        _ => face.ToString()
    };

    private static string RotularEstado(EstadoDente estado) => estado switch
    {
        EstadoDente.Carie => "Carie",
        EstadoDente.Restaurado => "Restaurado",
        EstadoDente.Ausente => "Ausente",
        EstadoDente.ExtracaoIndicada => "Extracao indicada",
        EstadoDente.Fratura => "Fratura",
        EstadoDente.Selante => "Selante",
        EstadoDente.Protese => "Protese",
        EstadoDente.Implante => "Implante",
        EstadoDente.RestoRadicular => "Resto radicular",
        _ => estado.ToString()
    };
}
