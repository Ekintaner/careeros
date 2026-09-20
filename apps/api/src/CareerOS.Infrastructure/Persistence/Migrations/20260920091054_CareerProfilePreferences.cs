using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CareerOS.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class CareerProfilePreferences : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string[]>(
                name: "EmploymentTypes",
                table: "career_profiles",
                type: "character varying(32)[]",
                nullable: false,
                defaultValueSql: "'{}'");

            migrationBuilder.AddColumn<string[]>(
                name: "PreferredLocations",
                table: "career_profiles",
                type: "character varying(200)[]",
                nullable: false,
                defaultValueSql: "'{}'");

            migrationBuilder.AddColumn<string[]>(
                name: "SeniorityLevels",
                table: "career_profiles",
                type: "character varying(32)[]",
                nullable: false,
                defaultValueSql: "'{}'");

            migrationBuilder.AddColumn<string[]>(
                name: "TargetRoles",
                table: "career_profiles",
                type: "character varying(100)[]",
                nullable: false,
                defaultValueSql: "'{}'");

            migrationBuilder.CreateIndex(
                name: "IX_skills_NormalizedName_prefix",
                table: "skills",
                column: "NormalizedName")
                .Annotation("Npgsql:IndexOperators", new[] { "varchar_pattern_ops" });

            migrationBuilder.AddCheckConstraint(
                name: "CK_CareerProfile_EmploymentTypes_bounds",
                table: "career_profiles",
                sql: "cardinality(\"EmploymentTypes\") <= 7 AND array_position(\"EmploymentTypes\", NULL) IS NULL AND array_position(\"EmploymentTypes\", '') IS NULL AND (array_ndims(\"EmploymentTypes\") IS NULL OR array_ndims(\"EmploymentTypes\") = 1)");

            migrationBuilder.AddCheckConstraint(
                name: "CK_CareerProfile_EmploymentTypes_values",
                table: "career_profiles",
                sql: "\"EmploymentTypes\"::text[] <@ ARRAY['FullTime','PartTime','Contract','Temporary','Internship','Apprenticeship','Other']::text[]");

            migrationBuilder.AddCheckConstraint(
                name: "CK_CareerProfile_PreferredLocations_bounds",
                table: "career_profiles",
                sql: "cardinality(\"PreferredLocations\") <= 10 AND array_position(\"PreferredLocations\", NULL) IS NULL AND array_position(\"PreferredLocations\", '') IS NULL AND (array_ndims(\"PreferredLocations\") IS NULL OR array_ndims(\"PreferredLocations\") = 1)");

            migrationBuilder.AddCheckConstraint(
                name: "CK_CareerProfile_SeniorityLevels_bounds",
                table: "career_profiles",
                sql: "cardinality(\"SeniorityLevels\") <= 8 AND array_position(\"SeniorityLevels\", NULL) IS NULL AND array_position(\"SeniorityLevels\", '') IS NULL AND (array_ndims(\"SeniorityLevels\") IS NULL OR array_ndims(\"SeniorityLevels\") = 1)");

            migrationBuilder.AddCheckConstraint(
                name: "CK_CareerProfile_SeniorityLevels_values",
                table: "career_profiles",
                sql: "\"SeniorityLevels\"::text[] <@ ARRAY['Internship','Entry','Associate','Mid','Senior','Lead','Executive','Other']::text[]");

            migrationBuilder.AddCheckConstraint(
                name: "CK_CareerProfile_TargetRoles_bounds",
                table: "career_profiles",
                sql: "cardinality(\"TargetRoles\") <= 10 AND array_position(\"TargetRoles\", NULL) IS NULL AND array_position(\"TargetRoles\", '') IS NULL AND (array_ndims(\"TargetRoles\") IS NULL OR array_ndims(\"TargetRoles\") = 1)");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_skills_NormalizedName_prefix",
                table: "skills");

            migrationBuilder.DropCheckConstraint(
                name: "CK_CareerProfile_EmploymentTypes_bounds",
                table: "career_profiles");

            migrationBuilder.DropCheckConstraint(
                name: "CK_CareerProfile_EmploymentTypes_values",
                table: "career_profiles");

            migrationBuilder.DropCheckConstraint(
                name: "CK_CareerProfile_PreferredLocations_bounds",
                table: "career_profiles");

            migrationBuilder.DropCheckConstraint(
                name: "CK_CareerProfile_SeniorityLevels_bounds",
                table: "career_profiles");

            migrationBuilder.DropCheckConstraint(
                name: "CK_CareerProfile_SeniorityLevels_values",
                table: "career_profiles");

            migrationBuilder.DropCheckConstraint(
                name: "CK_CareerProfile_TargetRoles_bounds",
                table: "career_profiles");

            migrationBuilder.DropColumn(
                name: "EmploymentTypes",
                table: "career_profiles");

            migrationBuilder.DropColumn(
                name: "PreferredLocations",
                table: "career_profiles");

            migrationBuilder.DropColumn(
                name: "SeniorityLevels",
                table: "career_profiles");

            migrationBuilder.DropColumn(
                name: "TargetRoles",
                table: "career_profiles");
        }
    }
}
