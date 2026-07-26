using AtendimentoDeCampo.Domain;
using Microsoft.EntityFrameworkCore;

namespace AtendimentoDeCampo.Infrastructure;

public class AtendimentoDbContext : DbContext
{
    public AtendimentoDbContext(DbContextOptions<AtendimentoDbContext> options) : base(options)
    {
    }

    public DbSet<Base> Bases => Set<Base>();
    public DbSet<Profissional> Profissionais => Set<Profissional>();
    public DbSet<Paciente> Pacientes => Set<Paciente>();
    public DbSet<Atendimento> Atendimentos => Set<Atendimento>();
    public DbSet<Etapa> Etapas => Set<Etapa>();
    public DbSet<Triagem> Triagens => Set<Triagem>();
    public DbSet<Consulta> Consultas => Set<Consulta>();
    public DbSet<ConsultaOrtopedia> ConsultasOrtopedia => Set<ConsultaOrtopedia>();
    public DbSet<Odontologia> Odontologias => Set<Odontologia>();
    public DbSet<MarcacaoDente> MarcacoesDente => Set<MarcacaoDente>();
    public DbSet<Enfermagem> Enfermagens => Set<Enfermagem>();
    public DbSet<ItemCatalogo> ItensCatalogo => Set<ItemCatalogo>();
    public DbSet<EstoqueBase> EstoqueBases => Set<EstoqueBase>();
    public DbSet<Dispensacao> Dispensacoes => Set<Dispensacao>();
    public DbSet<Cid10> Cid10s => Set<Cid10>();
    public DbSet<PassagemFila> PassagensFila => Set<PassagemFila>();
    public DbSet<Auditoria> Auditorias => Set<Auditoria>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        ConfigurarBase(b);
        ConfigurarProfissional(b);
        ConfigurarPaciente(b);
        ConfigurarAtendimento(b);
        ConfigurarEtapas(b);
        ConfigurarCatalogo(b);
        ConfigurarFilaEAuditoria(b);
    }

    private static void ConfigurarBase(ModelBuilder b)
    {
        b.Entity<Base>(e =>
        {
            e.ToTable("bases");
            e.Property(x => x.Nome).IsRequired().HasMaxLength(160);
            e.Property(x => x.PrefixoCodigo).IsRequired().HasMaxLength(3);
            e.HasIndex(x => x.PrefixoCodigo).IsUnique();
        });
    }

    private static void ConfigurarProfissional(ModelBuilder b)
    {
        b.Entity<Profissional>(e =>
        {
            e.ToTable("profissionais");
            e.Property(x => x.Nome).IsRequired().HasMaxLength(160);
            e.Property(x => x.Registro).HasMaxLength(40);
            e.Property(x => x.SenhaHash).IsRequired();
            e.Property(x => x.Usuario).IsRequired().HasMaxLength(40);
            e.Property(x => x.Email).HasMaxLength(200);
            e.Property(x => x.MotivoRecusa).HasMaxLength(300);

            // O usuario e a identidade de login, e a unica coisa unica aqui.
            // Antes a chave era nome + funcao, o que impedia duas pessoas
            // homonimas na mesma funcao de terem conta.
            e.HasIndex(x => x.Usuario).IsUnique();
            e.HasIndex(x => x.Status);

            // Quem revisou a conta e outro profissional; auto-referencia.
            e.HasOne(x => x.RevisadoPor)
                .WithMany()
                .HasForeignKey(x => x.RevisadoPorId)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }

    private static void ConfigurarPaciente(ModelBuilder b)
    {
        b.Entity<Paciente>(e =>
        {
            e.ToTable("pacientes");
            e.Property(x => x.Codigo).IsRequired().HasMaxLength(9);
            e.Property(x => x.Nome).IsRequired().HasMaxLength(200);
            e.Property(x => x.NumeroDocumento).HasMaxLength(60);
            e.Property(x => x.Alergias).HasMaxLength(500);
            e.Property(x => x.OutraCondicaoCronica).HasMaxLength(200);

            AplicarListaEnum<Paciente, CondicaoCronica>(e, x => x.CondicoesCronicas);
            AplicarListaEnum<Paciente, Vulnerabilidade>(e, x => x.Vulnerabilidades);

            e.HasIndex(x => x.Nome);

            // O codigo e o que o paciente carrega no bolso: precisa achar um so.
            e.HasIndex(x => x.Codigo).IsUnique();

            // Documento identifica o paciente quando existe. Pacientes sem
            // documento sao comuns em campo, e o indice parcial permite varios
            // deles sem colidir.
            e.HasIndex(x => new { x.TipoDocumento, x.NumeroDocumento })
                .IsUnique()
                .HasFilter("\"NumeroDocumento\" IS NOT NULL");
        });
    }

    private static void ConfigurarAtendimento(ModelBuilder b)
    {
        b.Entity<Atendimento>(e =>
        {
            e.ToTable("atendimentos");
            e.Property(x => x.Codigo).IsRequired().HasMaxLength(12);
            e.HasIndex(x => x.Codigo).IsUnique();
            e.Property(x => x.QueixaPrincipal).HasMaxLength(500);

            e.HasOne(x => x.Base)
                .WithMany(x => x.Atendimentos)
                .HasForeignKey(x => x.BaseId)
                .OnDelete(DeleteBehavior.Restrict);

            e.HasOne(x => x.Paciente)
                .WithMany(x => x.Atendimentos)
                .HasForeignKey(x => x.PacienteId)
                .OnDelete(DeleteBehavior.Restrict);

            e.HasOne(x => x.CriadoPor)
                .WithMany()
                .HasForeignKey(x => x.CriadoPorId)
                .OnDelete(DeleteBehavior.Restrict);

            e.HasOne(x => x.FinalizadoPor)
                .WithMany()
                .HasForeignKey(x => x.FinalizadoPorId)
                .OnDelete(DeleteBehavior.Restrict);

            e.HasIndex(x => new { x.BaseId, x.Status });
            e.HasIndex(x => new { x.BaseId, x.CriadoEm });
            e.HasIndex(x => x.ClassificacaoRisco);
        });
    }

    private static void ConfigurarEtapas(ModelBuilder b)
    {
        b.Entity<Etapa>(e =>
        {
            e.ToTable("etapas");

            e.HasOne(x => x.Atendimento)
                .WithMany(x => x.Etapas)
                .HasForeignKey(x => x.AtendimentoId)
                .OnDelete(DeleteBehavior.Cascade);

            e.HasOne(x => x.Profissional)
                .WithMany(x => x.Etapas)
                .HasForeignKey(x => x.ProfissionalId)
                .OnDelete(DeleteBehavior.Restrict);

            // Um atendimento passa uma vez por cada especialidade.
            e.HasIndex(x => new { x.AtendimentoId, x.Especialidade }).IsUnique();
            e.HasIndex(x => x.Status);
        });

        b.Entity<Triagem>(e =>
        {
            e.ToTable("triagens");
            AplicarListaEnum<Triagem, Sintoma>(e, x => x.Sintomas);
            e.Property(x => x.OutroSintoma).HasMaxLength(300);
            e.Property(x => x.MedicamentosEmUso).HasMaxLength(500);
            e.Property(x => x.Alergias).HasMaxLength(500);
            e.Property(x => x.Observacoes).HasMaxLength(1000);

            e.HasOne(x => x.Etapa)
                .WithOne(x => x.Triagem)
                .HasForeignKey<Triagem>(x => x.EtapaId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<Consulta>(e =>
        {
            e.ToTable("consultas");
            e.Property(x => x.SintomasDescricao).HasMaxLength(2000);
            e.Property(x => x.DiagnosticoObservacao).HasMaxLength(1000);
            e.Property(x => x.Conduta).HasMaxLength(2000);
            e.Property(x => x.Cid10Codigo).HasMaxLength(10);
            AplicarListaEnum<Consulta, SintomaSaudeMental>(e, x => x.SintomasSaudeMental);
            AplicarListaEnum<Consulta, PerdaVivenciada>(e, x => x.PerdasVivenciadas);

            e.HasOne(x => x.Etapa)
                .WithOne(x => x.Consulta)
                .HasForeignKey<Consulta>(x => x.EtapaId)
                .OnDelete(DeleteBehavior.Cascade);

            e.HasOne(x => x.Cid10)
                .WithMany()
                .HasForeignKey(x => x.Cid10Codigo)
                .OnDelete(DeleteBehavior.Restrict);

            e.HasIndex(x => x.Cid10Codigo);
        });

        b.Entity<ConsultaOrtopedia>(e =>
        {
            e.ToTable("consultas_ortopedia");
            e.Property(x => x.Localizacao).HasMaxLength(200);
            e.Property(x => x.MecanismoTrauma).HasMaxLength(1000);

            e.HasOne(x => x.Consulta)
                .WithOne(x => x.Ortopedia)
                .HasForeignKey<ConsultaOrtopedia>(x => x.ConsultaId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<Odontologia>(e =>
        {
            e.ToTable("odontologia");
            e.Property(x => x.Queixa).HasMaxLength(2000);
            e.Property(x => x.Cid10Codigo).HasMaxLength(10);
            AplicarListaEnum<Odontologia, ProcedimentoOdontologico>(e, x => x.Procedimentos);
            e.Property(x => x.OutroProcedimento).HasMaxLength(300);

            e.HasOne(x => x.Etapa)
                .WithOne(x => x.Odontologia)
                .HasForeignKey<Odontologia>(x => x.EtapaId)
                .OnDelete(DeleteBehavior.Cascade);

            e.HasOne(x => x.Cid10)
                .WithMany()
                .HasForeignKey(x => x.Cid10Codigo)
                .OnDelete(DeleteBehavior.Restrict);
        });

        b.Entity<MarcacaoDente>(e =>
        {
            e.ToTable("marcacoes_dente");
            AplicarListaEnum<MarcacaoDente, FaceDentaria>(e, x => x.Faces);

            e.HasOne(x => x.Odontologia)
                .WithMany(x => x.Marcacoes)
                .HasForeignKey(x => x.OdontologiaId)
                .OnDelete(DeleteBehavior.Cascade);

            // Um estado por dente. Estados diferentes no mesmo dente sao linhas
            // distintas, e e justamente isso que permite carie + extracao
            // indicada coexistirem.
            e.HasIndex(x => new { x.OdontologiaId, x.Dente, x.Estado }).IsUnique();
            e.HasIndex(x => x.Dente);
        });

        b.Entity<Enfermagem>(e =>
        {
            e.ToTable("enfermagem");
            AplicarListaEnum<Enfermagem, ProcedimentoEnfermagem>(e, x => x.Procedimentos);
            e.Property(x => x.OutroProcedimento).HasMaxLength(300);
            e.Property(x => x.Observacoes).HasMaxLength(2000);

            e.HasOne(x => x.Etapa)
                .WithOne(x => x.Enfermagem)
                .HasForeignKey<Enfermagem>(x => x.EtapaId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }

    private static void ConfigurarCatalogo(ModelBuilder b)
    {
        b.Entity<ItemCatalogo>(e =>
        {
            e.ToTable("itens_catalogo");
            e.Property(x => x.Nome).IsRequired().HasMaxLength(200);
            e.Property(x => x.PrincipioAtivo).HasMaxLength(200);
            e.Property(x => x.Concentracao).HasMaxLength(60);
            AplicarListaEnum<ItemCatalogo, ViaAdministracao>(e, x => x.ViasPermitidas);

            e.HasIndex(x => new { x.Nome, x.Concentracao, x.Forma }).IsUnique();
            e.HasIndex(x => x.Ativo);
        });

        b.Entity<EstoqueBase>(e =>
        {
            e.ToTable("estoque_base");

            e.HasOne(x => x.Base)
                .WithMany(x => x.Estoque)
                .HasForeignKey(x => x.BaseId)
                .OnDelete(DeleteBehavior.Cascade);

            e.HasOne(x => x.Item)
                .WithMany(x => x.Estoques)
                .HasForeignKey(x => x.ItemId)
                .OnDelete(DeleteBehavior.Cascade);

            e.HasIndex(x => new { x.BaseId, x.ItemId }).IsUnique();
        });

        b.Entity<Dispensacao>(e =>
        {
            e.ToTable("dispensacoes");
            e.Property(x => x.DescricaoLivre).HasMaxLength(200);
            e.Property(x => x.JustificativaItemLivre).HasMaxLength(300);
            e.Property(x => x.Posologia).HasMaxLength(300);

            e.HasOne(x => x.Etapa)
                .WithMany(x => x.Dispensacoes)
                .HasForeignKey(x => x.EtapaId)
                .OnDelete(DeleteBehavior.Cascade);

            e.HasOne(x => x.Item)
                .WithMany(x => x.Dispensacoes)
                .HasForeignKey(x => x.ItemId)
                .OnDelete(DeleteBehavior.Restrict);

            e.HasIndex(x => x.ItemId);
        });

        b.Entity<Cid10>(e =>
        {
            e.ToTable("cid10");
            e.HasKey(x => x.Codigo);
            e.Property(x => x.Codigo).HasMaxLength(10);
            e.Property(x => x.DescricaoPt).IsRequired().HasMaxLength(300);
            e.Property(x => x.DescricaoEs).IsRequired().HasMaxLength(300);
            e.Property(x => x.DescricaoEn).IsRequired().HasMaxLength(300);
            e.Property(x => x.Capitulo).HasMaxLength(120);
            e.HasIndex(x => x.DescricaoPt);
        });
    }

    private static void ConfigurarFilaEAuditoria(ModelBuilder b)
    {
        b.Entity<PassagemFila>(e =>
        {
            e.ToTable("passagens_fila");

            e.HasOne(x => x.Atendimento)
                .WithMany(x => x.PassagensFila)
                .HasForeignKey(x => x.AtendimentoId)
                .OnDelete(DeleteBehavior.Cascade);

            e.HasIndex(x => x.AtendimentoId);
            e.HasIndex(x => new { x.Especialidade, x.EntrouEm });
        });

        b.Entity<Auditoria>(e =>
        {
            e.ToTable("auditorias");
            e.Property(x => x.Campo).HasMaxLength(120);
            e.Property(x => x.ValorAnterior).HasMaxLength(2000);
            e.Property(x => x.ValorNovo).HasMaxLength(2000);

            e.HasOne(x => x.Atendimento)
                .WithMany(x => x.Auditorias)
                .HasForeignKey(x => x.AtendimentoId)
                .OnDelete(DeleteBehavior.Cascade);

            e.HasOne(x => x.Profissional)
                .WithMany()
                .HasForeignKey(x => x.ProfissionalId)
                .OnDelete(DeleteBehavior.Restrict);

            e.HasIndex(x => new { x.AtendimentoId, x.CriadaEm });
        });
    }

    /// <summary>
    /// Registra a conversao lista-de-enum para integer[] junto com o comparador
    /// de valor, que o EF exige para detectar alteracoes dentro da colecao.
    /// </summary>
    private static void AplicarListaEnum<TEntidade, TEnum>(
        Microsoft.EntityFrameworkCore.Metadata.Builders.EntityTypeBuilder<TEntidade> builder,
        System.Linq.Expressions.Expression<Func<TEntidade, List<TEnum>>> propriedade)
        where TEntidade : class
        where TEnum : struct, Enum
    {
        builder.Property(propriedade)
            .HasColumnType("integer[]")
            .HasConversion(new ListaEnumParaIntArrayConverter<TEnum>(), new ListaEnumComparer<TEnum>());
    }
}
