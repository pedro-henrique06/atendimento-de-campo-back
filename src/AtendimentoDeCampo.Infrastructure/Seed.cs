using AtendimentoDeCampo.Domain;
using AtendimentoDeCampo.Domain.Servicos;
using Microsoft.EntityFrameworkCore;

namespace AtendimentoDeCampo.Infrastructure;

/// <summary>
/// Dados iniciais: bases, CID-10 e catalogo de itens.
///
/// O catalogo e a peca que impede o problema central do sistema de referencia,
/// onde o mesmo farmaco virava varios registros distintos por ser digitado a
/// mao. Os itens abaixo cobrem o que aparecia no relatorio de consumo real,
/// cada um com apresentacao, unidade e vias compativeis.
/// </summary>
public static class Seed
{
    public static async Task ExecutarAsync(AtendimentoDbContext db, CancellationToken ct = default)
    {
        await SemearBasesAsync(db, ct);
        await SemearCid10Async(db, ct);
        await SemearCatalogoAsync(db, ct);
        await db.SaveChangesAsync(ct);
    }

    private static async Task SemearBasesAsync(AtendimentoDbContext db, CancellationToken ct)
    {
        if (await db.Bases.AnyAsync(ct))
        {
            return;
        }

        string[] nomes =
        {
            "Acampamento Panamá",
            "Escuela Zoe",
            "IB 10 de Marzo",
            "Lo Coco",
            "PIB em Caraballeda",
            "Praia Verde",
            "Refúgio do Teatro Municipal"
        };

        var usados = new HashSet<string>();

        foreach (var nome in nomes)
        {
            var prefixo = GeradorCodigoAtendimento.DerivarPrefixo(nome);

            // Bases com nomes parecidos podem colidir no prefixo derivado.
            var sufixo = 1;
            while (!usados.Add(prefixo))
            {
                prefixo = GeradorCodigoAtendimento.DerivarPrefixo(nome)[..2] + (char)('A' + sufixo++);
            }

            db.Bases.Add(new Base { Nome = nome, PrefixoCodigo = prefixo });
        }
    }

    private static async Task SemearCid10Async(AtendimentoDbContext db, CancellationToken ct)
    {
        if (await db.Cid10s.AnyAsync(ct))
        {
            return;
        }

        // Subconjunto do CID-10 com os diagnosticos mais frequentes em campo,
        // incluindo todos os que apareciam no relatorio analisado.
        (string Codigo, string Pt, string Es, string En, string Cap)[] codigos =
        {
            ("A09", "Diarreia e gastroenterite de origem infecciosa presumível", "Diarrea y gastroenteritis de presunto origen infeccioso", "Diarrhoea and gastroenteritis of presumed infectious origin", "Doenças infecciosas"),
            ("B34.9", "Infecção viral não especificada", "Infección viral no especificada", "Viral infection, unspecified", "Doenças infecciosas"),
            ("E11", "Diabetes mellitus tipo 2", "Diabetes mellitus tipo 2", "Type 2 diabetes mellitus", "Endócrinas"),
            ("D50", "Anemia por deficiência de ferro", "Anemia por deficiencia de hierro", "Iron deficiency anaemia", "Sangue"),
            ("F41.0", "Transtorno de pânico", "Trastorno de pánico", "Panic disorder", "Saúde mental"),
            ("F41.1", "Ansiedade generalizada", "Ansiedad generalizada", "Generalized anxiety disorder", "Saúde mental"),
            ("F43.2", "Transtorno de adaptação", "Trastorno de adaptación", "Adjustment disorder", "Saúde mental"),
            ("H66.9", "Otite média não especificada", "Otitis media no especificada", "Otitis media, unspecified", "Ouvido"),
            ("I10", "Hipertensão essencial", "Hipertensión esencial", "Essential hypertension", "Circulatório"),
            ("I83.9", "Varizes de membros inferiores sem úlcera ou inflamação", "Varices de miembros inferiores sin úlcera ni inflamación", "Varicose veins of lower extremities without ulcer or inflammation", "Circulatório"),
            ("J00", "Nasofaringite aguda (resfriado comum)", "Rinofaringitis aguda (resfriado común)", "Acute nasopharyngitis (common cold)", "Respiratório"),
            ("J11", "Influenza devida a vírus não identificado", "Gripe debida a virus no identificado", "Influenza, virus not identified", "Respiratório"),
            ("J18.9", "Pneumonia não especificada", "Neumonía no especificada", "Pneumonia, unspecified", "Respiratório"),
            ("J45.9", "Asma não especificada", "Asma no especificada", "Asthma, unspecified", "Respiratório"),
            ("K02.9", "Cárie dentária não especificada", "Caries dental no especificada", "Dental caries, unspecified", "Digestivo"),
            ("K04.7", "Abscesso periapical sem fístula", "Absceso periapical sin fístula", "Periapical abscess without sinus", "Digestivo"),
            ("K05.6", "Doença periodontal não especificada", "Enfermedad periodontal no especificada", "Periodontal disease, unspecified", "Digestivo"),
            ("L23.9", "Dermatite alérgica de contato de causa não especificada", "Dermatitis alérgica de contacto de causa no especificada", "Allergic contact dermatitis, unspecified cause", "Pele"),
            ("M25.5", "Dor articular", "Dolor articular", "Pain in joint", "Osteomuscular"),
            ("M54.5", "Dor lombar baixa", "Lumbago", "Low back pain", "Osteomuscular"),
            ("M79.1", "Mialgia", "Mialgia", "Myalgia", "Osteomuscular"),
            ("N39.0", "Infecção do trato urinário de localização não especificada", "Infección de vías urinarias, sitio no especificado", "Urinary tract infection, site not specified", "Geniturinário"),
            ("R51", "Cefaleia", "Cefalea", "Headache", "Sintomas"),
            ("S83.2", "Ruptura do menisco atual", "Desgarro de menisco actual", "Tear of meniscus, current", "Lesões"),
            ("Z00.0", "Exame médico geral", "Examen médico general", "General medical examination", "Fatores de saúde")
        };

        foreach (var (codigo, pt, es, en, cap) in codigos)
        {
            db.Cid10s.Add(new Cid10
            {
                Codigo = codigo,
                DescricaoPt = pt,
                DescricaoEs = es,
                DescricaoEn = en,
                Capitulo = cap
            });
        }
    }

    private static async Task SemearCatalogoAsync(AtendimentoDbContext db, CancellationToken ct)
    {
        if (await db.ItensCatalogo.AnyAsync(ct))
        {
            return;
        }

        var oral = new List<ViaAdministracao> { ViaAdministracao.Oral };
        var im = new List<ViaAdministracao> { ViaAdministracao.Intramuscular, ViaAdministracao.Intravenosa };

        ItemCatalogo Medicamento(
            string nome,
            string principio,
            string? concentracao,
            FormaFarmaceutica forma,
            UnidadeDispensacao unidade,
            List<ViaAdministracao> vias)
            => new()
            {
                Nome = nome,
                PrincipioAtivo = principio,
                Concentracao = concentracao,
                Forma = forma,
                Unidade = unidade,
                Categoria = CategoriaItem.Medicamento,
                ViasPermitidas = vias
            };

        db.ItensCatalogo.AddRange(
            // Analgesicos e anti-inflamatorios. No relatorio original o mesmo
            // principio ativo aparecia como Acetaminofen / Acetaminofeno /
            // Acetominofen / "1. Paracetamol 1 gramo": aqui e uma linha so.
            Medicamento("Paracetamol", "Paracetamol", "500 mg", FormaFarmaceutica.Comprimido, UnidadeDispensacao.Comprimido, oral),
            Medicamento("Paracetamol", "Paracetamol", "1 g", FormaFarmaceutica.Comprimido, UnidadeDispensacao.Comprimido, oral),
            Medicamento("Dipirona", "Metamizol sódico", "500 mg", FormaFarmaceutica.Comprimido, UnidadeDispensacao.Comprimido, oral),
            Medicamento("Dipirona", "Metamizol sódico", "500 mg/mL", FormaFarmaceutica.Ampola, UnidadeDispensacao.Ampola, im),
            Medicamento("Ibuprofeno", "Ibuprofeno", "400 mg", FormaFarmaceutica.Comprimido, UnidadeDispensacao.Comprimido, oral),
            Medicamento("Ibuprofeno", "Ibuprofeno", "600 mg", FormaFarmaceutica.Comprimido, UnidadeDispensacao.Comprimido, oral),
            Medicamento("Diclofenaco", "Diclofenaco potássico", "50 mg", FormaFarmaceutica.Comprimido, UnidadeDispensacao.Comprimido, oral),
            Medicamento("Diclofenaco", "Diclofenaco sódico", "75 mg/3 mL", FormaFarmaceutica.Ampola, UnidadeDispensacao.Ampola, im),

            // Antibioticos.
            Medicamento("Amoxicilina", "Amoxicilina", "500 mg", FormaFarmaceutica.Capsula, UnidadeDispensacao.Capsula, oral),
            Medicamento("Amoxicilina + clavulanato", "Amoxicilina + ácido clavulânico", "250/62,5 mg/5 mL", FormaFarmaceutica.Suspensao, UnidadeDispensacao.Frasco, oral),
            Medicamento("Cefalexina", "Cefalexina", "500 mg", FormaFarmaceutica.Capsula, UnidadeDispensacao.Capsula, oral),
            Medicamento("Azitromicina", "Azitromicina", "500 mg", FormaFarmaceutica.Comprimido, UnidadeDispensacao.Comprimido, oral),
            Medicamento("Ciprofloxacino", "Ciprofloxacino", "500 mg", FormaFarmaceutica.Comprimido, UnidadeDispensacao.Comprimido, oral),

            // Cardiovascular.
            Medicamento("Losartana", "Losartana potássica", "50 mg", FormaFarmaceutica.Comprimido, UnidadeDispensacao.Comprimido, oral),
            Medicamento("Captopril", "Captopril", "25 mg", FormaFarmaceutica.Comprimido, UnidadeDispensacao.Comprimido, oral),
            Medicamento("Anlodipino", "Anlodipino", "10 mg", FormaFarmaceutica.Comprimido, UnidadeDispensacao.Comprimido, oral),

            // Respiratorio e alergia.
            Medicamento("Loratadina", "Loratadina", "10 mg", FormaFarmaceutica.Comprimido, UnidadeDispensacao.Comprimido, oral),
            Medicamento("Cetirizina", "Cetirizina", "10 mg", FormaFarmaceutica.Comprimido, UnidadeDispensacao.Comprimido, oral),
            Medicamento("Prednisolona", "Prednisolona", "20 mg", FormaFarmaceutica.Comprimido, UnidadeDispensacao.Comprimido, oral),
            Medicamento("Salbutamol", "Salbutamol", "100 mcg/dose", FormaFarmaceutica.Inalador, UnidadeDispensacao.Dose, new List<ViaAdministracao> { ViaAdministracao.Inalatoria }),

            // Digestivo.
            Medicamento("Omeprazol", "Omeprazol", "20 mg", FormaFarmaceutica.Capsula, UnidadeDispensacao.Capsula, oral),
            Medicamento("Ondansetrona", "Ondansetrona", "4 mg/2 mL", FormaFarmaceutica.Ampola, UnidadeDispensacao.Ampola, im),
            Medicamento("Hioscina", "Butilbrometo de escopolamina", "10 mg", FormaFarmaceutica.Comprimido, UnidadeDispensacao.Comprimido, oral),
            Medicamento("Sais de reidratação oral", "Sais de reidratação oral", null, FormaFarmaceutica.Sache, UnidadeDispensacao.Sache, oral),
            Medicamento("Albendazol", "Albendazol", "400 mg", FormaFarmaceutica.Comprimido, UnidadeDispensacao.Comprimido, oral),

            // Suplementos e topicos.
            Medicamento("Sulfato ferroso", "Sulfato ferroso", "40 mg", FormaFarmaceutica.Comprimido, UnidadeDispensacao.Comprimido, oral),
            Medicamento("Multivitamínico", "Polivitamínico", null, FormaFarmaceutica.Comprimido, UnidadeDispensacao.Comprimido, oral),
            Medicamento("Nistatina", "Nistatina", "100.000 UI/g", FormaFarmaceutica.Creme, UnidadeDispensacao.Tubo, new List<ViaAdministracao> { ViaAdministracao.Topica }),
            Medicamento("Lágrimas artificiais", "Carmelose sódica", "5 mg/mL", FormaFarmaceutica.Colirio, UnidadeDispensacao.Frasco, new List<ViaAdministracao> { ViaAdministracao.Oftalmica }),

            // Insumos.
            new ItemCatalogo
            {
                Nome = "Gaze estéril",
                Forma = FormaFarmaceutica.Insumo,
                Unidade = UnidadeDispensacao.Unidade,
                Categoria = CategoriaItem.Insumo,
                ViasPermitidas = new List<ViaAdministracao>()
            },
            new ItemCatalogo
            {
                Nome = "Atadura de crepe",
                Forma = FormaFarmaceutica.Insumo,
                Unidade = UnidadeDispensacao.Unidade,
                Categoria = CategoriaItem.Insumo,
                ViasPermitidas = new List<ViaAdministracao>()
            });
    }
}
