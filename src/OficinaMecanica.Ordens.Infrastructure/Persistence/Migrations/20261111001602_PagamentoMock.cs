using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OficinaMecanica.Ordens.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class PagamentoMock : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "PagamentoConfirmadoEm",
                table: "SagasAbertura",
                type: "datetime(6)",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "PagamentoId",
                table: "SagasAbertura",
                type: "char(36)",
                nullable: true,
                collation: "ascii_general_ci");

            migrationBuilder.AddColumn<string>(
                name: "PagamentoProvedorId",
                table: "SagasAbertura",
                type: "varchar(100)",
                maxLength: 100,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<Guid>(
                name: "PedidoPagamentoId",
                table: "SagasAbertura",
                type: "char(36)",
                nullable: true,
                collation: "ascii_general_ci");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PagamentoConfirmadoEm",
                table: "SagasAbertura");

            migrationBuilder.DropColumn(
                name: "PagamentoId",
                table: "SagasAbertura");

            migrationBuilder.DropColumn(
                name: "PagamentoProvedorId",
                table: "SagasAbertura");

            migrationBuilder.DropColumn(
                name: "PedidoPagamentoId",
                table: "SagasAbertura");
        }
    }
}
