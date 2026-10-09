using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FileProcessingService.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RenameImportJobentityfieldsaccordingtousecasechanges : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "FileHash",
                table: "ImportJobs");

            migrationBuilder.RenameColumn(
                name: "StoredFileName",
                table: "ImportJobs",
                newName: "UploadedFileName");

            migrationBuilder.RenameColumn(
                name: "OriginalFileName",
                table: "ImportJobs",
                newName: "DataPackageGuid");

            migrationBuilder.AddColumn<string>(
                name: "CompositeHash",
                table: "ImportJobs",
                type: "nvarchar(128)",
                maxLength: 128,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateIndex(
                name: "IX_ImportJobs_CompositeHash",
                table: "ImportJobs",
                column: "CompositeHash",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ImportJobs_CompositeHash",
                table: "ImportJobs");

            migrationBuilder.DropColumn(
                name: "CompositeHash",
                table: "ImportJobs");

            migrationBuilder.RenameColumn(
                name: "UploadedFileName",
                table: "ImportJobs",
                newName: "StoredFileName");

            migrationBuilder.RenameColumn(
                name: "DataPackageGuid",
                table: "ImportJobs",
                newName: "OriginalFileName");

            migrationBuilder.AddColumn<string>(
                name: "FileHash",
                table: "ImportJobs",
                type: "nvarchar(128)",
                maxLength: 128,
                nullable: true);
        }
    }
}
