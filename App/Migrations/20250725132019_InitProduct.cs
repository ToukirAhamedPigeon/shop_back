using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace server.App.Migrations
{
    /// <inheritdoc />
    public partial class InitProduct : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Products",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    Price = table.Column<decimal>(type: "numeric", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Products", x => x.Id);
                });

            migrationBuilder.InsertData(
                table: "Products",
                columns: new[] { "Id", "CreatedAt", "Name", "Price" },
                values: new object[,]
                {
                    { new Guid("294ac8c0-889f-499e-a04f-cd8789143b9f"), new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "AirPods", 249.99m },
                    { new Guid("61d4c29a-c5d4-4f8d-b4cb-d9ad3c2fc8fb"), new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "MacBook Pro", 1999.99m },
                    { new Guid("9a7b1a43-efc7-46ad-9271-8a11a5f65c99"), new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "iPhone 15", 1299.00m },
                    { new Guid("b67327a5-4b27-4ac2-8c94-4d6c1a0e10ab"), new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Sample Product", 19.99m }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Products");
        }
    }
}
