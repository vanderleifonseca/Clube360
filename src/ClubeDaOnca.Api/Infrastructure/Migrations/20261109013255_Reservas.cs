using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ClubeDaOnca.Api.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Reservas : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Espacos",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Nome = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Descricao = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Ativo = table.Column<bool>(type: "boolean", nullable: false),
                    Abertura = table.Column<TimeOnly>(type: "time without time zone", nullable: false),
                    Fechamento = table.Column<TimeOnly>(type: "time without time zone", nullable: false),
                    DuracaoMinutos = table.Column<int>(type: "integer", nullable: false),
                    DiasSemana = table.Column<int[]>(type: "integer[]", nullable: false),
                    AntecedenciaMinimaHoras = table.Column<int>(type: "integer", nullable: false),
                    AntecedenciaMaximaDias = table.Column<int>(type: "integer", nullable: false),
                    LimiteReservasAtivas = table.Column<int>(type: "integer", nullable: false),
                    Taxa = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: false),
                    ExigeAprovacao = table.Column<bool>(type: "boolean", nullable: false),
                    PrazoCancelamentoHoras = table.Column<int>(type: "integer", nullable: false),
                    CriadoEm = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Espacos", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Reservas",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    EspacoId = table.Column<Guid>(type: "uuid", nullable: false),
                    SocioId = table.Column<Guid>(type: "uuid", nullable: false),
                    Data = table.Column<DateOnly>(type: "date", nullable: false),
                    Inicio = table.Column<TimeOnly>(type: "time without time zone", nullable: false),
                    Fim = table.Column<TimeOnly>(type: "time without time zone", nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Taxa = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: false),
                    Observacao = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    MotivoRecusa = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    CriadoEm = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    AtualizadoEm = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Reservas", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Reservas_Espacos_EspacoId",
                        column: x => x.EspacoId,
                        principalTable: "Espacos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Reservas_Socios_SocioId",
                        column: x => x.SocioId,
                        principalTable: "Socios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Espacos_Nome",
                table: "Espacos",
                column: "Nome",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Reservas_HorarioAtivo",
                table: "Reservas",
                columns: new[] { "EspacoId", "Data", "Inicio" },
                unique: true,
                filter: "\"Status\" IN ('Pendente', 'Confirmada')");

            migrationBuilder.CreateIndex(
                name: "IX_Reservas_SocioId_Data",
                table: "Reservas",
                columns: new[] { "SocioId", "Data" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Reservas");

            migrationBuilder.DropTable(
                name: "Espacos");
        }
    }
}
