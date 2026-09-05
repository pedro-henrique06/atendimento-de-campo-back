using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AtendimentoDeCampo.Infrastructure.Migrations
{
    /// <summary>
    /// A conta passa a ser criada pela coordenacao, com senha sorteada.
    ///
    /// <c>PrecisaTrocarSenha</c> entra como <c>false</c> nas contas que ja
    /// existem, e isso e de proposito: elas se registraram sozinhas e a senha
    /// delas nunca passou pela mao de outra pessoa. Marcar todo mundo obrigaria
    /// a equipe inteira a trocar a senha no meio do plantao para resolver um
    /// problema que so as contas novas tem.
    ///
    /// As especialidades medicas novas (clinico geral, pediatra, ortopedista)
    /// nao aparecem aqui: a profissao e gravada como inteiro e os valores novos
    /// entraram no fim do enum, entao nao ha dado a converter. Quem esta
    /// cadastrado como "medico" continua medico e ve as tres filas medicas ate a
    /// coordenacao reclassificar pela tela.
    /// </summary>
    public partial class CadastroDeProfissionalPelaCoordenacao : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "CriadaPorId",
                table: "profissionais",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "PrecisaTrocarSenha",
                table: "profissionais",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateIndex(
                name: "IX_profissionais_CriadaPorId",
                table: "profissionais",
                column: "CriadaPorId");

            migrationBuilder.AddForeignKey(
                name: "FK_profissionais_profissionais_CriadaPorId",
                table: "profissionais",
                column: "CriadaPorId",
                principalTable: "profissionais",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_profissionais_profissionais_CriadaPorId",
                table: "profissionais");

            migrationBuilder.DropIndex(
                name: "IX_profissionais_CriadaPorId",
                table: "profissionais");

            migrationBuilder.DropColumn(
                name: "CriadaPorId",
                table: "profissionais");

            migrationBuilder.DropColumn(
                name: "PrecisaTrocarSenha",
                table: "profissionais");
        }
    }
}
