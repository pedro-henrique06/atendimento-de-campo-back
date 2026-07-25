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
            "Acampamento Panama",
            "Escuela Zoe",
            "IB 10 de Marzo",
            "Lo Coco",
            "PIB em Caraballeda",
            "Praia Verde",
            "Refugio do Teatro Municipal"
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
            ("A09", "Diarreia e gastroenterite de origem infecciosa presumivel", "Diarrea y gastroenteritis de presunto origen infeccioso", "Diarrhoea and gastroenteritis of presumed infectious origin", "Doencas infecciosas"),
            ("B34.9", "Infeccao viral nao especificada", "Infeccion viral no especificada", "Viral infection, unspecified", "Doencas infecciosas"),
            ("E11", "Diabetes mellitus tipo 2", "Diabetes mellitus tipo 2", "Type 2 diabetes mellitus", "Endocrinas"),
            ("D50", "Anemia por deficiencia de ferro", "Anemia por deficiencia de hierro", "Iron deficiency anaemia", "Sangue"),
            ("F41.0", "Transtorno de panico", "Trastorno de panico", "Panic disorder", "Saude mental"),
            ("F41.1", "Ansiedade generalizada", "Ansiedad generalizada", "Generalized anxiety disorder", "Saude mental"),
            ("F43.2", "Transtorno de adaptacao", "Trastorno de adaptacion", "Adjustment disorder", "Saude mental"),
            ("H66.9", "Otite media nao especificada", "Otitis media no especificada", "Otitis media, unspecified", "Ouvido"),
            ("I10", "Hipertensao essencial", "Hipertension esencial", "Essential hypertension", "Circulatorio"),
            ("I83.9", "Varizes de membros inferiores sem ulcera ou inflamacao", "Varices de miembros inferiores sin ulcera ni inflamacion", "Varicose veins of lower extremities without ulcer or inflammation", "Circulatorio"),
            ("J00", "Nasofaringite aguda (resfriado comum)", "Rinofaringitis aguda (resfriado comun)", "Acute nasopharyngitis (common cold)", "Respiratorio"),
            ("J11", "Influenza devida a virus nao identificado", "Gripe debida a virus no identificado", "Influenza, virus not identified", "Respiratorio"),
            ("J18.9", "Pneumonia nao especificada", "Neumonia no especificada", "Pneumonia, unspecified", "Respiratorio"),
            ("J45.9", "Asma nao especificada", "Asma no especificada", "Asthma, unspecified", "Respiratorio"),
            ("K02.9", "Carie dentaria nao especificada", "Caries dental no especificada", "Dental caries, unspecified", "Digestivo"),
            ("K04.7", "Abscesso periapical sem fistula", "Absceso periapical sin fistula", "Periapical abscess without sinus", "Digestivo"),
            ("K05.6", "Doenca periodontal nao especificada", "Enfermedad periodontal no especificada", "Periodontal disease, unspecified", "Digestivo"),
            ("L23.9", "Dermatite alergica de contato de causa nao especificada", "Dermatitis alergica de contacto de causa no especificada", "Allergic contact dermatitis, unspecified cause", "Pele"),
            ("M25.5", "Dor articular", "Dolor articular", "Pain in joint", "Osteomuscular"),
            ("M54.5", "Dor lombar baixa", "Lumbago", "Low back pain", "Osteomuscular"),
            ("M79.1", "Mialgia", "Mialgia", "Myalgia", "Osteomuscular"),
            ("N39.0", "Infeccao do trato urinario de localizacao nao especificada", "Infeccion de vias urinarias, sitio no especificado", "Urinary tract infection, site not specified", "Geniturinario"),
            ("R51", "Cefaleia", "Cefalea", "Headache", "Sintomas"),
            ("S83.2", "Ruptura do menisco atual", "Desgarro de menisco actual", "Tear of meniscus, current", "Lesoes"),
            ("Z00.0", "Exame medico geral", "Examen medico general", "General medical examination", "Fatores de saude")
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
            Medicamento("Dipirona", "Metamizol sodico", "500 mg", FormaFarmaceutica.Comprimido, UnidadeDispensacao.Comprimido, oral),
            Medicamento("Dipirona", "Metamizol sodico", "500 mg/mL", FormaFarmaceutica.Ampola, UnidadeDispensacao.Ampola, im),
            Medicamento("Ibuprofeno", "Ibuprofeno", "400 mg", FormaFarmaceutica.Comprimido, UnidadeDispensacao.Comprimido, oral),
            Medicamento("Ibuprofeno", "Ibuprofeno", "600 mg", FormaFarmaceutica.Comprimido, UnidadeDispensacao.Comprimido, oral),
            Medicamento("Diclofenaco", "Diclofenaco potassico", "50 mg", FormaFarmaceutica.Comprimido, UnidadeDispensacao.Comprimido, oral),
            Medicamento("Diclofenaco", "Diclofenaco sodico", "75 mg/3 mL", FormaFarmaceutica.Ampola, UnidadeDispensacao.Ampola, im),

            // Antibioticos.
            Medicamento("Amoxicilina", "Amoxicilina", "500 mg", FormaFarmaceutica.Capsula, UnidadeDispensacao.Capsula, oral),
            Medicamento("Amoxicilina + clavulanato", "Amoxicilina + acido clavulanico", "250/62,5 mg/5 mL", FormaFarmaceutica.Suspensao, UnidadeDispensacao.Frasco, oral),
            Medicamento("Cefalexina", "Cefalexina", "500 mg", FormaFarmaceutica.Capsula, UnidadeDispensacao.Capsula, oral),
            Medicamento("Azitromicina", "Azitromicina", "500 mg", FormaFarmaceutica.Comprimido, UnidadeDispensacao.Comprimido, oral),
            Medicamento("Ciprofloxacino", "Ciprofloxacino", "500 mg", FormaFarmaceutica.Comprimido, UnidadeDispensacao.Comprimido, oral),

            // Cardiovascular.
            Medicamento("Losartana", "Losartana potassica", "50 mg", FormaFarmaceutica.Comprimido, UnidadeDispensacao.Comprimido, oral),
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
            Medicamento("Sais de reidratacao oral", "Sais de reidratacao oral", null, FormaFarmaceutica.Sache, UnidadeDispensacao.Sache, oral),
            Medicamento("Albendazol", "Albendazol", "400 mg", FormaFarmaceutica.Comprimido, UnidadeDispensacao.Comprimido, oral),

            // Suplementos e topicos.
            Medicamento("Sulfato ferroso", "Sulfato ferroso", "40 mg", FormaFarmaceutica.Comprimido, UnidadeDispensacao.Comprimido, oral),
            Medicamento("Multivitaminico", "Polivitaminico", null, FormaFarmaceutica.Comprimido, UnidadeDispensacao.Comprimido, oral),
            Medicamento("Nistatina", "Nistatina", "100.000 UI/g", FormaFarmaceutica.Creme, UnidadeDispensacao.Tubo, new List<ViaAdministracao> { ViaAdministracao.Topica }),
            Medicamento("Lagrimas artificiais", "Carmelose sodica", "5 mg/mL", FormaFarmaceutica.Colirio, UnidadeDispensacao.Frasco, new List<ViaAdministracao> { ViaAdministracao.Oftalmica }),

            // Insumos.
            new ItemCatalogo
            {
                Nome = "Gaze esteril",
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
