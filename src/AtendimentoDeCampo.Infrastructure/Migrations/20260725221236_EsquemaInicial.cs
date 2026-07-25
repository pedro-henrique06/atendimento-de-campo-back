using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AtendimentoDeCampo.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class EsquemaInicial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "bases",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Nome = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    PrefixoCodigo = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    Ativa = table.Column<bool>(type: "boolean", nullable: false),
                    CriadaEm = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_bases", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "cid10",
                columns: table => new
                {
                    Codigo = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    DescricaoPt = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    DescricaoEs = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    DescricaoEn = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    Capitulo = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_cid10", x => x.Codigo);
                });

            migrationBuilder.CreateTable(
                name: "itens_catalogo",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Nome = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    PrincipioAtivo = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Concentracao = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: true),
                    Forma = table.Column<int>(type: "integer", nullable: false),
                    Unidade = table.Column<int>(type: "integer", nullable: false),
                    Categoria = table.Column<int>(type: "integer", nullable: false),
                    ViasPermitidas = table.Column<int[]>(type: "integer[]", nullable: false),
                    Ativo = table.Column<bool>(type: "boolean", nullable: false),
                    CriadoEm = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_itens_catalogo", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "pacientes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Nome = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    TipoDocumento = table.Column<int>(type: "integer", nullable: false),
                    NumeroDocumento = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: true),
                    DataNascimento = table.Column<DateOnly>(type: "date", nullable: true),
                    IdadeAproximada = table.Column<int>(type: "integer", nullable: true),
                    Sexo = table.Column<int>(type: "integer", nullable: false),
                    StatusAlergia = table.Column<int>(type: "integer", nullable: false),
                    Alergias = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    CondicoesCronicas = table.Column<int[]>(type: "integer[]", nullable: false),
                    OutraCondicaoCronica = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Vulnerabilidades = table.Column<int[]>(type: "integer[]", nullable: false),
                    ConsentimentoRegistro = table.Column<bool>(type: "boolean", nullable: false),
                    ConsentimentoEm = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CriadoEm = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    AtualizadoEm = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_pacientes", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "profissionais",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Nome = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    Funcao = table.Column<int>(type: "integer", nullable: false),
                    ConselhoTipo = table.Column<int>(type: "integer", nullable: false),
                    Registro = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    SenhaHash = table.Column<string>(type: "text", nullable: false),
                    Idioma = table.Column<int>(type: "integer", nullable: false),
                    Ativo = table.Column<bool>(type: "boolean", nullable: false),
                    CriadoEm = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_profissionais", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "estoque_base",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    BaseId = table.Column<Guid>(type: "uuid", nullable: false),
                    ItemId = table.Column<Guid>(type: "uuid", nullable: false),
                    Quantidade = table.Column<int>(type: "integer", nullable: false),
                    AtualizadoEm = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_estoque_base", x => x.Id);
                    table.ForeignKey(
                        name: "FK_estoque_base_bases_BaseId",
                        column: x => x.BaseId,
                        principalTable: "bases",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_estoque_base_itens_catalogo_ItemId",
                        column: x => x.ItemId,
                        principalTable: "itens_catalogo",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "atendimentos",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Codigo = table.Column<string>(type: "character varying(12)", maxLength: 12, nullable: false),
                    BaseId = table.Column<Guid>(type: "uuid", nullable: false),
                    PacienteId = table.Column<Guid>(type: "uuid", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    ClassificacaoRisco = table.Column<int>(type: "integer", nullable: true),
                    QueixaPrincipal = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Latitude = table.Column<double>(type: "double precision", nullable: true),
                    Longitude = table.Column<double>(type: "double precision", nullable: true),
                    PrecisaoMetros = table.Column<double>(type: "double precision", nullable: true),
                    CriadoPorId = table.Column<Guid>(type: "uuid", nullable: false),
                    CriadoEm = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    FinalizadoPorId = table.Column<Guid>(type: "uuid", nullable: true),
                    FinalizadoEm = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    AtualizadoEm = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_atendimentos", x => x.Id);
                    table.ForeignKey(
                        name: "FK_atendimentos_bases_BaseId",
                        column: x => x.BaseId,
                        principalTable: "bases",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_atendimentos_pacientes_PacienteId",
                        column: x => x.PacienteId,
                        principalTable: "pacientes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_atendimentos_profissionais_CriadoPorId",
                        column: x => x.CriadoPorId,
                        principalTable: "profissionais",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_atendimentos_profissionais_FinalizadoPorId",
                        column: x => x.FinalizadoPorId,
                        principalTable: "profissionais",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "auditorias",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AtendimentoId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProfissionalId = table.Column<Guid>(type: "uuid", nullable: false),
                    Acao = table.Column<int>(type: "integer", nullable: false),
                    Especialidade = table.Column<int>(type: "integer", nullable: true),
                    Campo = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    ValorAnterior = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    ValorNovo = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    CriadaEm = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_auditorias", x => x.Id);
                    table.ForeignKey(
                        name: "FK_auditorias_atendimentos_AtendimentoId",
                        column: x => x.AtendimentoId,
                        principalTable: "atendimentos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_auditorias_profissionais_ProfissionalId",
                        column: x => x.ProfissionalId,
                        principalTable: "profissionais",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "etapas",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AtendimentoId = table.Column<Guid>(type: "uuid", nullable: false),
                    Especialidade = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    ProfissionalId = table.Column<Guid>(type: "uuid", nullable: true),
                    IniciadaEm = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ConcluidaEm = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CriadaEm = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_etapas", x => x.Id);
                    table.ForeignKey(
                        name: "FK_etapas_atendimentos_AtendimentoId",
                        column: x => x.AtendimentoId,
                        principalTable: "atendimentos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_etapas_profissionais_ProfissionalId",
                        column: x => x.ProfissionalId,
                        principalTable: "profissionais",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "passagens_fila",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AtendimentoId = table.Column<Guid>(type: "uuid", nullable: false),
                    Especialidade = table.Column<int>(type: "integer", nullable: false),
                    EntrouEm = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    SaiuEm = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_passagens_fila", x => x.Id);
                    table.ForeignKey(
                        name: "FK_passagens_fila_atendimentos_AtendimentoId",
                        column: x => x.AtendimentoId,
                        principalTable: "atendimentos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "consultas",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    EtapaId = table.Column<Guid>(type: "uuid", nullable: false),
                    SintomasDescricao = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    Cid10Codigo = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    DiagnosticoObservacao = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    Conduta = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    Desfecho = table.Column<int>(type: "integer", nullable: true),
                    EncaminhadoPara = table.Column<int>(type: "integer", nullable: true),
                    SintomasSaudeMental = table.Column<int[]>(type: "integer[]", nullable: false),
                    PerdasVivenciadas = table.Column<int[]>(type: "integer[]", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_consultas", x => x.Id);
                    table.ForeignKey(
                        name: "FK_consultas_cid10_Cid10Codigo",
                        column: x => x.Cid10Codigo,
                        principalTable: "cid10",
                        principalColumn: "Codigo",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_consultas_etapas_EtapaId",
                        column: x => x.EtapaId,
                        principalTable: "etapas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "dispensacoes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    EtapaId = table.Column<Guid>(type: "uuid", nullable: false),
                    ItemId = table.Column<Guid>(type: "uuid", nullable: true),
                    DescricaoLivre = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    JustificativaItemLivre = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    Quantidade = table.Column<int>(type: "integer", nullable: false),
                    Unidade = table.Column<int>(type: "integer", nullable: false),
                    Via = table.Column<int>(type: "integer", nullable: true),
                    Posologia = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    CriadaEm = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_dispensacoes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_dispensacoes_etapas_EtapaId",
                        column: x => x.EtapaId,
                        principalTable: "etapas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_dispensacoes_itens_catalogo_ItemId",
                        column: x => x.ItemId,
                        principalTable: "itens_catalogo",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "enfermagem",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    EtapaId = table.Column<Guid>(type: "uuid", nullable: false),
                    Procedimentos = table.Column<int[]>(type: "integer[]", nullable: false),
                    OutroProcedimento = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    Observacoes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    Desfecho = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_enfermagem", x => x.Id);
                    table.ForeignKey(
                        name: "FK_enfermagem_etapas_EtapaId",
                        column: x => x.EtapaId,
                        principalTable: "etapas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "odontologia",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    EtapaId = table.Column<Guid>(type: "uuid", nullable: false),
                    Queixa = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    Cid10Codigo = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    Procedimentos = table.Column<int[]>(type: "integer[]", nullable: false),
                    OutroProcedimento = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    Desfecho = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_odontologia", x => x.Id);
                    table.ForeignKey(
                        name: "FK_odontologia_cid10_Cid10Codigo",
                        column: x => x.Cid10Codigo,
                        principalTable: "cid10",
                        principalColumn: "Codigo",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_odontologia_etapas_EtapaId",
                        column: x => x.EtapaId,
                        principalTable: "etapas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "triagens",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    EtapaId = table.Column<Guid>(type: "uuid", nullable: false),
                    PressaoSistolica = table.Column<int>(type: "integer", nullable: true),
                    PressaoDiastolica = table.Column<int>(type: "integer", nullable: true),
                    FrequenciaCardiaca = table.Column<int>(type: "integer", nullable: true),
                    FrequenciaRespiratoria = table.Column<int>(type: "integer", nullable: true),
                    SaturacaoO2 = table.Column<int>(type: "integer", nullable: true),
                    TemperaturaCelsius = table.Column<double>(type: "double precision", nullable: true),
                    GlicemiaCapilar = table.Column<int>(type: "integer", nullable: true),
                    Sintomas = table.Column<int[]>(type: "integer[]", nullable: false),
                    OutroSintoma = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    MedicamentosEmUso = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    StatusAlergia = table.Column<int>(type: "integer", nullable: false),
                    Alergias = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    ClassificacaoRisco = table.Column<int>(type: "integer", nullable: false),
                    Encaminhamento = table.Column<int>(type: "integer", nullable: true),
                    Observacoes = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_triagens", x => x.Id);
                    table.ForeignKey(
                        name: "FK_triagens_etapas_EtapaId",
                        column: x => x.EtapaId,
                        principalTable: "etapas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "consultas_ortopedia",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ConsultaId = table.Column<Guid>(type: "uuid", nullable: false),
                    Localizacao = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    MecanismoTrauma = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    Imobilizacao = table.Column<bool>(type: "boolean", nullable: false),
                    NecessitaRaioX = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_consultas_ortopedia", x => x.Id);
                    table.ForeignKey(
                        name: "FK_consultas_ortopedia_consultas_ConsultaId",
                        column: x => x.ConsultaId,
                        principalTable: "consultas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "marcacoes_dente",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OdontologiaId = table.Column<Guid>(type: "uuid", nullable: false),
                    Dente = table.Column<int>(type: "integer", nullable: false),
                    Estado = table.Column<int>(type: "integer", nullable: false),
                    Faces = table.Column<int[]>(type: "integer[]", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_marcacoes_dente", x => x.Id);
                    table.ForeignKey(
                        name: "FK_marcacoes_dente_odontologia_OdontologiaId",
                        column: x => x.OdontologiaId,
                        principalTable: "odontologia",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_atendimentos_BaseId_CriadoEm",
                table: "atendimentos",
                columns: new[] { "BaseId", "CriadoEm" });

            migrationBuilder.CreateIndex(
                name: "IX_atendimentos_BaseId_Status",
                table: "atendimentos",
                columns: new[] { "BaseId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_atendimentos_ClassificacaoRisco",
                table: "atendimentos",
                column: "ClassificacaoRisco");

            migrationBuilder.CreateIndex(
                name: "IX_atendimentos_Codigo",
                table: "atendimentos",
                column: "Codigo",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_atendimentos_CriadoPorId",
                table: "atendimentos",
                column: "CriadoPorId");

            migrationBuilder.CreateIndex(
                name: "IX_atendimentos_FinalizadoPorId",
                table: "atendimentos",
                column: "FinalizadoPorId");

            migrationBuilder.CreateIndex(
                name: "IX_atendimentos_PacienteId",
                table: "atendimentos",
                column: "PacienteId");

            migrationBuilder.CreateIndex(
                name: "IX_auditorias_AtendimentoId_CriadaEm",
                table: "auditorias",
                columns: new[] { "AtendimentoId", "CriadaEm" });

            migrationBuilder.CreateIndex(
                name: "IX_auditorias_ProfissionalId",
                table: "auditorias",
                column: "ProfissionalId");

            migrationBuilder.CreateIndex(
                name: "IX_bases_PrefixoCodigo",
                table: "bases",
                column: "PrefixoCodigo",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_cid10_DescricaoPt",
                table: "cid10",
                column: "DescricaoPt");

            migrationBuilder.CreateIndex(
                name: "IX_consultas_Cid10Codigo",
                table: "consultas",
                column: "Cid10Codigo");

            migrationBuilder.CreateIndex(
                name: "IX_consultas_EtapaId",
                table: "consultas",
                column: "EtapaId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_consultas_ortopedia_ConsultaId",
                table: "consultas_ortopedia",
                column: "ConsultaId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_dispensacoes_EtapaId",
                table: "dispensacoes",
                column: "EtapaId");

            migrationBuilder.CreateIndex(
                name: "IX_dispensacoes_ItemId",
                table: "dispensacoes",
                column: "ItemId");

            migrationBuilder.CreateIndex(
                name: "IX_enfermagem_EtapaId",
                table: "enfermagem",
                column: "EtapaId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_estoque_base_BaseId_ItemId",
                table: "estoque_base",
                columns: new[] { "BaseId", "ItemId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_estoque_base_ItemId",
                table: "estoque_base",
                column: "ItemId");

            migrationBuilder.CreateIndex(
                name: "IX_etapas_AtendimentoId_Especialidade",
                table: "etapas",
                columns: new[] { "AtendimentoId", "Especialidade" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_etapas_ProfissionalId",
                table: "etapas",
                column: "ProfissionalId");

            migrationBuilder.CreateIndex(
                name: "IX_etapas_Status",
                table: "etapas",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_itens_catalogo_Ativo",
                table: "itens_catalogo",
                column: "Ativo");

            migrationBuilder.CreateIndex(
                name: "IX_itens_catalogo_Nome_Concentracao_Forma",
                table: "itens_catalogo",
                columns: new[] { "Nome", "Concentracao", "Forma" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_marcacoes_dente_Dente",
                table: "marcacoes_dente",
                column: "Dente");

            migrationBuilder.CreateIndex(
                name: "IX_marcacoes_dente_OdontologiaId_Dente_Estado",
                table: "marcacoes_dente",
                columns: new[] { "OdontologiaId", "Dente", "Estado" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_odontologia_Cid10Codigo",
                table: "odontologia",
                column: "Cid10Codigo");

            migrationBuilder.CreateIndex(
                name: "IX_odontologia_EtapaId",
                table: "odontologia",
                column: "EtapaId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_pacientes_Nome",
                table: "pacientes",
                column: "Nome");

            migrationBuilder.CreateIndex(
                name: "IX_pacientes_TipoDocumento_NumeroDocumento",
                table: "pacientes",
                columns: new[] { "TipoDocumento", "NumeroDocumento" },
                unique: true,
                filter: "\"NumeroDocumento\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_passagens_fila_AtendimentoId",
                table: "passagens_fila",
                column: "AtendimentoId");

            migrationBuilder.CreateIndex(
                name: "IX_passagens_fila_Especialidade_EntrouEm",
                table: "passagens_fila",
                columns: new[] { "Especialidade", "EntrouEm" });

            migrationBuilder.CreateIndex(
                name: "IX_profissionais_Ativo",
                table: "profissionais",
                column: "Ativo");

            migrationBuilder.CreateIndex(
                name: "IX_profissionais_Nome_Funcao",
                table: "profissionais",
                columns: new[] { "Nome", "Funcao" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_triagens_EtapaId",
                table: "triagens",
                column: "EtapaId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "auditorias");

            migrationBuilder.DropTable(
                name: "consultas_ortopedia");

            migrationBuilder.DropTable(
                name: "dispensacoes");

            migrationBuilder.DropTable(
                name: "enfermagem");

            migrationBuilder.DropTable(
                name: "estoque_base");

            migrationBuilder.DropTable(
                name: "marcacoes_dente");

            migrationBuilder.DropTable(
                name: "passagens_fila");

            migrationBuilder.DropTable(
                name: "triagens");

            migrationBuilder.DropTable(
                name: "consultas");

            migrationBuilder.DropTable(
                name: "itens_catalogo");

            migrationBuilder.DropTable(
                name: "odontologia");

            migrationBuilder.DropTable(
                name: "cid10");

            migrationBuilder.DropTable(
                name: "etapas");

            migrationBuilder.DropTable(
                name: "atendimentos");

            migrationBuilder.DropTable(
                name: "bases");

            migrationBuilder.DropTable(
                name: "pacientes");

            migrationBuilder.DropTable(
                name: "profissionais");
        }
    }
}
