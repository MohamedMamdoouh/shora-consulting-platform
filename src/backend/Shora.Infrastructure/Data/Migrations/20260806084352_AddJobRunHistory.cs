using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Shora.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddJobRunHistory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "JobRunHistories",
                columns: table => new
                {
                    JobName = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    LastSuccessAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LastFailureAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LastError = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_JobRunHistories", x => x.JobName);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "JobRunHistories");
        }
    }
}
