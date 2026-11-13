using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ClubeDaOnca.Api.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class QualificacaoCivil : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "EstadoCivil",
                table: "Socios",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "NaoInformado");

            migrationBuilder.AddColumn<string>(
                name: "Nacionalidade",
                table: "Socios",
                type: "character varying(60)",
                maxLength: 60,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Rg",
                table: "Socios",
                type: "character varying(30)",
                maxLength: 30,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "EstadoCivil",
                table: "Socios");

            migrationBuilder.DropColumn(
                name: "Nacionalidade",
                table: "Socios");

            migrationBuilder.DropColumn(
                name: "Rg",
                table: "Socios");
        }
    }
}
