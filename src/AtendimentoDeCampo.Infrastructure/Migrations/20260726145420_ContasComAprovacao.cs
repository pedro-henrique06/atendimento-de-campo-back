using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AtendimentoDeCampo.Infrastructure.Migrations
{
    /// <summary>
    /// Introduz login por usuario e contas com aprovacao.
    ///
    /// O Up gerado automaticamente foi reescrito a mao por dois motivos:
    ///
    ///  1. O EF interpretou a troca de `Ativo` por `EhAdministrador` como uma
    ///     renomeacao de coluna. Os dois sao bool, mas significam coisas opostas:
    ///     todo profissional ativo viraria administrador, e num sistema publico
    ///     isso e uma escalacao de privilegio silenciosa sobre dados reais.
    ///
    ///  2. `Usuario` entrava como NOT NULL com default '' e logo em seguida
    ///     ganhava indice unico. Com duas ou mais linhas na tabela a migration
    ///     aborta; com uma, ela passa e deixa um usuario vazio com o qual
    ///     ninguem consegue entrar.
    ///
    /// Aqui a coluna entra opcional, e preenchida a partir do nome, e so entao
    /// vira obrigatoria e unica.
    /// </summary>
    public partial class ContasComAprovacao : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_profissionais_Ativo",
                table: "profissionais");

            migrationBuilder.DropIndex(
                name: "IX_profissionais_Nome_Funcao",
                table: "profissionais");

            migrationBuilder.AddColumn<string>(
                name: "Email",
                table: "profissionais",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MotivoRecusa",
                table: "profissionais",
                type: "character varying(300)",
                maxLength: 300,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "RevisadoEm",
                table: "profissionais",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "RevisadoPorId",
                table: "profissionais",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Status",
                table: "profissionais",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            // Quem ja estava ativo continua ativo; o resto fica desativado. Sem
            // este passo todo mundo cairia em Pendente e a equipe inteira
            // perderia o acesso na primeira subida da versao nova.
            migrationBuilder.Sql(
                "UPDATE profissionais SET \"Status\" = CASE WHEN \"Ativo\" THEN 1 ELSE 3 END;");

            migrationBuilder.DropColumn(
                name: "Ativo",
                table: "profissionais");

            // Ninguem herda permissao de administrador: ela e concedida
            // explicitamente, pela configuracao do administrador inicial ou por
            // outro administrador na tela de gestao.
            migrationBuilder.AddColumn<bool>(
                name: "EhAdministrador",
                table: "profissionais",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            // Coluna opcional primeiro, para poder preencher antes de exigir.
            migrationBuilder.AddColumn<string>(
                name: "Usuario",
                table: "profissionais",
                type: "character varying(40)",
                maxLength: 40,
                nullable: true);

            // Deriva o usuario do nome: "Claudia Candido da Luz" vira
            // "claudia.candido.da.luz". Homonimos ganham sufixo numerico, que e
            // justamente o caso que o indice unico anterior proibia.
            migrationBuilder.Sql(@"
                WITH normalizado AS (
                    SELECT
                        ""Id"",
                        ""CriadoEm"",
                        COALESCE(
                            NULLIF(
                                trim(BOTH '.' FROM regexp_replace(
                                    lower(translate(
                                        ""Nome"",
                                        'ÁÀÂÃÄÉÈÊËÍÌÎÏÓÒÔÕÖÚÙÛÜÇÑáàâãäéèêëíìîïóòôõöúùûüçñ',
                                        'AAAAAEEEEIIIIOOOOOUUUUCNaaaaaeeeeiiiiooooouuuucn')),
                                    '[^a-z0-9]+', '.', 'g')),
                                ''),
                            'usuario') AS base
                    FROM profissionais
                ),
                numerado AS (
                    SELECT
                        ""Id"",
                        left(base, 36) AS base,
                        row_number() OVER (PARTITION BY base ORDER BY ""CriadoEm"", ""Id"") AS ordem
                    FROM normalizado
                )
                UPDATE profissionais p
                SET ""Usuario"" = CASE
                        WHEN n.ordem = 1 THEN n.base
                        ELSE n.base || n.ordem::text
                    END
                FROM numerado n
                WHERE p.""Id"" = n.""Id"";
            ");

            migrationBuilder.AlterColumn<string>(
                name: "Usuario",
                table: "profissionais",
                type: "character varying(40)",
                maxLength: 40,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(40)",
                oldMaxLength: 40,
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_profissionais_RevisadoPorId",
                table: "profissionais",
                column: "RevisadoPorId");

            migrationBuilder.CreateIndex(
                name: "IX_profissionais_Status",
                table: "profissionais",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_profissionais_Usuario",
                table: "profissionais",
                column: "Usuario",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_profissionais_profissionais_RevisadoPorId",
                table: "profissionais",
                column: "RevisadoPorId",
                principalTable: "profissionais",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_profissionais_profissionais_RevisadoPorId",
                table: "profissionais");

            migrationBuilder.DropIndex(
                name: "IX_profissionais_RevisadoPorId",
                table: "profissionais");

            migrationBuilder.DropIndex(
                name: "IX_profissionais_Status",
                table: "profissionais");

            migrationBuilder.DropIndex(
                name: "IX_profissionais_Usuario",
                table: "profissionais");

            migrationBuilder.AddColumn<bool>(
                name: "Ativo",
                table: "profissionais",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.Sql("UPDATE profissionais SET \"Ativo\" = (\"Status\" = 1);");

            migrationBuilder.DropColumn(name: "EhAdministrador", table: "profissionais");
            migrationBuilder.DropColumn(name: "Email", table: "profissionais");
            migrationBuilder.DropColumn(name: "MotivoRecusa", table: "profissionais");
            migrationBuilder.DropColumn(name: "RevisadoEm", table: "profissionais");
            migrationBuilder.DropColumn(name: "RevisadoPorId", table: "profissionais");
            migrationBuilder.DropColumn(name: "Status", table: "profissionais");
            migrationBuilder.DropColumn(name: "Usuario", table: "profissionais");

            migrationBuilder.CreateIndex(
                name: "IX_profissionais_Ativo",
                table: "profissionais",
                column: "Ativo");

            // Pode falhar se existirem homonimos criados enquanto a restricao
            // nao existia — que e exatamente a limitacao removida por esta
            // migration.
            migrationBuilder.CreateIndex(
                name: "IX_profissionais_Nome_Funcao",
                table: "profissionais",
                columns: new[] { "Nome", "Funcao" },
                unique: true);
        }
    }
}
