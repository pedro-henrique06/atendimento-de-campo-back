using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AtendimentoDeCampo.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class TipoDaMissao : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "TipoMissao",
                table: "bases",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "TipoMissao",
                table: "atendimentos",
                type: "integer",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "TipoMissao",
                table: "bases");

            migrationBuilder.DropColumn(
                name: "TipoMissao",
                table: "atendimentos");
        }
    }
}
