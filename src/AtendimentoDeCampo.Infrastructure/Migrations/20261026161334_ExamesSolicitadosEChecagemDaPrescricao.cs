using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AtendimentoDeCampo.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class ExamesSolicitadosEChecagemDaPrescricao : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "ChecadaEm",
                table: "dispensacoes",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ChecadaPorId",
                table: "dispensacoes",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ExameLaboratorialDescricao",
                table: "consultas",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "ExameLaboratorialSolicitado",
                table: "consultas",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "OutroExameSolicitado",
                table: "consultas",
                type: "character varying(300)",
                maxLength: 300,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PapanicolauObservacao",
                table: "consultas",
                type: "character varying(300)",
                maxLength: 300,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "PapanicolauSolicitado",
                table: "consultas",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int[]>(
                name: "Procedimentos",
                table: "consultas",
                type: "integer[]",
                nullable: false,
                defaultValue: new int[0]);

            migrationBuilder.AddColumn<string>(
                name: "ProcedimentosObservacao",
                table: "consultas",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_dispensacoes_ChecadaPorId",
                table: "dispensacoes",
                column: "ChecadaPorId");

            migrationBuilder.AddForeignKey(
                name: "FK_dispensacoes_profissionais_ChecadaPorId",
                table: "dispensacoes",
                column: "ChecadaPorId",
                principalTable: "profissionais",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_dispensacoes_profissionais_ChecadaPorId",
                table: "dispensacoes");

            migrationBuilder.DropIndex(
                name: "IX_dispensacoes_ChecadaPorId",
                table: "dispensacoes");

            migrationBuilder.DropColumn(
                name: "ChecadaEm",
                table: "dispensacoes");

            migrationBuilder.DropColumn(
                name: "ChecadaPorId",
                table: "dispensacoes");

            migrationBuilder.DropColumn(
                name: "ExameLaboratorialDescricao",
                table: "consultas");

            migrationBuilder.DropColumn(
                name: "ExameLaboratorialSolicitado",
                table: "consultas");

            migrationBuilder.DropColumn(
                name: "OutroExameSolicitado",
                table: "consultas");

            migrationBuilder.DropColumn(
                name: "PapanicolauObservacao",
                table: "consultas");

            migrationBuilder.DropColumn(
                name: "PapanicolauSolicitado",
                table: "consultas");

            migrationBuilder.DropColumn(
                name: "Procedimentos",
                table: "consultas");

            migrationBuilder.DropColumn(
                name: "ProcedimentosObservacao",
                table: "consultas");
        }
    }
}
