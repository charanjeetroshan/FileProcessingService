using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FileProcessingService.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddconditionaluniquenesstoCompositeHashfield : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ImportJobs_CompositeHash",
                table: "ImportJobs");

            migrationBuilder.CreateIndex(
                name: "IX_ImportJobs_CompositeHash",
                table: "ImportJobs",
                column: "CompositeHash",
                unique: true,
                filter: "[Status] != 'Failed' AND [Status] != 'Cancelled'");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ImportJobs_CompositeHash",
                table: "ImportJobs");

            migrationBuilder.CreateIndex(
                name: "IX_ImportJobs_CompositeHash",
                table: "ImportJobs",
                column: "CompositeHash",
                unique: true);
        }
    }
}
