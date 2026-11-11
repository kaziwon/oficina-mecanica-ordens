using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OficinaMecanica.Ordens.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class LiberacaoExecucao : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "EventoPagamentoConfirmadoId",
                table: "SagasAbertura",
                type: "char(36)",
                nullable: true,
                collation: "ascii_general_ci");

            migrationBuilder.AddColumn<DateTime>(
                name: "ExecucaoLiberadaEm",
                table: "SagasAbertura",
                type: "datetime(6)",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "PedidoLiberacaoExecucaoId",
                table: "SagasAbertura",
                type: "char(36)",
                nullable: true,
                collation: "ascii_general_ci");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "EventoPagamentoConfirmadoId",
                table: "SagasAbertura");

            migrationBuilder.DropColumn(
                name: "ExecucaoLiberadaEm",
                table: "SagasAbertura");

            migrationBuilder.DropColumn(
                name: "PedidoLiberacaoExecucaoId",
                table: "SagasAbertura");
        }
    }
}
