using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AtendimentoDeCampo.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class TransoperatorioERecuperacaoPosAnestesica : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "RecuperacaoDieta",
                table: "cirurgia",
                type: "character varying(300)",
                maxLength: 300,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "PreparoTricotomiaLocal",
                table: "cirurgia",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "PreparoCuidadosEnfermagem",
                table: "cirurgia",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "PreOpExamesObservacao",
                table: "cirurgia",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "PertencesDestino",
                table: "cirurgia",
                type: "character varying(300)",
                maxLength: 300,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.AddColumn<int[]>(
                name: "Anestesia",
                table: "cirurgia",
                type: "integer[]",
                nullable: false,
                defaultValue: new int[0]);

            migrationBuilder.AddColumn<string>(
                name: "AnestesiaLocal",
                table: "cirurgia",
                type: "character varying(300)",
                maxLength: 300,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Antissepsia",
                table: "cirurgia",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "CheckOutBiopsiaQuantidade",
                table: "cirurgia",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ChegadaAoCentroEm",
                table: "cirurgia",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "CompressasUtilizadas",
                table: "cirurgia",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Curativo",
                table: "cirurgia",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "DestinoPosOperatorio",
                table: "cirurgia",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "EvolucaoEnfermagem",
                table: "cirurgia",
                type: "character varying(4000)",
                maxLength: 4000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "EvolucaoMedica",
                table: "cirurgia",
                type: "character varying(4000)",
                maxLength: 4000,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "GazesUtilizadas",
                table: "cirurgia",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "IncisaoCirurgica",
                table: "cirurgia",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MedicacoesRealizadas",
                table: "cirurgia",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "PlacaBisturi",
                table: "cirurgia",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "PlacaBisturiLocal",
                table: "cirurgia",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Posicionamento",
                table: "cirurgia",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PreparoDaPele",
                table: "cirurgia",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RecuperacaoAcessoVenosoDescricao",
                table: "cirurgia",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "RecuperacaoAcessoVenosoPeriferico",
                table: "cirurgia",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "RecuperacaoCateterVenosoCentral",
                table: "cirurgia",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "RecuperacaoCateterVenosoCentralLocal",
                table: "cirurgia",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "RecuperacaoDreno",
                table: "cirurgia",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "RecuperacaoDrenoDescricao",
                table: "cirurgia",
                type: "character varying(300)",
                maxLength: 300,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RecuperacaoDrogasVasoativas",
                table: "cirurgia",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RecuperacaoEvolucaoEnfermagem",
                table: "cirurgia",
                type: "character varying(4000)",
                maxLength: 4000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RecuperacaoEvolucaoMedica",
                table: "cirurgia",
                type: "character varying(4000)",
                maxLength: 4000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RecuperacaoInfusoes",
                table: "cirurgia",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "RecuperacaoIntervencaoIntensiva",
                table: "cirurgia",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "RecuperacaoNivelConsciencia",
                table: "cirurgia",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RecuperacaoOutroDispositivo",
                table: "cirurgia",
                type: "character varying(300)",
                maxLength: 300,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RecuperacaoOxigenioDetalhe",
                table: "cirurgia",
                type: "character varying(60)",
                maxLength: 60,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "RecuperacaoSondaNasogastrica",
                table: "cirurgia",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "RecuperacaoSondaVesical",
                table: "cirurgia",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "RecuperacaoSuporteOxigenio",
                table: "cirurgia",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TimeOutDoisAntibioticoQual",
                table: "cirurgia",
                type: "character varying(300)",
                maxLength: 300,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "TimeOutDoisInstrumentalIntegrador",
                table: "cirurgia",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "TimeOutDoisPrevisaoBiopsia",
                table: "cirurgia",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "TimeOutDoisTempoPrevistoMinutos",
                table: "cirurgia",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "TimeOutUmExamesEmSala",
                table: "cirurgia",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "TimeOutUmVerificacaoAnestesica",
                table: "cirurgia",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "TimeOutUmViaAereaDificil",
                table: "cirurgia",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Anestesia",
                table: "cirurgia");

            migrationBuilder.DropColumn(
                name: "AnestesiaLocal",
                table: "cirurgia");

            migrationBuilder.DropColumn(
                name: "Antissepsia",
                table: "cirurgia");

            migrationBuilder.DropColumn(
                name: "CheckOutBiopsiaQuantidade",
                table: "cirurgia");

            migrationBuilder.DropColumn(
                name: "ChegadaAoCentroEm",
                table: "cirurgia");

            migrationBuilder.DropColumn(
                name: "CompressasUtilizadas",
                table: "cirurgia");

            migrationBuilder.DropColumn(
                name: "Curativo",
                table: "cirurgia");

            migrationBuilder.DropColumn(
                name: "DestinoPosOperatorio",
                table: "cirurgia");

            migrationBuilder.DropColumn(
                name: "EvolucaoEnfermagem",
                table: "cirurgia");

            migrationBuilder.DropColumn(
                name: "EvolucaoMedica",
                table: "cirurgia");

            migrationBuilder.DropColumn(
                name: "GazesUtilizadas",
                table: "cirurgia");

            migrationBuilder.DropColumn(
                name: "IncisaoCirurgica",
                table: "cirurgia");

            migrationBuilder.DropColumn(
                name: "MedicacoesRealizadas",
                table: "cirurgia");

            migrationBuilder.DropColumn(
                name: "PlacaBisturi",
                table: "cirurgia");

            migrationBuilder.DropColumn(
                name: "PlacaBisturiLocal",
                table: "cirurgia");

            migrationBuilder.DropColumn(
                name: "Posicionamento",
                table: "cirurgia");

            migrationBuilder.DropColumn(
                name: "PreparoDaPele",
                table: "cirurgia");

            migrationBuilder.DropColumn(
                name: "RecuperacaoAcessoVenosoDescricao",
                table: "cirurgia");

            migrationBuilder.DropColumn(
                name: "RecuperacaoAcessoVenosoPeriferico",
                table: "cirurgia");

            migrationBuilder.DropColumn(
                name: "RecuperacaoCateterVenosoCentral",
                table: "cirurgia");

            migrationBuilder.DropColumn(
                name: "RecuperacaoCateterVenosoCentralLocal",
                table: "cirurgia");

            migrationBuilder.DropColumn(
                name: "RecuperacaoDreno",
                table: "cirurgia");

            migrationBuilder.DropColumn(
                name: "RecuperacaoDrenoDescricao",
                table: "cirurgia");

            migrationBuilder.DropColumn(
                name: "RecuperacaoDrogasVasoativas",
                table: "cirurgia");

            migrationBuilder.DropColumn(
                name: "RecuperacaoEvolucaoEnfermagem",
                table: "cirurgia");

            migrationBuilder.DropColumn(
                name: "RecuperacaoEvolucaoMedica",
                table: "cirurgia");

            migrationBuilder.DropColumn(
                name: "RecuperacaoInfusoes",
                table: "cirurgia");

            migrationBuilder.DropColumn(
                name: "RecuperacaoIntervencaoIntensiva",
                table: "cirurgia");

            migrationBuilder.DropColumn(
                name: "RecuperacaoNivelConsciencia",
                table: "cirurgia");

            migrationBuilder.DropColumn(
                name: "RecuperacaoOutroDispositivo",
                table: "cirurgia");

            migrationBuilder.DropColumn(
                name: "RecuperacaoOxigenioDetalhe",
                table: "cirurgia");

            migrationBuilder.DropColumn(
                name: "RecuperacaoSondaNasogastrica",
                table: "cirurgia");

            migrationBuilder.DropColumn(
                name: "RecuperacaoSondaVesical",
                table: "cirurgia");

            migrationBuilder.DropColumn(
                name: "RecuperacaoSuporteOxigenio",
                table: "cirurgia");

            migrationBuilder.DropColumn(
                name: "TimeOutDoisAntibioticoQual",
                table: "cirurgia");

            migrationBuilder.DropColumn(
                name: "TimeOutDoisInstrumentalIntegrador",
                table: "cirurgia");

            migrationBuilder.DropColumn(
                name: "TimeOutDoisPrevisaoBiopsia",
                table: "cirurgia");

            migrationBuilder.DropColumn(
                name: "TimeOutDoisTempoPrevistoMinutos",
                table: "cirurgia");

            migrationBuilder.DropColumn(
                name: "TimeOutUmExamesEmSala",
                table: "cirurgia");

            migrationBuilder.DropColumn(
                name: "TimeOutUmVerificacaoAnestesica",
                table: "cirurgia");

            migrationBuilder.DropColumn(
                name: "TimeOutUmViaAereaDificil",
                table: "cirurgia");

            migrationBuilder.AlterColumn<string>(
                name: "RecuperacaoDieta",
                table: "cirurgia",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(300)",
                oldMaxLength: 300,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "PreparoTricotomiaLocal",
                table: "cirurgia",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(200)",
                oldMaxLength: 200,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "PreparoCuidadosEnfermagem",
                table: "cirurgia",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(2000)",
                oldMaxLength: 2000,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "PreOpExamesObservacao",
                table: "cirurgia",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(500)",
                oldMaxLength: 500,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "PertencesDestino",
                table: "cirurgia",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(300)",
                oldMaxLength: 300,
                oldNullable: true);
        }
    }
}
