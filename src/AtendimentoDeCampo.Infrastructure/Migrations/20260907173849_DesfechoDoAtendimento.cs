using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AtendimentoDeCampo.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class DesfechoDoAtendimento : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Desfecho",
                table: "atendimentos",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DesfechoDetalhe",
                table: "atendimentos",
                type: "character varying(300)",
                maxLength: 300,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_atendimentos_BaseId_Desfecho",
                table: "atendimentos",
                columns: new[] { "BaseId", "Desfecho" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_atendimentos_BaseId_Desfecho",
                table: "atendimentos");

            migrationBuilder.DropColumn(
                name: "Desfecho",
                table: "atendimentos");

            migrationBuilder.DropColumn(
                name: "DesfechoDetalhe",
                table: "atendimentos");
        }
    }
}
