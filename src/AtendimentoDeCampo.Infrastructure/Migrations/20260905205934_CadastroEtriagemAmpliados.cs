using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AtendimentoDeCampo.Infrastructure.Migrations
{
    /// <summary>
    /// Campos que a equipe sente falta todo dia: cartao do SUS e comunidade no
    /// cadastro, peso, altura e escala de dor na triagem, e nome da mae e
    /// endereco para o menor de idade.
    ///
    /// Tudo entra anulavel, e isso e deliberado: os pacientes ja cadastrados nao
    /// tem esses dados e nao ha de onde tira-los. Exigir preenchimento
    /// retroativo travaria o retorno de quem ja esta no sistema — a regra do
    /// menor vale no cadastro novo, e nao sobre o que ja foi gravado.
    ///
    /// A tabela de comunidades nasce vazia. Ate a coordenacao cadastrar a
    /// primeira, o campo simplesmente nao oferece opcao — o que e melhor que
    /// oferecer texto livre e produzir tres grafias do mesmo lugar.
    /// </summary>
    public partial class CadastroEtriagemAmpliados : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "AlturaCm",
                table: "triagens",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "EscalaDor",
                table: "triagens",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "PesoKg",
                table: "triagens",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CartaoSus",
                table: "pacientes",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ComunidadeId",
                table: "pacientes",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Endereco",
                table: "pacientes",
                type: "character varying(300)",
                maxLength: 300,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "NomeDaMae",
                table: "pacientes",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "comunidades",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Nome = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    Ativa = table.Column<bool>(type: "boolean", nullable: false),
                    CriadaEm = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_comunidades", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_pacientes_ComunidadeId",
                table: "pacientes",
                column: "ComunidadeId");

            migrationBuilder.CreateIndex(
                name: "IX_comunidades_Nome",
                table: "comunidades",
                column: "Nome",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_pacientes_comunidades_ComunidadeId",
                table: "pacientes",
                column: "ComunidadeId",
                principalTable: "comunidades",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_pacientes_comunidades_ComunidadeId",
                table: "pacientes");

            migrationBuilder.DropTable(
                name: "comunidades");

            migrationBuilder.DropIndex(
                name: "IX_pacientes_ComunidadeId",
                table: "pacientes");

            migrationBuilder.DropColumn(
                name: "AlturaCm",
                table: "triagens");

            migrationBuilder.DropColumn(
                name: "EscalaDor",
                table: "triagens");

            migrationBuilder.DropColumn(
                name: "PesoKg",
                table: "triagens");

            migrationBuilder.DropColumn(
                name: "CartaoSus",
                table: "pacientes");

            migrationBuilder.DropColumn(
                name: "ComunidadeId",
                table: "pacientes");

            migrationBuilder.DropColumn(
                name: "Endereco",
                table: "pacientes");

            migrationBuilder.DropColumn(
                name: "NomeDaMae",
                table: "pacientes");
        }
    }
}
