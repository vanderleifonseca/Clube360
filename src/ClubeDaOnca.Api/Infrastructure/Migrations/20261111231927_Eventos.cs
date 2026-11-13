using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ClubeDaOnca.Api.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Eventos : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Capacidade",
                table: "Espacos",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "Tipo",
                table: "Espacos",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "Outro");

            migrationBuilder.AddColumn<Guid>(
                name: "ReservaEventoId",
                table: "Cobrancas",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Eventos",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AssociacaoId = table.Column<Guid>(type: "uuid", nullable: false),
                    Titulo = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    Descricao = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    Inicio = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    Termino = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Capacidade = table.Column<int>(type: "integer", nullable: false),
                    AbertoAoPublico = table.Column<bool>(type: "boolean", nullable: false),
                    DivulgarAssociados = table.Column<bool>(type: "boolean", nullable: false),
                    DivulgarRedesSociais = table.Column<bool>(type: "boolean", nullable: false),
                    PrazoCancelamentoHoras = table.Column<int>(type: "integer", nullable: false),
                    MotivoCancelamento = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    CriadoEm = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Eventos", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Eventos_Associacoes_AssociacaoId",
                        column: x => x.AssociacaoId,
                        principalTable: "Associacoes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AmbientesEvento",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    EventoId = table.Column<Guid>(type: "uuid", nullable: false),
                    Nome = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    PessoasPorReserva = table.Column<int>(type: "integer", nullable: false),
                    Vagas = table.Column<int>(type: "integer", nullable: false),
                    LimitePorSocio = table.Column<int>(type: "integer", nullable: false),
                    Valor = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: false),
                    Reservadas = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AmbientesEvento", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AmbientesEvento_Eventos_EventoId",
                        column: x => x.EventoId,
                        principalTable: "Eventos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "EventoLocais",
                columns: table => new
                {
                    EventoId = table.Column<Guid>(type: "uuid", nullable: false),
                    EspacoId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EventoLocais", x => new { x.EventoId, x.EspacoId });
                    table.ForeignKey(
                        name: "FK_EventoLocais_Espacos_EspacoId",
                        column: x => x.EspacoId,
                        principalTable: "Espacos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_EventoLocais_Eventos_EventoId",
                        column: x => x.EventoId,
                        principalTable: "Eventos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ReservasEvento",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AssociacaoId = table.Column<Guid>(type: "uuid", nullable: false),
                    EventoId = table.Column<Guid>(type: "uuid", nullable: false),
                    AmbienteId = table.Column<Guid>(type: "uuid", nullable: false),
                    SocioId = table.Column<Guid>(type: "uuid", nullable: false),
                    Pessoas = table.Column<int>(type: "integer", nullable: false),
                    Valor = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: false),
                    Observacao = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    CriadoEm = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CanceladaEm = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReservasEvento", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ReservasEvento_AmbientesEvento_AmbienteId",
                        column: x => x.AmbienteId,
                        principalTable: "AmbientesEvento",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ReservasEvento_Associacoes_AssociacaoId",
                        column: x => x.AssociacaoId,
                        principalTable: "Associacoes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ReservasEvento_Eventos_EventoId",
                        column: x => x.EventoId,
                        principalTable: "Eventos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ReservasEvento_Socios_SocioId",
                        column: x => x.SocioId,
                        principalTable: "Socios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Cobrancas_ReservaEventoId",
                table: "Cobrancas",
                column: "ReservaEventoId",
                unique: true,
                filter: "\"ReservaEventoId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_AmbientesEvento_EventoId",
                table: "AmbientesEvento",
                column: "EventoId");

            migrationBuilder.CreateIndex(
                name: "IX_EventoLocais_EspacoId",
                table: "EventoLocais",
                column: "EspacoId");

            migrationBuilder.CreateIndex(
                name: "IX_Eventos_AssociacaoId",
                table: "Eventos",
                column: "AssociacaoId");

            migrationBuilder.CreateIndex(
                name: "IX_Eventos_Status_Inicio",
                table: "Eventos",
                columns: new[] { "Status", "Inicio" });

            migrationBuilder.CreateIndex(
                name: "IX_ReservasEvento_AmbienteId",
                table: "ReservasEvento",
                column: "AmbienteId");

            migrationBuilder.CreateIndex(
                name: "IX_ReservasEvento_AssociacaoId",
                table: "ReservasEvento",
                column: "AssociacaoId");

            migrationBuilder.CreateIndex(
                name: "IX_ReservasEvento_EventoId_Status",
                table: "ReservasEvento",
                columns: new[] { "EventoId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_ReservasEvento_SocioId_Status",
                table: "ReservasEvento",
                columns: new[] { "SocioId", "Status" });

            migrationBuilder.AddForeignKey(
                name: "FK_Cobrancas_ReservasEvento_ReservaEventoId",
                table: "Cobrancas",
                column: "ReservaEventoId",
                principalTable: "ReservasEvento",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Cobrancas_ReservasEvento_ReservaEventoId",
                table: "Cobrancas");

            migrationBuilder.DropTable(
                name: "EventoLocais");

            migrationBuilder.DropTable(
                name: "ReservasEvento");

            migrationBuilder.DropTable(
                name: "AmbientesEvento");

            migrationBuilder.DropTable(
                name: "Eventos");

            migrationBuilder.DropIndex(
                name: "IX_Cobrancas_ReservaEventoId",
                table: "Cobrancas");

            migrationBuilder.DropColumn(
                name: "Capacidade",
                table: "Espacos");

            migrationBuilder.DropColumn(
                name: "Tipo",
                table: "Espacos");

            migrationBuilder.DropColumn(
                name: "ReservaEventoId",
                table: "Cobrancas");
        }
    }
}
