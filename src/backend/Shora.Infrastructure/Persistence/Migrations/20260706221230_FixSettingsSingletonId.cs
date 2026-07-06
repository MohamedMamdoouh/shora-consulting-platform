using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Shora.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class FixSettingsSingletonId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Settings");

            migrationBuilder.CreateTable(
                name: "Settings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false),
                    SessionPrice = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: false),
                    SessionDurationMinutes = table.Column<int>(type: "int", nullable: false),
                    BufferMinutes = table.Column<int>(type: "int", nullable: false),
                    ConsultantWhatsAppNumber = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    VodafoneCashNumber = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    InstaPayHandle = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    PaymentInstructions = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    ReceiptUploadWindowMinutes = table.Column<int>(type: "int", nullable: false),
                    CancellationRequestAutoDeclineHours = table.Column<int>(type: "int", nullable: false),
                    ReceiptRetentionMonths = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Settings", x => x.Id);
                    table.CheckConstraint("CK_Settings_Singleton", "[Id] = 1");
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Settings");

            migrationBuilder.CreateTable(
                name: "Settings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SessionPrice = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: false),
                    SessionDurationMinutes = table.Column<int>(type: "int", nullable: false),
                    BufferMinutes = table.Column<int>(type: "int", nullable: false),
                    ConsultantWhatsAppNumber = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    VodafoneCashNumber = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    InstaPayHandle = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    PaymentInstructions = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    ReceiptUploadWindowMinutes = table.Column<int>(type: "int", nullable: false),
                    CancellationRequestAutoDeclineHours = table.Column<int>(type: "int", nullable: false),
                    ReceiptRetentionMonths = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Settings", x => x.Id);
                    table.CheckConstraint("CK_Settings_Singleton", "[Id] = 1");
                });
        }
    }
}
