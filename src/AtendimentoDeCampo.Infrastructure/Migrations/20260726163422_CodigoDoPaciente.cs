using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AtendimentoDeCampo.Infrastructure.Migrations
{
    /// <summary>
    /// Da um codigo proprio a cada paciente.
    ///
    /// Escrita a mao. O que o EF gerou adicionava a coluna NOT NULL com
    /// defaultValue "" e criava o indice unico em seguida: com dois pacientes ja
    /// cadastrados, a segunda linha viola a unicidade e a migracao aborta no meio
    /// do deploy.
    ///
    /// A ordem correta e: coluna anulavel, sorteio de um codigo para cada linha
    /// existente, e so entao NOT NULL e indice unico.
    /// </summary>
    public partial class CodigoDoPaciente : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Codigo",
                table: "pacientes",
                type: "character varying(9)",
                maxLength: 9,
                nullable: true);

            // Sorteia no banco, com o mesmo alfabeto do GeradorCodigoPaciente:
            // sem I, O, S, 0, 1 e 5, que se confundem escritos a mao.
            //
            // O laco interno repete enquanto o codigo sorteado ja existir. Sem
            // ele, uma colisao — improvavel, mas possivel — so apareceria na
            // criacao do indice unico, ou seja, com a migracao ja pela metade.
            migrationBuilder.Sql(@"
                DO $$
                DECLARE
                    alfabeto text := 'ABCDEFGHJKLMNPQRTUVWXYZ2346789';
                    linha    RECORD;
                    bruto    text;
                    novo     text;
                    i        int;
                BEGIN
                    FOR linha IN SELECT ""Id"" FROM pacientes WHERE ""Codigo"" IS NULL LOOP
                        LOOP
                            bruto := '';
                            FOR i IN 1..8 LOOP
                                bruto := bruto || substr(
                                    alfabeto,
                                    1 + floor(random() * length(alfabeto))::int,
                                    1);
                            END LOOP;

                            novo := substr(bruto, 1, 4) || '-' || substr(bruto, 5, 4);

                            EXIT WHEN NOT EXISTS (
                                SELECT 1 FROM pacientes WHERE ""Codigo"" = novo);
                        END LOOP;

                        UPDATE pacientes SET ""Codigo"" = novo WHERE ""Id"" = linha.""Id"";
                    END LOOP;
                END $$;
            ");

            migrationBuilder.AlterColumn<string>(
                name: "Codigo",
                table: "pacientes",
                type: "character varying(9)",
                maxLength: 9,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(9)",
                oldMaxLength: 9,
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_pacientes_Codigo",
                table: "pacientes",
                column: "Codigo",
                unique: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_pacientes_Codigo",
                table: "pacientes");

            migrationBuilder.DropColumn(
                name: "Codigo",
                table: "pacientes");
        }
    }
}
