using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Shora.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class SyncPendingModelChanges : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "RevokedAt",
                table: "RefreshTokens",
                newName: "RevokedAtUtc");

            migrationBuilder.RenameColumn(
                name: "ExpiresAt",
                table: "RefreshTokens",
                newName: "ExpiresAtUtc");

            migrationBuilder.RenameColumn(
                name: "CreatedAt",
                table: "RefreshTokens",
                newName: "CreatedAtUtc");

            migrationBuilder.RenameColumn(
                name: "StartTime",
                table: "AvailabilitySlots",
                newName: "StartTimeUtc");

            migrationBuilder.RenameColumn(
                name: "EndTime",
                table: "AvailabilitySlots",
                newName: "EndTimeUtc");

            migrationBuilder.RenameIndex(
                name: "IX_AvailabilitySlots_StartTime",
                table: "AvailabilitySlots",
                newName: "IX_AvailabilitySlots_StartTimeUtc");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "RevokedAtUtc",
                table: "RefreshTokens",
                newName: "RevokedAt");

            migrationBuilder.RenameColumn(
                name: "ExpiresAtUtc",
                table: "RefreshTokens",
                newName: "ExpiresAt");

            migrationBuilder.RenameColumn(
                name: "CreatedAtUtc",
                table: "RefreshTokens",
                newName: "CreatedAt");

            migrationBuilder.RenameColumn(
                name: "StartTimeUtc",
                table: "AvailabilitySlots",
                newName: "StartTime");

            migrationBuilder.RenameColumn(
                name: "EndTimeUtc",
                table: "AvailabilitySlots",
                newName: "EndTime");

            migrationBuilder.RenameIndex(
                name: "IX_AvailabilitySlots_StartTimeUtc",
                table: "AvailabilitySlots",
                newName: "IX_AvailabilitySlots_StartTime");
        }
    }
}
