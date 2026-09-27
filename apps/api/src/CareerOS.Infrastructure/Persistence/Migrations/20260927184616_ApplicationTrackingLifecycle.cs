using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CareerOS.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ApplicationTrackingLifecycle : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_ApplicationStatusHistory_initial_or_transition",
                table: "application_status_history");

            migrationBuilder.AddCheckConstraint(
                name: "CK_ApplicationStatusHistory_initial_or_transition",
                table: "application_status_history",
                sql: "(\"Sequence\" = 0 AND \"FromStatus\" IS NULL AND \"ToStatus\" IN ('Saved','Applied')) OR (\"Sequence\" > 0 AND \"FromStatus\" IS NOT NULL AND \"FromStatus\" <> \"ToStatus\")");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_ApplicationStatusHistory_initial_or_transition",
                table: "application_status_history");

            migrationBuilder.AddCheckConstraint(
                name: "CK_ApplicationStatusHistory_initial_or_transition",
                table: "application_status_history",
                sql: "(\"Sequence\" = 0 AND \"FromStatus\" IS NULL AND \"ToStatus\" = 'Saved') OR (\"Sequence\" > 0 AND \"FromStatus\" IS NOT NULL AND \"FromStatus\" <> \"ToStatus\")");
        }
    }
}
