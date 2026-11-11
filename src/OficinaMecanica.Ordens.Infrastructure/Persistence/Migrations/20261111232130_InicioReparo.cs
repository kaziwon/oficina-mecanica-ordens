using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OficinaMecanica.Ordens.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InicioReparo : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "ReparoIniciadoEm",
                table: "SagasAbertura",
                type: "datetime(6)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ReparoIniciadoEm",
                table: "SagasAbertura");
        }
    }
}
