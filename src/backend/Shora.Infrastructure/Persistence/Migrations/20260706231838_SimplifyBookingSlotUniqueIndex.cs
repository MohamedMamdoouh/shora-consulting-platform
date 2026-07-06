using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Shora.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class SimplifyBookingSlotUniqueIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Bookings_AvailabilitySlotId",
                table: "Bookings");

            migrationBuilder.CreateIndex(
                name: "IX_Bookings_AvailabilitySlotId",
                table: "Bookings",
                column: "AvailabilitySlotId",
                unique: true,
                filter: "[AvailabilitySlotId] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Bookings_AvailabilitySlotId",
                table: "Bookings");

            migrationBuilder.CreateIndex(
                name: "IX_Bookings_AvailabilitySlotId",
                table: "Bookings",
                column: "AvailabilitySlotId",
                unique: true,
                filter: "[AvailabilitySlotId] IS NOT NULL AND [Status] IN ('PendingPayment', 'PendingApproval', 'Confirmed', 'CancellationRequested', 'Completed')");
        }
    }
}
