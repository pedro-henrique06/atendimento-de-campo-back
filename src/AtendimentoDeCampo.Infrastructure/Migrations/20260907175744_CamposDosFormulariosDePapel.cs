using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AtendimentoDeCampo.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class CamposDosFormulariosDePapel : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<double>(
                name: "CircunferenciaCefalicaCm",
                table: "triagens",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CirurgiasPrevias",
                table: "triagens",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "TesteRapidoCovid",
                table: "triagens",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "TesteRapidoMalaria",
                table: "triagens",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "TeveCirurgiaPrevia",
                table: "triagens",
                type: "boolean",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Cpf",
                table: "pacientes",
                type: "character varying(14)",
                maxLength: 14,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Dsei",
                table: "pacientes",
                type: "character varying(160)",
                maxLength: 160,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "EstadoResidencia",
                table: "pacientes",
                type: "character varying(80)",
                maxLength: 80,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Etnia",
                table: "pacientes",
                type: "character varying(120)",
                maxLength: 120,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MunicipioNascimento",
                table: "pacientes",
                type: "character varying(160)",
                maxLength: 160,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PaisNascimento",
                table: "pacientes",
                type: "character varying(80)",
                maxLength: 80,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PoloBase",
                table: "pacientes",
                type: "character varying(160)",
                maxLength: 160,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "RacaCor",
                table: "pacientes",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "ExameFisico",
                table: "consultas",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "HistoriaClinica",
                table: "consultas",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "OrientacoesGerais",
                table: "consultas",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CircunferenciaCefalicaCm",
                table: "triagens");

            migrationBuilder.DropColumn(
                name: "CirurgiasPrevias",
                table: "triagens");

            migrationBuilder.DropColumn(
                name: "TesteRapidoCovid",
                table: "triagens");

            migrationBuilder.DropColumn(
                name: "TesteRapidoMalaria",
                table: "triagens");

            migrationBuilder.DropColumn(
                name: "TeveCirurgiaPrevia",
                table: "triagens");

            migrationBuilder.DropColumn(
                name: "Cpf",
                table: "pacientes");

            migrationBuilder.DropColumn(
                name: "Dsei",
                table: "pacientes");

            migrationBuilder.DropColumn(
                name: "EstadoResidencia",
                table: "pacientes");

            migrationBuilder.DropColumn(
                name: "Etnia",
                table: "pacientes");

            migrationBuilder.DropColumn(
                name: "MunicipioNascimento",
                table: "pacientes");

            migrationBuilder.DropColumn(
                name: "PaisNascimento",
                table: "pacientes");

            migrationBuilder.DropColumn(
                name: "PoloBase",
                table: "pacientes");

            migrationBuilder.DropColumn(
                name: "RacaCor",
                table: "pacientes");

            migrationBuilder.DropColumn(
                name: "ExameFisico",
                table: "consultas");

            migrationBuilder.DropColumn(
                name: "HistoriaClinica",
                table: "consultas");

            migrationBuilder.DropColumn(
                name: "OrientacoesGerais",
                table: "consultas");
        }
    }
}
