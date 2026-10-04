using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KassenLager.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddImportLog : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ImportLogs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ImportedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    FileName = table.Column<string>(type: "TEXT", maxLength: 260, nullable: false),
                    Mode = table.Column<int>(type: "INTEGER", nullable: false),
                    NewRows = table.Column<int>(type: "INTEGER", nullable: false),
                    UpdatedRows = table.Column<int>(type: "INTEGER", nullable: false),
                    UnchangedRows = table.Column<int>(type: "INTEGER", nullable: false),
                    ErrorRows = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ImportLogs", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ImportLogs_ImportedAt",
                table: "ImportLogs",
                column: "ImportedAt");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ImportLogs");
        }
    }
}
