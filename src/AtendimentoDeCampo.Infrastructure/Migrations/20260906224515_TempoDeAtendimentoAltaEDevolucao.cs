using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AtendimentoDeCampo.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class TempoDeAtendimentoAltaEDevolucao : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "AssumidaEm",
                table: "passagens_fila",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ConcluidaEm",
                table: "passagens_fila",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "EncaminhadaDe",
                table: "passagens_fila",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "EncaminhadaPorId",
                table: "passagens_fila",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ProfissionalId",
                table: "passagens_fila",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_passagens_fila_EncaminhadaPorId",
                table: "passagens_fila",
                column: "EncaminhadaPorId");

            migrationBuilder.CreateIndex(
                name: "IX_passagens_fila_ProfissionalId_ConcluidaEm",
                table: "passagens_fila",
                columns: new[] { "ProfissionalId", "ConcluidaEm" });

            migrationBuilder.AddForeignKey(
                name: "FK_passagens_fila_profissionais_EncaminhadaPorId",
                table: "passagens_fila",
                column: "EncaminhadaPorId",
                principalTable: "profissionais",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_passagens_fila_profissionais_ProfissionalId",
                table: "passagens_fila",
                column: "ProfissionalId",
                principalTable: "profissionais",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            /*
                Traz para a passagem o que ja estava na etapa.

                O relatorio de producao passou a ler daqui. Sem este preenchimento
                a tela de producao ficaria vazia para tudo que foi atendido ate
                hoje — o dado nao teria sumido do banco, mas teria sumido da tela,
                que da no mesmo para quem monta a escala do plantao.

                Cada atendimento tem no maximo uma etapa por especialidade e, ate
                esta versao, no maximo uma passagem por fila: a correspondencia e
                um-para-um e nao ha o que duplicar.
            */
            migrationBuilder.Sql("""
                UPDATE passagens_fila p
                   SET "ProfissionalId" = e."ProfissionalId",
                       "AssumidaEm"     = e."IniciadaEm",
                       "ConcluidaEm"    = e."ConcluidaEm"
                  FROM etapas e
                 WHERE e."AtendimentoId" = p."AtendimentoId"
                   AND e."Especialidade" = p."Especialidade"
                   AND e."Status" = 2
                   AND e."ProfissionalId" IS NOT NULL
                   AND e."IniciadaEm"  IS NOT NULL
                   AND e."ConcluidaEm" IS NOT NULL;
                """);

            /*
                Quem esta com um paciente neste exato momento tambem precisa do
                inicio, senao o cronometro da tela nasceria zerado no primeiro
                atendimento em andamento depois do deploy.
            */
            migrationBuilder.Sql("""
                UPDATE passagens_fila p
                   SET "ProfissionalId" = e."ProfissionalId",
                       "AssumidaEm"     = e."IniciadaEm"
                  FROM etapas e
                 WHERE e."AtendimentoId" = p."AtendimentoId"
                   AND e."Especialidade" = p."Especialidade"
                   AND p."SaiuEm" IS NULL
                   AND e."Status" = 1
                   AND e."ProfissionalId" IS NOT NULL
                   AND e."IniciadaEm" IS NOT NULL;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_passagens_fila_profissionais_EncaminhadaPorId",
                table: "passagens_fila");

            migrationBuilder.DropForeignKey(
                name: "FK_passagens_fila_profissionais_ProfissionalId",
                table: "passagens_fila");

            migrationBuilder.DropIndex(
                name: "IX_passagens_fila_EncaminhadaPorId",
                table: "passagens_fila");

            migrationBuilder.DropIndex(
                name: "IX_passagens_fila_ProfissionalId_ConcluidaEm",
                table: "passagens_fila");

            migrationBuilder.DropColumn(
                name: "AssumidaEm",
                table: "passagens_fila");

            migrationBuilder.DropColumn(
                name: "ConcluidaEm",
                table: "passagens_fila");

            migrationBuilder.DropColumn(
                name: "EncaminhadaDe",
                table: "passagens_fila");

            migrationBuilder.DropColumn(
                name: "EncaminhadaPorId",
                table: "passagens_fila");

            migrationBuilder.DropColumn(
                name: "ProfissionalId",
                table: "passagens_fila");
        }
    }
}
