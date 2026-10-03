using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KassenLager.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddStockLedger : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Devices",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ArticleId = table.Column<int>(type: "INTEGER", nullable: false),
                    CustomerId = table.Column<int>(type: "INTEGER", nullable: false),
                    SerialNumber = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    SerialNumberKey = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    State = table.Column<int>(type: "INTEGER", nullable: false),
                    Note = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: true),
                    IsVoided = table.Column<bool>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    StateChangedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Devices", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Devices_Articles_ArticleId",
                        column: x => x.ArticleId,
                        principalTable: "Articles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Devices_Customers_CustomerId",
                        column: x => x.CustomerId,
                        principalTable: "Customers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "MinimumStocks",
                columns: table => new
                {
                    ArticleId = table.Column<int>(type: "INTEGER", nullable: false),
                    CustomerId = table.Column<int>(type: "INTEGER", nullable: false),
                    Quantity = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MinimumStocks", x => new { x.ArticleId, x.CustomerId });
                    table.ForeignKey(
                        name: "FK_MinimumStocks_Articles_ArticleId",
                        column: x => x.ArticleId,
                        principalTable: "Articles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_MinimumStocks_Customers_CustomerId",
                        column: x => x.CustomerId,
                        principalTable: "Customers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Movements",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Type = table.Column<int>(type: "INTEGER", nullable: false),
                    OccurredAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    RecordedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    CustomerId = table.Column<int>(type: "INTEGER", nullable: false),
                    ArticleId = table.Column<int>(type: "INTEGER", nullable: false),
                    QuantityChange = table.Column<int>(type: "INTEGER", nullable: false),
                    DeviceId = table.Column<int>(type: "INTEGER", nullable: true),
                    FromState = table.Column<int>(type: "INTEGER", nullable: true),
                    ToState = table.Column<int>(type: "INTEGER", nullable: true),
                    Branch = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    Reference = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    Note = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: true),
                    ReversalOfId = table.Column<int>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Movements", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Movements_Articles_ArticleId",
                        column: x => x.ArticleId,
                        principalTable: "Articles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Movements_Customers_CustomerId",
                        column: x => x.CustomerId,
                        principalTable: "Customers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Movements_Devices_DeviceId",
                        column: x => x.DeviceId,
                        principalTable: "Devices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Movements_Movements_ReversalOfId",
                        column: x => x.ReversalOfId,
                        principalTable: "Movements",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "BranchIssues",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    MovementId = table.Column<int>(type: "INTEGER", nullable: false),
                    Kind = table.Column<int>(type: "INTEGER", nullable: false),
                    CustomerDeviceSerialNumber = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    CustomerDeviceModel = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    CustomerDeviceSentOn = table.Column<DateOnly>(type: "TEXT", nullable: true),
                    ReturnMovementId = table.Column<int>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BranchIssues", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BranchIssues_Movements_MovementId",
                        column: x => x.MovementId,
                        principalTable: "Movements",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_BranchIssues_Movements_ReturnMovementId",
                        column: x => x.ReturnMovementId,
                        principalTable: "Movements",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_BranchIssues_MovementId",
                table: "BranchIssues",
                column: "MovementId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_BranchIssues_ReturnMovementId",
                table: "BranchIssues",
                column: "ReturnMovementId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Devices_ArticleId_SerialNumberKey",
                table: "Devices",
                columns: new[] { "ArticleId", "SerialNumberKey" },
                unique: true,
                filter: "\"IsVoided\" = 0");

            migrationBuilder.CreateIndex(
                name: "IX_Devices_CustomerId_State",
                table: "Devices",
                columns: new[] { "CustomerId", "State" });

            migrationBuilder.CreateIndex(
                name: "IX_Devices_SerialNumberKey",
                table: "Devices",
                column: "SerialNumberKey");

            migrationBuilder.CreateIndex(
                name: "IX_MinimumStocks_CustomerId",
                table: "MinimumStocks",
                column: "CustomerId");

            migrationBuilder.CreateIndex(
                name: "IX_Movements_ArticleId_CustomerId",
                table: "Movements",
                columns: new[] { "ArticleId", "CustomerId" });

            migrationBuilder.CreateIndex(
                name: "IX_Movements_CustomerId",
                table: "Movements",
                column: "CustomerId");

            migrationBuilder.CreateIndex(
                name: "IX_Movements_DeviceId",
                table: "Movements",
                column: "DeviceId");

            migrationBuilder.CreateIndex(
                name: "IX_Movements_OccurredAt",
                table: "Movements",
                column: "OccurredAt");

            migrationBuilder.CreateIndex(
                name: "IX_Movements_ReversalOfId",
                table: "Movements",
                column: "ReversalOfId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BranchIssues");

            migrationBuilder.DropTable(
                name: "MinimumStocks");

            migrationBuilder.DropTable(
                name: "Movements");

            migrationBuilder.DropTable(
                name: "Devices");
        }
    }
}
