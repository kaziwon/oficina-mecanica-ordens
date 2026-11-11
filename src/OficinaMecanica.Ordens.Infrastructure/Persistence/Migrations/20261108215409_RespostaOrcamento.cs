using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OficinaMecanica.Ordens.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RespostaOrcamento : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "EventoRespostaOrcamentoId",
                table: "SagasAbertura",
                type: "char(36)",
                nullable: true,
                collation: "ascii_general_ci");

            migrationBuilder.AddColumn<bool>(
                name: "OrcamentoAprovado",
                table: "SagasAbertura",
                type: "tinyint(1)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "OrcamentoCriadoEm",
                table: "SagasAbertura",
                type: "datetime(6)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "OrcamentoRespondidoEm",
                table: "SagasAbertura",
                type: "datetime(6)",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "PedidoCancelamentoPorRecusaId",
                table: "SagasAbertura",
                type: "char(36)",
                nullable: true,
                collation: "ascii_general_ci");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "EventoRespostaOrcamentoId",
                table: "SagasAbertura");

            migrationBuilder.DropColumn(
                name: "OrcamentoAprovado",
                table: "SagasAbertura");

            migrationBuilder.DropColumn(
                name: "OrcamentoCriadoEm",
                table: "SagasAbertura");

            migrationBuilder.DropColumn(
                name: "OrcamentoRespondidoEm",
                table: "SagasAbertura");

            migrationBuilder.DropColumn(
                name: "PedidoCancelamentoPorRecusaId",
                table: "SagasAbertura");
        }
    }
}
