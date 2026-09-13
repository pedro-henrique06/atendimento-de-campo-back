using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AtendimentoDeCampo.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class FichasDoUltrassomDaFarmaciaEDaGinecologia : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "consultas_ginecologia",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ConsultaId = table.Column<Guid>(type: "uuid", nullable: false),
                    DataUltimaMenstruacao = table.Column<DateOnly>(type: "date", nullable: true),
                    Gestacoes = table.Column<int>(type: "integer", nullable: true),
                    Partos = table.Column<int>(type: "integer", nullable: true),
                    Abortos = table.Column<int>(type: "integer", nullable: true),
                    Gestante = table.Column<bool>(type: "boolean", nullable: true),
                    SemanasGestacao = table.Column<int>(type: "integer", nullable: true),
                    MetodoContraceptivo = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    UltimoPreventivo = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_consultas_ginecologia", x => x.Id);
                    table.ForeignKey(
                        name: "FK_consultas_ginecologia_consultas_ConsultaId",
                        column: x => x.ConsultaId,
                        principalTable: "consultas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "farmacia",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    EtapaId = table.Column<Guid>(type: "uuid", nullable: false),
                    Orientacoes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    Observacoes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    Desfecho = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_farmacia", x => x.Id);
                    table.ForeignKey(
                        name: "FK_farmacia_etapas_EtapaId",
                        column: x => x.EtapaId,
                        principalTable: "etapas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ultrassom",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    EtapaId = table.Column<Guid>(type: "uuid", nullable: false),
                    ExameSolicitado = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    Indicacao = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    Analise = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    Conclusao = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    Desfecho = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ultrassom", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ultrassom_etapas_EtapaId",
                        column: x => x.EtapaId,
                        principalTable: "etapas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_consultas_ginecologia_ConsultaId",
                table: "consultas_ginecologia",
                column: "ConsultaId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_farmacia_EtapaId",
                table: "farmacia",
                column: "EtapaId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ultrassom_EtapaId",
                table: "ultrassom",
                column: "EtapaId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "consultas_ginecologia");

            migrationBuilder.DropTable(
                name: "farmacia");

            migrationBuilder.DropTable(
                name: "ultrassom");
        }
    }
}
