using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ClubeDaOnca.Api.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Financeiro : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Cobrancas",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SocioId = table.Column<Guid>(type: "uuid", nullable: false),
                    Tipo = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Descricao = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Competencia = table.Column<DateOnly>(type: "date", nullable: true),
                    ReservaId = table.Column<Guid>(type: "uuid", nullable: true),
                    Valor = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: false),
                    Vencimento = table.Column<DateOnly>(type: "date", nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    PagoEm = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    FormaPagamento = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    Observacao = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    CriadoEm = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Cobrancas", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Cobrancas_Reservas_ReservaId",
                        column: x => x.ReservaId,
                        principalTable: "Reservas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Cobrancas_Socios_SocioId",
                        column: x => x.SocioId,
                        principalTable: "Socios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ConfiguracaoFinanceira",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false),
                    ValorIndividual = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: false),
                    ValorFamiliar = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: false),
                    ValorRemido = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: false),
                    DiaVencimento = table.Column<int>(type: "integer", nullable: false),
                    DiasTolerancia = table.Column<int>(type: "integer", nullable: false),
                    BloquearInadimplentesNaPortaria = table.Column<bool>(type: "boolean", nullable: false),
                    BloquearInadimplentesNasReservas = table.Column<bool>(type: "boolean", nullable: false),
                    PrazoTaxaReservaDias = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ConfiguracaoFinanceira", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Pagamentos",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CobrancaId = table.Column<Guid>(type: "uuid", nullable: false),
                    Gateway = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    IdExterno = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Forma = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Valor = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: false),
                    PixCopiaECola = table.Column<string>(type: "text", nullable: true),
                    PixImagemBase64 = table.Column<string>(type: "text", nullable: true),
                    PixExpiraEm = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    BoletoUrl = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    LinhaDigitavel = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    CriadoEm = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ConfirmadoEm = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Pagamentos", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Pagamentos_Cobrancas_CobrancaId",
                        column: x => x.CobrancaId,
                        principalTable: "Cobrancas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Cobrancas_ReservaId",
                table: "Cobrancas",
                column: "ReservaId",
                unique: true,
                filter: "\"ReservaId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Cobrancas_SocioId_Competencia",
                table: "Cobrancas",
                columns: new[] { "SocioId", "Competencia" },
                unique: true,
                filter: "\"Competencia\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Cobrancas_Status_Vencimento",
                table: "Cobrancas",
                columns: new[] { "Status", "Vencimento" });

            migrationBuilder.CreateIndex(
                name: "IX_Pagamentos_CobrancaId",
                table: "Pagamentos",
                column: "CobrancaId");

            migrationBuilder.CreateIndex(
                name: "IX_Pagamentos_Gateway_IdExterno",
                table: "Pagamentos",
                columns: new[] { "Gateway", "IdExterno" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ConfiguracaoFinanceira");

            migrationBuilder.DropTable(
                name: "Pagamentos");

            migrationBuilder.DropTable(
                name: "Cobrancas");
        }
    }
}
