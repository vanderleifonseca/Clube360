using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ClubeDaOnca.Api.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Comunicacao : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "ReceberAvisosApp",
                table: "Socios",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<bool>(
                name: "ReceberEmail",
                table: "Socios",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<bool>(
                name: "ReceberWhatsApp",
                table: "Socios",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.CreateTable(
                name: "Avisos",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AssociacaoId = table.Column<Guid>(type: "uuid", nullable: false),
                    SocioId = table.Column<Guid>(type: "uuid", nullable: false),
                    Tipo = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Titulo = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    Mensagem = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    ReferenciaId = table.Column<Guid>(type: "uuid", nullable: true),
                    DivulgacaoId = table.Column<Guid>(type: "uuid", nullable: true),
                    CriadoEm = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    LidoEm = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Avisos", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Avisos_Associacoes_AssociacaoId",
                        column: x => x.AssociacaoId,
                        principalTable: "Associacoes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Avisos_Socios_SocioId",
                        column: x => x.SocioId,
                        principalTable: "Socios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Divulgacoes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AssociacaoId = table.Column<Guid>(type: "uuid", nullable: false),
                    Titulo = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    Mensagem = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    EventoId = table.Column<Guid>(type: "uuid", nullable: true),
                    Canais = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    CriadoPor = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    CriadoEm = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ConcluidaEm = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Divulgacoes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Divulgacoes_Associacoes_AssociacaoId",
                        column: x => x.AssociacaoId,
                        principalTable: "Associacoes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "EnviosDivulgacao",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    DivulgacaoId = table.Column<Guid>(type: "uuid", nullable: false),
                    SocioId = table.Column<Guid>(type: "uuid", nullable: false),
                    Canal = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Detalhe = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    Em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EnviosDivulgacao", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EnviosDivulgacao_Divulgacoes_DivulgacaoId",
                        column: x => x.DivulgacaoId,
                        principalTable: "Divulgacoes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Avisos_AssociacaoId",
                table: "Avisos",
                column: "AssociacaoId");

            migrationBuilder.CreateIndex(
                name: "IX_Avisos_ReferenciaId",
                table: "Avisos",
                column: "ReferenciaId");

            migrationBuilder.CreateIndex(
                name: "IX_Avisos_SocioId_CriadoEm",
                table: "Avisos",
                columns: new[] { "SocioId", "CriadoEm" });

            migrationBuilder.CreateIndex(
                name: "IX_Divulgacoes_AssociacaoId",
                table: "Divulgacoes",
                column: "AssociacaoId");

            migrationBuilder.CreateIndex(
                name: "IX_Divulgacoes_CriadoEm",
                table: "Divulgacoes",
                column: "CriadoEm");

            migrationBuilder.CreateIndex(
                name: "IX_EnviosDivulgacao_DivulgacaoId_Status",
                table: "EnviosDivulgacao",
                columns: new[] { "DivulgacaoId", "Status" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Avisos");

            migrationBuilder.DropTable(
                name: "EnviosDivulgacao");

            migrationBuilder.DropTable(
                name: "Divulgacoes");

            migrationBuilder.DropColumn(
                name: "ReceberAvisosApp",
                table: "Socios");

            migrationBuilder.DropColumn(
                name: "ReceberEmail",
                table: "Socios");

            migrationBuilder.DropColumn(
                name: "ReceberWhatsApp",
                table: "Socios");
        }
    }
}
