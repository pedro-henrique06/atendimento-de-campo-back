using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AtendimentoDeCampo.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class TabelaDeSinaisVitais : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "sinais_vitais",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AtendimentoId = table.Column<Guid>(type: "uuid", nullable: false),
                    MedidaEm = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    RegistradaPorId = table.Column<Guid>(type: "uuid", nullable: false),
                    PressaoSistolica = table.Column<int>(type: "integer", nullable: true),
                    PressaoDiastolica = table.Column<int>(type: "integer", nullable: true),
                    FrequenciaCardiaca = table.Column<int>(type: "integer", nullable: true),
                    FrequenciaRespiratoria = table.Column<int>(type: "integer", nullable: true),
                    SaturacaoO2 = table.Column<int>(type: "integer", nullable: true),
                    TemperaturaCelsius = table.Column<double>(type: "double precision", nullable: true),
                    GlicemiaCapilar = table.Column<int>(type: "integer", nullable: true),
                    EscalaDor = table.Column<int>(type: "integer", nullable: true),
                    Observacao = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    CriadaEm = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_sinais_vitais", x => x.Id);
                    table.ForeignKey(
                        name: "FK_sinais_vitais_atendimentos_AtendimentoId",
                        column: x => x.AtendimentoId,
                        principalTable: "atendimentos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_sinais_vitais_profissionais_RegistradaPorId",
                        column: x => x.RegistradaPorId,
                        principalTable: "profissionais",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_sinais_vitais_AtendimentoId_MedidaEm",
                table: "sinais_vitais",
                columns: new[] { "AtendimentoId", "MedidaEm" });

            migrationBuilder.CreateIndex(
                name: "IX_sinais_vitais_RegistradaPorId",
                table: "sinais_vitais",
                column: "RegistradaPorId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "sinais_vitais");
        }
    }
}
