using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Shora.Infrastructure.Data;

#nullable disable

namespace Shora.Infrastructure.Migrations
{
    [DbContext(typeof(ApplicationDbContext))]
    [Migration("20260814000000_RemovePaymentRevocationAudit")]
    /// <inheritdoc />
    public partial class RemovePaymentRevocationAudit : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Payments_Users_RefundRevokedByAdminId",
                table: "Payments");

            migrationBuilder.DropIndex(
                name: "IX_Payments_RefundRevokedByAdminId",
                table: "Payments");

            migrationBuilder.DropColumn(
                name: "RefundRevocationReason",
                table: "Payments");

            migrationBuilder.DropColumn(
                name: "RefundRevokedAtUtc",
                table: "Payments");

            migrationBuilder.DropColumn(
                name: "RefundRevokedByAdminId",
                table: "Payments");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "RefundRevocationReason",
                table: "Payments",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "RefundRevokedAtUtc",
                table: "Payments",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "RefundRevokedByAdminId",
                table: "Payments",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Payments_RefundRevokedByAdminId",
                table: "Payments",
                column: "RefundRevokedByAdminId");

            migrationBuilder.AddForeignKey(
                name: "FK_Payments_Users_RefundRevokedByAdminId",
                table: "Payments",
                column: "RefundRevokedByAdminId",
                principalTable: "Users",
                principalColumn: "Id");
        }
    }
}
