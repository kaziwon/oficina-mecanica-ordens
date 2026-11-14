using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OficinaMecanica.Ordens.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class CompensacaoPagamento : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "CompensacaoRecusadaEm",
                table: "SagasAbertura",
                type: "datetime(6)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "CompensacaoSolicitadaEm",
                table: "SagasAbertura",
                type: "datetime(6)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "EstornadoEm",
                table: "SagasAbertura",
                type: "datetime(6)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "EstornoProvedorId",
                table: "SagasAbertura",
                type: "varchar(100)",
                maxLength: 100,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<DateTime>(
                name: "ExecucaoCanceladaEm",
                table: "SagasAbertura",
                type: "datetime(6)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MotivoCompensacao",
                table: "SagasAbertura",
                type: "varchar(1000)",
                maxLength: 1000,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<Guid>(
                name: "PedidoCompensacaoId",
                table: "SagasAbertura",
                type: "char(36)",
                nullable: true,
                collation: "ascii_general_ci");

            migrationBuilder.AddColumn<Guid>(
                name: "PedidoEstornoId",
                table: "SagasAbertura",
                type: "char(36)",
                nullable: true,
                collation: "ascii_general_ci");

            migrationBuilder.AddColumn<string>(
                name: "StatusAntesCompensacao",
                table: "SagasAbertura",
                type: "varchar(40)",
                maxLength: 40,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CompensacaoRecusadaEm",
                table: "SagasAbertura");

            migrationBuilder.DropColumn(
                name: "CompensacaoSolicitadaEm",
                table: "SagasAbertura");

            migrationBuilder.DropColumn(
                name: "EstornadoEm",
                table: "SagasAbertura");

            migrationBuilder.DropColumn(
                name: "EstornoProvedorId",
                table: "SagasAbertura");

            migrationBuilder.DropColumn(
                name: "ExecucaoCanceladaEm",
                table: "SagasAbertura");

            migrationBuilder.DropColumn(
                name: "MotivoCompensacao",
                table: "SagasAbertura");

            migrationBuilder.DropColumn(
                name: "PedidoCompensacaoId",
                table: "SagasAbertura");

            migrationBuilder.DropColumn(
                name: "PedidoEstornoId",
                table: "SagasAbertura");

            migrationBuilder.DropColumn(
                name: "StatusAntesCompensacao",
                table: "SagasAbertura");
        }
    }
}
