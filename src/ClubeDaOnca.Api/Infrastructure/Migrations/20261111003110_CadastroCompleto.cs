using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ClubeDaOnca.Api.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class CadastroCompleto : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Bairro",
                table: "Socios",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Cep",
                table: "Socios",
                type: "character varying(8)",
                maxLength: 8,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Cidade",
                table: "Socios",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Complemento",
                table: "Socios",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "DataCadastro",
                table: "Socios",
                type: "date",
                nullable: false,
                defaultValue: new DateOnly(1, 1, 1));

            migrationBuilder.AddColumn<string>(
                name: "Genero",
                table: "Socios",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "NaoInformado");

            migrationBuilder.AddColumn<string>(
                name: "Logradouro",
                table: "Socios",
                type: "character varying(150)",
                maxLength: 150,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Numero",
                table: "Socios",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Profissao",
                table: "Socios",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Uf",
                table: "Socios",
                type: "character varying(2)",
                maxLength: 2,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Email",
                table: "Dependentes",
                type: "character varying(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Genero",
                table: "Dependentes",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "NaoInformado");

            migrationBuilder.AddColumn<string>(
                name: "Profissao",
                table: "Dependentes",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Telefone",
                table: "Dependentes",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            // Sócios existentes: data de cadastro = data em que o registro foi criado.
            migrationBuilder.Sql("UPDATE \"Socios\" SET \"DataCadastro\" = (\"CriadoEm\" AT TIME ZONE 'America/Sao_Paulo')::date;");

            migrationBuilder.CreateTable(
                name: "HistoricoAlteracoes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AssociacaoId = table.Column<Guid>(type: "uuid", nullable: false),
                    Entidade = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    EntidadeId = table.Column<Guid>(type: "uuid", nullable: false),
                    RaizId = table.Column<Guid>(type: "uuid", nullable: false),
                    Descricao = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Acao = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Campo = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: true),
                    ValorAnterior = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    ValorNovo = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    UsuarioId = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: true),
                    UsuarioNome = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    Em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HistoricoAlteracoes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_HistoricoAlteracoes_Associacoes_AssociacaoId",
                        column: x => x.AssociacaoId,
                        principalTable: "Associacoes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_HistoricoAlteracoes_AssociacaoId",
                table: "HistoricoAlteracoes",
                column: "AssociacaoId");

            migrationBuilder.CreateIndex(
                name: "IX_HistoricoAlteracoes_AssociacaoId_RaizId_Em",
                table: "HistoricoAlteracoes",
                columns: new[] { "AssociacaoId", "RaizId", "Em" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "HistoricoAlteracoes");

            migrationBuilder.DropColumn(
                name: "Bairro",
                table: "Socios");

            migrationBuilder.DropColumn(
                name: "Cep",
                table: "Socios");

            migrationBuilder.DropColumn(
                name: "Cidade",
                table: "Socios");

            migrationBuilder.DropColumn(
                name: "Complemento",
                table: "Socios");

            migrationBuilder.DropColumn(
                name: "DataCadastro",
                table: "Socios");

            migrationBuilder.DropColumn(
                name: "Genero",
                table: "Socios");

            migrationBuilder.DropColumn(
                name: "Logradouro",
                table: "Socios");

            migrationBuilder.DropColumn(
                name: "Numero",
                table: "Socios");

            migrationBuilder.DropColumn(
                name: "Profissao",
                table: "Socios");

            migrationBuilder.DropColumn(
                name: "Uf",
                table: "Socios");

            migrationBuilder.DropColumn(
                name: "Email",
                table: "Dependentes");

            migrationBuilder.DropColumn(
                name: "Genero",
                table: "Dependentes");

            migrationBuilder.DropColumn(
                name: "Profissao",
                table: "Dependentes");

            migrationBuilder.DropColumn(
                name: "Telefone",
                table: "Dependentes");
        }
    }
}
