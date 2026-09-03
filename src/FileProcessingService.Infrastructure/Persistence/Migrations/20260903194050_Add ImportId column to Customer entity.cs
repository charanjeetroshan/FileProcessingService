using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FileProcessingService.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddImportIdcolumntoCustomerentity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "ImportId",
                table: "Customers",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ImportId",
                table: "Customers");
        }
    }
}
