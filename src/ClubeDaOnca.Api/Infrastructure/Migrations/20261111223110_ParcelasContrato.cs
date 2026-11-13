using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace ClubeDaOnca.Api.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class ParcelasContrato : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "ContratoId",
                table: "Cobrancas",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "EmitidaEm",
                table: "Cobrancas",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Parcela",
                table: "Cobrancas",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AsaasApiKeyProtegida",
                table: "Associacoes",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AsaasWebhookTokenProtegido",
                table: "Associacoes",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "DataProtectionKeys",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    FriendlyName = table.Column<string>(type: "text", nullable: true),
                    Xml = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DataProtectionKeys", x => x.Id);
                });

            migrationBuilder.UpdateData(
                table: "Associacoes",
                keyColumn: "Id",
                keyValue: new Guid("0c1b0000-0000-4000-8000-000000000001"),
                columns: new[] { "AsaasApiKeyProtegida", "AsaasWebhookTokenProtegido" },
                values: new object[] { null, null });

            migrationBuilder.CreateIndex(
                name: "IX_Cobrancas_ContratoId_Parcela",
                table: "Cobrancas",
                columns: new[] { "ContratoId", "Parcela" });

            migrationBuilder.AddForeignKey(
                name: "FK_Cobrancas_Contratos_ContratoId",
                table: "Cobrancas",
                column: "ContratoId",
                principalTable: "Contratos",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Cobrancas_Contratos_ContratoId",
                table: "Cobrancas");

            migrationBuilder.DropTable(
                name: "DataProtectionKeys");

            migrationBuilder.DropIndex(
                name: "IX_Cobrancas_ContratoId_Parcela",
                table: "Cobrancas");

            migrationBuilder.DropColumn(
                name: "ContratoId",
                table: "Cobrancas");

            migrationBuilder.DropColumn(
                name: "EmitidaEm",
                table: "Cobrancas");

            migrationBuilder.DropColumn(
                name: "Parcela",
                table: "Cobrancas");

            migrationBuilder.DropColumn(
                name: "AsaasApiKeyProtegida",
                table: "Associacoes");

            migrationBuilder.DropColumn(
                name: "AsaasWebhookTokenProtegido",
                table: "Associacoes");
        }
    }
}
