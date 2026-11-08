using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OficinaMecanica.Ordens.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class DiagnosticoOrcamento : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "DiagnosticoConcluidoEm",
                table: "SagasAbertura",
                type: "datetime(6)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "DiagnosticoIniciadoEm",
                table: "SagasAbertura",
                type: "datetime(6)",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "EventoDiagnosticoConcluidoId",
                table: "SagasAbertura",
                type: "char(36)",
                nullable: true,
                collation: "ascii_general_ci");

            migrationBuilder.AddColumn<Guid>(
                name: "OrcamentoId",
                table: "SagasAbertura",
                type: "char(36)",
                nullable: true,
                collation: "ascii_general_ci");

            migrationBuilder.AddColumn<Guid>(
                name: "PedidoOrcamentoId",
                table: "SagasAbertura",
                type: "char(36)",
                nullable: true,
                collation: "ascii_general_ci");

            migrationBuilder.AddColumn<decimal>(
                name: "ValorOrcamento",
                table: "SagasAbertura",
                type: "decimal(11,2)",
                precision: 11,
                scale: 2,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DiagnosticoConcluidoEm",
                table: "SagasAbertura");

            migrationBuilder.DropColumn(
                name: "DiagnosticoIniciadoEm",
                table: "SagasAbertura");

            migrationBuilder.DropColumn(
                name: "EventoDiagnosticoConcluidoId",
                table: "SagasAbertura");

            migrationBuilder.DropColumn(
                name: "OrcamentoId",
                table: "SagasAbertura");

            migrationBuilder.DropColumn(
                name: "PedidoOrcamentoId",
                table: "SagasAbertura");

            migrationBuilder.DropColumn(
                name: "ValorOrcamento",
                table: "SagasAbertura");
        }
    }
}
