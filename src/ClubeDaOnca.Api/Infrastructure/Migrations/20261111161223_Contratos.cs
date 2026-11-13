using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ClubeDaOnca.Api.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Contratos : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Contratos",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AssociacaoId = table.Column<Guid>(type: "uuid", nullable: false),
                    SocioId = table.Column<Guid>(type: "uuid", nullable: false),
                    Numero = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Ano = table.Column<int>(type: "integer", nullable: false),
                    Sequencial = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Categoria = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    ValorMensalidade = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: false),
                    DiaVencimento = table.Column<int>(type: "integer", nullable: false),
                    PrimeiroVencimento = table.Column<DateOnly>(type: "date", nullable: false),
                    VigenciaInicio = table.Column<DateOnly>(type: "date", nullable: true),
                    VigenciaFim = table.Column<DateOnly>(type: "date", nullable: true),
                    RenovacaoAutomatica = table.Column<bool>(type: "boolean", nullable: false),
                    ContratoAnteriorId = table.Column<Guid>(type: "uuid", nullable: true),
                    Dependentes = table.Column<string>(type: "jsonb", nullable: false),
                    EnviadoEm = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    EnviadoPara = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    TokenAceiteHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    TokenExpiraEm = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    AceiteEm = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    AceiteIp = table.Column<string>(type: "character varying(45)", maxLength: 45, nullable: true),
                    AprovadoEm = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    AprovadoPor = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    AprovadoPorUsuarioId = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: true),
                    MotivoReprovacao = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    DesligadoEm = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    MotivoDesligamento = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Documento = table.Column<byte[]>(type: "bytea", nullable: true),
                    DocumentoTipo = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    DocumentoGeradoEm = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CriadoEm = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Contratos", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Contratos_Associacoes_AssociacaoId",
                        column: x => x.AssociacaoId,
                        principalTable: "Associacoes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Contratos_Contratos_ContratoAnteriorId",
                        column: x => x.ContratoAnteriorId,
                        principalTable: "Contratos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Contratos_Socios_SocioId",
                        column: x => x.SocioId,
                        principalTable: "Socios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Contratos_AssociacaoId",
                table: "Contratos",
                column: "AssociacaoId");

            migrationBuilder.CreateIndex(
                name: "IX_Contratos_AssociacaoId_Ano_Sequencial",
                table: "Contratos",
                columns: new[] { "AssociacaoId", "Ano", "Sequencial" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Contratos_ContratoAnteriorId",
                table: "Contratos",
                column: "ContratoAnteriorId");

            migrationBuilder.CreateIndex(
                name: "IX_Contratos_SocioId_CriadoEm",
                table: "Contratos",
                columns: new[] { "SocioId", "CriadoEm" });

            migrationBuilder.CreateIndex(
                name: "IX_Contratos_Status_VigenciaFim",
                table: "Contratos",
                columns: new[] { "Status", "VigenciaFim" });

            migrationBuilder.CreateIndex(
                name: "IX_Contratos_TokenAceiteHash",
                table: "Contratos",
                column: "TokenAceiteHash",
                unique: true,
                filter: "\"TokenAceiteHash\" IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Contratos");
        }
    }
}
