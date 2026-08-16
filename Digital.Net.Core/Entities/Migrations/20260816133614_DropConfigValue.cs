using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Digital.Net.Core.Entities.Migrations
{
    /// <inheritdoc />
    public partial class DropConfigValue : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ConfigValue",
                schema: "digital_net");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ConfigValue",
                schema: "digital_net",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Name = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    Type = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Value = table.Column<string>(type: "character varying(1048576)", maxLength: 1048576, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ConfigValue", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ConfigValue_Name",
                schema: "digital_net",
                table: "ConfigValue",
                column: "Name",
                unique: true);
        }
    }
}
