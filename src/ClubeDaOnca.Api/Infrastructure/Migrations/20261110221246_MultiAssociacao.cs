using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ClubeDaOnca.Api.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class MultiAssociacao : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Socios_Cpf",
                table: "Socios");

            migrationBuilder.DropIndex(
                name: "IX_Espacos_Nome",
                table: "Espacos");

            migrationBuilder.DropIndex(
                name: "IX_Dependentes_Cpf",
                table: "Dependentes");

            migrationBuilder.DropPrimaryKey(
                name: "PK_ConfiguracaoFinanceira",
                table: "ConfiguracaoFinanceira");

            migrationBuilder.DropColumn(
                name: "Id",
                table: "ConfiguracaoFinanceira");

            migrationBuilder.AddColumn<Guid>(
                name: "AssociacaoId",
                table: "Socios",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("0c1b0000-0000-4000-8000-000000000001"));

            migrationBuilder.AddColumn<Guid>(
                name: "AssociacaoId",
                table: "Reservas",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("0c1b0000-0000-4000-8000-000000000001"));

            migrationBuilder.AddColumn<Guid>(
                name: "AssociacaoId",
                table: "RegistrosAcesso",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("0c1b0000-0000-4000-8000-000000000001"));

            migrationBuilder.AddColumn<Guid>(
                name: "AssociacaoId",
                table: "Pagamentos",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("0c1b0000-0000-4000-8000-000000000001"));

            migrationBuilder.AddColumn<Guid>(
                name: "AssociacaoId",
                table: "Espacos",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("0c1b0000-0000-4000-8000-000000000001"));

            migrationBuilder.AddColumn<Guid>(
                name: "AssociacaoId",
                table: "Dependentes",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("0c1b0000-0000-4000-8000-000000000001"));

            migrationBuilder.AddColumn<Guid>(
                name: "AssociacaoId",
                table: "ConfiguracaoFinanceira",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("0c1b0000-0000-4000-8000-000000000001"));

            migrationBuilder.AddColumn<Guid>(
                name: "AssociacaoId",
                table: "Cobrancas",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("0c1b0000-0000-4000-8000-000000000001"));

            migrationBuilder.AddColumn<Guid>(
                name: "AssociacaoId",
                table: "AspNetUsers",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddPrimaryKey(
                name: "PK_ConfiguracaoFinanceira",
                table: "ConfiguracaoFinanceira",
                column: "AssociacaoId");

            migrationBuilder.CreateTable(
                name: "Associacoes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Nome = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    Cnpj = table.Column<string>(type: "character varying(14)", maxLength: 14, nullable: true),
                    Email = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    Telefone = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    Cep = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: true),
                    Logradouro = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    Numero = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    Complemento = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Bairro = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Cidade = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Uf = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: true),
                    Ativa = table.Column<bool>(type: "boolean", nullable: false),
                    CriadoEm = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Associacoes", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Diretores",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AssociacaoId = table.Column<Guid>(type: "uuid", nullable: false),
                    Nome = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    Cargo = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    Email = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    Telefone = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    Inicio = table.Column<DateOnly>(type: "date", nullable: false),
                    Fim = table.Column<DateOnly>(type: "date", nullable: true),
                    CriadoEm = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Diretores", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Diretores_Associacoes_AssociacaoId",
                        column: x => x.AssociacaoId,
                        principalTable: "Associacoes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "MandatosPresidente",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AssociacaoId = table.Column<Guid>(type: "uuid", nullable: false),
                    Nome = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    Cpf = table.Column<string>(type: "character varying(11)", maxLength: 11, nullable: true),
                    Email = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    Inicio = table.Column<DateOnly>(type: "date", nullable: false),
                    Fim = table.Column<DateOnly>(type: "date", nullable: false),
                    CriadoEm = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MandatosPresidente", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MandatosPresidente_Associacoes_AssociacaoId",
                        column: x => x.AssociacaoId,
                        principalTable: "Associacoes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.InsertData(
                table: "Associacoes",
                columns: new[] { "Id", "Ativa", "Cnpj", "CriadoEm", "Email", "Nome", "Telefone", "Bairro", "Cep", "Cidade", "Complemento", "Logradouro", "Numero", "Uf" },
                values: new object[] { new Guid("0c1b0000-0000-4000-8000-000000000001"), true, null, new DateTimeOffset(new DateTime(2026, 11, 10, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, "Clube da Onça", null, null, null, null, null, null, null, null });

            // Sistema era de clube único: todas as contas existentes passam a pertencer à associação padrão.
            migrationBuilder.Sql("UPDATE \"AspNetUsers\" SET \"AssociacaoId\" = '0c1b0000-0000-4000-8000-000000000001';");

            migrationBuilder.CreateIndex(
                name: "IX_Socios_AssociacaoId",
                table: "Socios",
                column: "AssociacaoId");

            migrationBuilder.CreateIndex(
                name: "IX_Socios_AssociacaoId_Cpf",
                table: "Socios",
                columns: new[] { "AssociacaoId", "Cpf" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Reservas_AssociacaoId",
                table: "Reservas",
                column: "AssociacaoId");

            migrationBuilder.CreateIndex(
                name: "IX_RegistrosAcesso_AssociacaoId",
                table: "RegistrosAcesso",
                column: "AssociacaoId");

            migrationBuilder.CreateIndex(
                name: "IX_Pagamentos_AssociacaoId",
                table: "Pagamentos",
                column: "AssociacaoId");

            migrationBuilder.CreateIndex(
                name: "IX_Espacos_AssociacaoId",
                table: "Espacos",
                column: "AssociacaoId");

            migrationBuilder.CreateIndex(
                name: "IX_Espacos_AssociacaoId_Nome",
                table: "Espacos",
                columns: new[] { "AssociacaoId", "Nome" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Dependentes_AssociacaoId",
                table: "Dependentes",
                column: "AssociacaoId");

            migrationBuilder.CreateIndex(
                name: "IX_Dependentes_AssociacaoId_Cpf",
                table: "Dependentes",
                columns: new[] { "AssociacaoId", "Cpf" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ConfiguracaoFinanceira_AssociacaoId",
                table: "ConfiguracaoFinanceira",
                column: "AssociacaoId");

            migrationBuilder.CreateIndex(
                name: "IX_Cobrancas_AssociacaoId",
                table: "Cobrancas",
                column: "AssociacaoId");

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUsers_AssociacaoId",
                table: "AspNetUsers",
                column: "AssociacaoId");

            migrationBuilder.CreateIndex(
                name: "IX_Associacoes_Cnpj",
                table: "Associacoes",
                column: "Cnpj",
                unique: true,
                filter: "\"Cnpj\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Associacoes_Nome",
                table: "Associacoes",
                column: "Nome",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Diretores_AssociacaoId",
                table: "Diretores",
                column: "AssociacaoId");

            migrationBuilder.CreateIndex(
                name: "IX_MandatosPresidente_AssociacaoId_Inicio",
                table: "MandatosPresidente",
                columns: new[] { "AssociacaoId", "Inicio" });

            migrationBuilder.AddForeignKey(
                name: "FK_AspNetUsers_Associacoes_AssociacaoId",
                table: "AspNetUsers",
                column: "AssociacaoId",
                principalTable: "Associacoes",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Cobrancas_Associacoes_AssociacaoId",
                table: "Cobrancas",
                column: "AssociacaoId",
                principalTable: "Associacoes",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ConfiguracaoFinanceira_Associacoes_AssociacaoId",
                table: "ConfiguracaoFinanceira",
                column: "AssociacaoId",
                principalTable: "Associacoes",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Dependentes_Associacoes_AssociacaoId",
                table: "Dependentes",
                column: "AssociacaoId",
                principalTable: "Associacoes",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Espacos_Associacoes_AssociacaoId",
                table: "Espacos",
                column: "AssociacaoId",
                principalTable: "Associacoes",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Pagamentos_Associacoes_AssociacaoId",
                table: "Pagamentos",
                column: "AssociacaoId",
                principalTable: "Associacoes",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_RegistrosAcesso_Associacoes_AssociacaoId",
                table: "RegistrosAcesso",
                column: "AssociacaoId",
                principalTable: "Associacoes",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Reservas_Associacoes_AssociacaoId",
                table: "Reservas",
                column: "AssociacaoId",
                principalTable: "Associacoes",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Socios_Associacoes_AssociacaoId",
                table: "Socios",
                column: "AssociacaoId",
                principalTable: "Associacoes",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            // O padrão só serviu para migrar os dados existentes; registros novos sempre informam a associação.
            migrationBuilder.Sql("ALTER TABLE \"Socios\" ALTER COLUMN \"AssociacaoId\" DROP DEFAULT;");
            migrationBuilder.Sql("ALTER TABLE \"Reservas\" ALTER COLUMN \"AssociacaoId\" DROP DEFAULT;");
            migrationBuilder.Sql("ALTER TABLE \"RegistrosAcesso\" ALTER COLUMN \"AssociacaoId\" DROP DEFAULT;");
            migrationBuilder.Sql("ALTER TABLE \"Pagamentos\" ALTER COLUMN \"AssociacaoId\" DROP DEFAULT;");
            migrationBuilder.Sql("ALTER TABLE \"Espacos\" ALTER COLUMN \"AssociacaoId\" DROP DEFAULT;");
            migrationBuilder.Sql("ALTER TABLE \"Dependentes\" ALTER COLUMN \"AssociacaoId\" DROP DEFAULT;");
            migrationBuilder.Sql("ALTER TABLE \"ConfiguracaoFinanceira\" ALTER COLUMN \"AssociacaoId\" DROP DEFAULT;");
            migrationBuilder.Sql("ALTER TABLE \"Cobrancas\" ALTER COLUMN \"AssociacaoId\" DROP DEFAULT;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AspNetUsers_Associacoes_AssociacaoId",
                table: "AspNetUsers");

            migrationBuilder.DropForeignKey(
                name: "FK_Cobrancas_Associacoes_AssociacaoId",
                table: "Cobrancas");

            migrationBuilder.DropForeignKey(
                name: "FK_ConfiguracaoFinanceira_Associacoes_AssociacaoId",
                table: "ConfiguracaoFinanceira");

            migrationBuilder.DropForeignKey(
                name: "FK_Dependentes_Associacoes_AssociacaoId",
                table: "Dependentes");

            migrationBuilder.DropForeignKey(
                name: "FK_Espacos_Associacoes_AssociacaoId",
                table: "Espacos");

            migrationBuilder.DropForeignKey(
                name: "FK_Pagamentos_Associacoes_AssociacaoId",
                table: "Pagamentos");

            migrationBuilder.DropForeignKey(
                name: "FK_RegistrosAcesso_Associacoes_AssociacaoId",
                table: "RegistrosAcesso");

            migrationBuilder.DropForeignKey(
                name: "FK_Reservas_Associacoes_AssociacaoId",
                table: "Reservas");

            migrationBuilder.DropForeignKey(
                name: "FK_Socios_Associacoes_AssociacaoId",
                table: "Socios");

            migrationBuilder.DropTable(
                name: "Diretores");

            migrationBuilder.DropTable(
                name: "MandatosPresidente");

            migrationBuilder.DropTable(
                name: "Associacoes");

            migrationBuilder.DropIndex(
                name: "IX_Socios_AssociacaoId",
                table: "Socios");

            migrationBuilder.DropIndex(
                name: "IX_Socios_AssociacaoId_Cpf",
                table: "Socios");

            migrationBuilder.DropIndex(
                name: "IX_Reservas_AssociacaoId",
                table: "Reservas");

            migrationBuilder.DropIndex(
                name: "IX_RegistrosAcesso_AssociacaoId",
                table: "RegistrosAcesso");

            migrationBuilder.DropIndex(
                name: "IX_Pagamentos_AssociacaoId",
                table: "Pagamentos");

            migrationBuilder.DropIndex(
                name: "IX_Espacos_AssociacaoId",
                table: "Espacos");

            migrationBuilder.DropIndex(
                name: "IX_Espacos_AssociacaoId_Nome",
                table: "Espacos");

            migrationBuilder.DropIndex(
                name: "IX_Dependentes_AssociacaoId",
                table: "Dependentes");

            migrationBuilder.DropIndex(
                name: "IX_Dependentes_AssociacaoId_Cpf",
                table: "Dependentes");

            migrationBuilder.DropPrimaryKey(
                name: "PK_ConfiguracaoFinanceira",
                table: "ConfiguracaoFinanceira");

            migrationBuilder.DropIndex(
                name: "IX_ConfiguracaoFinanceira_AssociacaoId",
                table: "ConfiguracaoFinanceira");

            migrationBuilder.DropIndex(
                name: "IX_Cobrancas_AssociacaoId",
                table: "Cobrancas");

            migrationBuilder.DropIndex(
                name: "IX_AspNetUsers_AssociacaoId",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "AssociacaoId",
                table: "Socios");

            migrationBuilder.DropColumn(
                name: "AssociacaoId",
                table: "Reservas");

            migrationBuilder.DropColumn(
                name: "AssociacaoId",
                table: "RegistrosAcesso");

            migrationBuilder.DropColumn(
                name: "AssociacaoId",
                table: "Pagamentos");

            migrationBuilder.DropColumn(
                name: "AssociacaoId",
                table: "Espacos");

            migrationBuilder.DropColumn(
                name: "AssociacaoId",
                table: "Dependentes");

            migrationBuilder.DropColumn(
                name: "AssociacaoId",
                table: "ConfiguracaoFinanceira");

            migrationBuilder.DropColumn(
                name: "AssociacaoId",
                table: "Cobrancas");

            migrationBuilder.DropColumn(
                name: "AssociacaoId",
                table: "AspNetUsers");

            migrationBuilder.AddColumn<int>(
                name: "Id",
                table: "ConfiguracaoFinanceira",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddPrimaryKey(
                name: "PK_ConfiguracaoFinanceira",
                table: "ConfiguracaoFinanceira",
                column: "Id");

            migrationBuilder.CreateIndex(
                name: "IX_Socios_Cpf",
                table: "Socios",
                column: "Cpf",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Espacos_Nome",
                table: "Espacos",
                column: "Nome",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Dependentes_Cpf",
                table: "Dependentes",
                column: "Cpf",
                unique: true);
        }
    }
}
