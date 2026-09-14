using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AtendimentoDeCampo.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class FichaCirurgica : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "cirurgia",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    EtapaId = table.Column<Guid>(type: "uuid", nullable: false),
                    Indicacao = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    ProcedimentoProposto = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    Lateralidade = table.Column<int>(type: "integer", nullable: false),
                    JejumHoras = table.Column<int>(type: "integer", nullable: true),
                    ConsentimentoAssinado = table.Column<bool>(type: "boolean", nullable: false),
                    ObservacoesPreOperatorio = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    CheckInIdentidadeConfirmada = table.Column<bool>(type: "boolean", nullable: false),
                    CheckInSitioMarcado = table.Column<bool>(type: "boolean", nullable: false),
                    CheckInConsentimentoConferido = table.Column<bool>(type: "boolean", nullable: false),
                    CheckInAlergiaConferida = table.Column<bool>(type: "boolean", nullable: false),
                    CheckInJejumConferido = table.Column<bool>(type: "boolean", nullable: false),
                    CheckInEm = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    TimeOutUmEquipeApresentada = table.Column<bool>(type: "boolean", nullable: false),
                    TimeOutUmMonitorizacaoOk = table.Column<bool>(type: "boolean", nullable: false),
                    TimeOutUmViaAereaAvaliada = table.Column<bool>(type: "boolean", nullable: false),
                    TimeOutUmRiscoSangramentoAvaliado = table.Column<bool>(type: "boolean", nullable: false),
                    TimeOutUmEm = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    TimeOutDoisPacienteSitioProcedimentoConfirmados = table.Column<bool>(type: "boolean", nullable: false),
                    TimeOutDoisAntibioticoProfilatico = table.Column<bool>(type: "boolean", nullable: false),
                    TimeOutDoisImagensDisponiveis = table.Column<bool>(type: "boolean", nullable: false),
                    TimeOutDoisEventosCriticosRevistos = table.Column<bool>(type: "boolean", nullable: false),
                    TimeOutDoisMaterialEsterilizado = table.Column<bool>(type: "boolean", nullable: false),
                    TimeOutDoisEm = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CheckOutProcedimentoRegistrado = table.Column<bool>(type: "boolean", nullable: false),
                    CheckOutContagemConfere = table.Column<bool>(type: "boolean", nullable: false),
                    CheckOutAmostrasIdentificadas = table.Column<bool>(type: "boolean", nullable: false),
                    CheckOutProblemasComEquipamento = table.Column<bool>(type: "boolean", nullable: false),
                    CheckOutCuidadosRecuperacao = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    CheckOutEm = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    RecuperacaoEntradaEm = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    RecuperacaoSaidaEm = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Intercorrencias = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    ObservacoesRecuperacao = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    Desfecho = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_cirurgia", x => x.Id);
                    table.ForeignKey(
                        name: "FK_cirurgia_etapas_EtapaId",
                        column: x => x.EtapaId,
                        principalTable: "etapas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_cirurgia_EtapaId",
                table: "cirurgia",
                column: "EtapaId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "cirurgia");
        }
    }
}
