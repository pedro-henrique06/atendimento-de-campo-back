using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AtendimentoDeCampo.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class PreparoDeEnfermagemEConferenciasPreOperatorias : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "EncaminhadoAoCentroEm",
                table: "cirurgia",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PertencesDestino",
                table: "cirurgia",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "PertencesDevolvidos",
                table: "cirurgia",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "PertencesProteseDentaria",
                table: "cirurgia",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "PertencesRoupasAcessorios",
                table: "cirurgia",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "PreOpAvaliacaoAnestesica",
                table: "cirurgia",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "PreOpEcg",
                table: "cirurgia",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "PreOpExamesObservacao",
                table: "cirurgia",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "PreOpLaboratorial",
                table: "cirurgia",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "PreOpUltrassom",
                table: "cirurgia",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "PreparoBanhoClorexidine",
                table: "cirurgia",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "PreparoCuidadosEnfermagem",
                table: "cirurgia",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "PreparoPulseiraIdentificacao",
                table: "cirurgia",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "PreparoTricotomia",
                table: "cirurgia",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "PreparoTricotomiaLocal",
                table: "cirurgia",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RecuperacaoDieta",
                table: "cirurgia",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "EncaminhadoAoCentroEm",
                table: "cirurgia");

            migrationBuilder.DropColumn(
                name: "PertencesDestino",
                table: "cirurgia");

            migrationBuilder.DropColumn(
                name: "PertencesDevolvidos",
                table: "cirurgia");

            migrationBuilder.DropColumn(
                name: "PertencesProteseDentaria",
                table: "cirurgia");

            migrationBuilder.DropColumn(
                name: "PertencesRoupasAcessorios",
                table: "cirurgia");

            migrationBuilder.DropColumn(
                name: "PreOpAvaliacaoAnestesica",
                table: "cirurgia");

            migrationBuilder.DropColumn(
                name: "PreOpEcg",
                table: "cirurgia");

            migrationBuilder.DropColumn(
                name: "PreOpExamesObservacao",
                table: "cirurgia");

            migrationBuilder.DropColumn(
                name: "PreOpLaboratorial",
                table: "cirurgia");

            migrationBuilder.DropColumn(
                name: "PreOpUltrassom",
                table: "cirurgia");

            migrationBuilder.DropColumn(
                name: "PreparoBanhoClorexidine",
                table: "cirurgia");

            migrationBuilder.DropColumn(
                name: "PreparoCuidadosEnfermagem",
                table: "cirurgia");

            migrationBuilder.DropColumn(
                name: "PreparoPulseiraIdentificacao",
                table: "cirurgia");

            migrationBuilder.DropColumn(
                name: "PreparoTricotomia",
                table: "cirurgia");

            migrationBuilder.DropColumn(
                name: "PreparoTricotomiaLocal",
                table: "cirurgia");

            migrationBuilder.DropColumn(
                name: "RecuperacaoDieta",
                table: "cirurgia");
        }
    }
}
