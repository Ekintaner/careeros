using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CareerOS.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialDomainSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "skills",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    NormalizedName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false, computedColumnSql: "translate(btrim(\"Name\"), 'ABCDEFGHIJKLMNOPQRSTUVWXYZ', 'abcdefghijklmnopqrstuvwxyz')", stored: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_skills", x => x.Id);
                    table.CheckConstraint("CK_Skill_audit_order", "\"UpdatedAtUtc\" >= \"CreatedAtUtc\"");
                    table.CheckConstraint("CK_Skill_name_canonical", "\"Name\" = btrim(\"Name\") AND \"Name\" ~ '^[ -~]+$'");
                    table.CheckConstraint("CK_Skill_Name_not_blank", "length(btrim(\"Name\")) > 0");
                });

            migrationBuilder.CreateTable(
                name: "users",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Email = table.Column<string>(type: "character varying(254)", maxLength: 254, nullable: false),
                    EmailNormalized = table.Column<string>(type: "character varying(254)", maxLength: 254, nullable: false, computedColumnSql: "translate(btrim(\"Email\"), 'ABCDEFGHIJKLMNOPQRSTUVWXYZ', 'abcdefghijklmnopqrstuvwxyz')", stored: true),
                    DisplayName = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_users", x => x.Id);
                    table.CheckConstraint("CK_User_audit_order", "\"UpdatedAtUtc\" >= \"CreatedAtUtc\"");
                    table.CheckConstraint("CK_User_DisplayName_not_blank", "length(btrim(\"DisplayName\")) > 0");
                    table.CheckConstraint("CK_User_Email_not_blank", "length(btrim(\"Email\")) > 0");
                    table.CheckConstraint("CK_User_email_shape", "\"Email\" = btrim(\"Email\") AND \"Email\" ~ '^[!-~]+@[!-~]+$'");
                });

            migrationBuilder.CreateTable(
                name: "career_profiles",
                columns: table => new
                {
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Headline = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Summary = table.Column<string>(type: "character varying(10000)", maxLength: 10000, nullable: true),
                    CurrentLocation = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    YearsOfExperience = table.Column<decimal>(type: "numeric(4,1)", precision: 4, scale: 1, nullable: true),
                    PreferredWorkMode = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_career_profiles", x => x.UserId);
                    table.CheckConstraint("CK_CareerProfile_audit_order", "\"UpdatedAtUtc\" >= \"CreatedAtUtc\"");
                    table.CheckConstraint("CK_CareerProfile_experience_range", "\"YearsOfExperience\" BETWEEN 0 AND 100");
                    table.CheckConstraint("CK_CareerProfile_PreferredWorkMode_values", "\"PreferredWorkMode\" IN ('OnSite', 'Hybrid', 'Remote')");
                    table.ForeignKey(
                        name: "FK_career_profiles_users_UserId",
                        column: x => x.UserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "companies",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Website = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: true),
                    Industry = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    Location = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Description = table.Column<string>(type: "character varying(10000)", maxLength: 10000, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_companies", x => x.Id);
                    table.UniqueConstraint("AK_companies_UserId_Id", x => new { x.UserId, x.Id });
                    table.CheckConstraint("CK_Company_audit_order", "\"UpdatedAtUtc\" >= \"CreatedAtUtc\"");
                    table.CheckConstraint("CK_Company_Name_not_blank", "length(btrim(\"Name\")) > 0");
                    table.CheckConstraint("CK_Company_Website_http", "\"Website\" IS NULL OR \"Website\" ~* '^https?://[^/@[:space:]]+([/?#][^[:space:]]*)?$'");
                    table.ForeignKey(
                        name: "FK_companies_users_UserId",
                        column: x => x.UserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "documents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    OriginalFileName = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    StorageKey = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    ContentType = table.Column<string>(type: "character varying(127)", maxLength: 127, nullable: false),
                    FileSizeBytes = table.Column<long>(type: "bigint", nullable: false),
                    Type = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_documents", x => x.Id);
                    table.UniqueConstraint("AK_documents_UserId_Id_Type", x => new { x.UserId, x.Id, x.Type });
                    table.CheckConstraint("CK_Document_audit_order", "\"UpdatedAtUtc\" >= \"CreatedAtUtc\"");
                    table.CheckConstraint("CK_Document_ContentType_not_blank", "length(btrim(\"ContentType\")) > 0");
                    table.CheckConstraint("CK_Document_file_size", "\"FileSizeBytes\" >= 0");
                    table.CheckConstraint("CK_Document_Name_not_blank", "length(btrim(\"Name\")) > 0");
                    table.CheckConstraint("CK_Document_OriginalFileName_not_blank", "length(btrim(\"OriginalFileName\")) > 0");
                    table.CheckConstraint("CK_Document_StorageKey_not_blank", "length(btrim(\"StorageKey\")) > 0");
                    table.CheckConstraint("CK_Document_Type_values", "\"Type\" IN ('Resume', 'CoverLetter', 'Certificate', 'Portfolio', 'Other')");
                    table.ForeignKey(
                        name: "FK_documents_users_UserId",
                        column: x => x.UserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "notifications",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Type = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Message = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    ReadAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_notifications", x => x.Id);
                    table.CheckConstraint("CK_Notification_audit_order", "\"UpdatedAtUtc\" >= \"CreatedAtUtc\"");
                    table.CheckConstraint("CK_Notification_Message_not_blank", "length(btrim(\"Message\")) > 0");
                    table.CheckConstraint("CK_Notification_read_order", "\"ReadAtUtc\" >= \"CreatedAtUtc\"");
                    table.CheckConstraint("CK_Notification_Title_not_blank", "length(btrim(\"Title\")) > 0");
                    table.CheckConstraint("CK_Notification_Type_not_blank", "length(btrim(\"Type\")) > 0");
                    table.ForeignKey(
                        name: "FK_notifications_users_UserId",
                        column: x => x.UserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "user_skills",
                columns: table => new
                {
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    SkillId = table.Column<Guid>(type: "uuid", nullable: false),
                    Proficiency = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    YearsExperience = table.Column<decimal>(type: "numeric(4,1)", precision: 4, scale: 1, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_user_skills", x => new { x.UserId, x.SkillId });
                    table.CheckConstraint("CK_UserSkill_audit_order", "\"UpdatedAtUtc\" >= \"CreatedAtUtc\"");
                    table.CheckConstraint("CK_UserSkill_experience_range", "\"YearsExperience\" BETWEEN 0 AND 100");
                    table.CheckConstraint("CK_UserSkill_Proficiency_values", "\"Proficiency\" IN ('Beginner', 'Intermediate', 'Advanced', 'Expert')");
                    table.ForeignKey(
                        name: "FK_user_skills_skills_SkillId",
                        column: x => x.SkillId,
                        principalTable: "skills",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_user_skills_users_UserId",
                        column: x => x.UserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "jobs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: false),
                    Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "character varying(50000)", maxLength: 50000, nullable: true),
                    Location = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    WorkMode = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    EmploymentType = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    SeniorityLevel = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    Source = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    SourceUrl = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: true),
                    SalaryMin = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: true),
                    SalaryMax = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: true),
                    SalaryCurrency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: true),
                    SalaryPeriod = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    PostedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ExpiresAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_jobs", x => x.Id);
                    table.UniqueConstraint("AK_jobs_UserId_Id", x => new { x.UserId, x.Id });
                    table.CheckConstraint("CK_Job_audit_order", "\"UpdatedAtUtc\" >= \"CreatedAtUtc\"");
                    table.CheckConstraint("CK_Job_EmploymentType_values", "\"EmploymentType\" IN ('FullTime', 'PartTime', 'Contract', 'Temporary', 'Internship', 'Apprenticeship', 'Other')");
                    table.CheckConstraint("CK_Job_posting_order", "\"ExpiresAtUtc\" >= \"PostedAtUtc\"");
                    table.CheckConstraint("CK_Job_salary_metadata", "((\"SalaryMin\" IS NOT NULL OR \"SalaryMax\" IS NOT NULL) AND \"SalaryCurrency\" IS NOT NULL AND \"SalaryCurrency\" ~ '^[A-Z]{3}$' AND \"SalaryPeriod\" IS NOT NULL) OR (\"SalaryMin\" IS NULL AND \"SalaryMax\" IS NULL AND \"SalaryCurrency\" IS NULL AND \"SalaryPeriod\" IS NULL)");
                    table.CheckConstraint("CK_Job_salary_range", "(\"SalaryMin\" IS NULL OR \"SalaryMin\" >= 0) AND (\"SalaryMax\" IS NULL OR \"SalaryMax\" >= 0) AND (\"SalaryMin\" IS NULL OR \"SalaryMax\" IS NULL OR \"SalaryMin\" <= \"SalaryMax\")");
                    table.CheckConstraint("CK_Job_SalaryPeriod_values", "\"SalaryPeriod\" IN ('Hour', 'Month', 'Year')");
                    table.CheckConstraint("CK_Job_SeniorityLevel_values", "\"SeniorityLevel\" IN ('Internship', 'Entry', 'Associate', 'Mid', 'Senior', 'Lead', 'Executive', 'Other')");
                    table.CheckConstraint("CK_Job_SourceUrl_http", "\"SourceUrl\" IS NULL OR \"SourceUrl\" ~* '^https?://[^/@[:space:]]+([/?#][^[:space:]]*)?$'");
                    table.CheckConstraint("CK_Job_Title_not_blank", "length(btrim(\"Title\")) > 0");
                    table.CheckConstraint("CK_Job_WorkMode_values", "\"WorkMode\" IN ('OnSite', 'Hybrid', 'Remote')");
                    table.ForeignKey(
                        name: "FK_jobs_companies_UserId_CompanyId",
                        columns: x => new { x.UserId, x.CompanyId },
                        principalTable: "companies",
                        principalColumns: new[] { "UserId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_jobs_users_UserId",
                        column: x => x.UserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "resumes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    DocumentId = table.Column<Guid>(type: "uuid", nullable: false),
                    DocumentType = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    IsPrimary = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_resumes", x => x.Id);
                    table.UniqueConstraint("AK_resumes_UserId_Id", x => new { x.UserId, x.Id });
                    table.CheckConstraint("CK_Resume_audit_order", "\"UpdatedAtUtc\" >= \"CreatedAtUtc\"");
                    table.CheckConstraint("CK_Resume_DocumentType_values", "\"DocumentType\" IN ('Resume', 'CoverLetter', 'Certificate', 'Portfolio', 'Other')");
                    table.CheckConstraint("CK_Resume_Name_not_blank", "length(btrim(\"Name\")) > 0");
                    table.CheckConstraint("CK_Resume_resume_document", "\"DocumentType\" = 'Resume'");
                    table.ForeignKey(
                        name: "FK_resumes_documents_UserId_DocumentId_DocumentType",
                        columns: x => new { x.UserId, x.DocumentId, x.DocumentType },
                        principalTable: "documents",
                        principalColumns: new[] { "UserId", "Id", "Type" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_resumes_users_UserId",
                        column: x => x.UserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "job_skills",
                columns: table => new
                {
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    JobId = table.Column<Guid>(type: "uuid", nullable: false),
                    SkillId = table.Column<Guid>(type: "uuid", nullable: false),
                    RequirementLevel = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_job_skills", x => new { x.UserId, x.JobId, x.SkillId });
                    table.CheckConstraint("CK_JobSkill_audit_order", "\"UpdatedAtUtc\" >= \"CreatedAtUtc\"");
                    table.CheckConstraint("CK_JobSkill_RequirementLevel_values", "\"RequirementLevel\" IN ('Required', 'Preferred')");
                    table.ForeignKey(
                        name: "FK_job_skills_jobs_UserId_JobId",
                        columns: x => new { x.UserId, x.JobId },
                        principalTable: "jobs",
                        principalColumns: new[] { "UserId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_job_skills_skills_SkillId",
                        column: x => x.SkillId,
                        principalTable: "skills",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_job_skills_users_UserId",
                        column: x => x.UserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "job_applications",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    JobId = table.Column<Guid>(type: "uuid", nullable: false),
                    ResumeId = table.Column<Guid>(type: "uuid", nullable: true),
                    Status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    StatusRevision = table.Column<int>(type: "integer", nullable: false),
                    LastStatusChangedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    AppliedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Source = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_job_applications", x => x.Id);
                    table.UniqueConstraint("AK_job_applications_UserId_Id", x => new { x.UserId, x.Id });
                    table.CheckConstraint("CK_JobApplication_applied_order", "\"AppliedAtUtc\" <= \"LastStatusChangedAtUtc\"");
                    table.CheckConstraint("CK_JobApplication_applied_required", "\"Status\" NOT IN ('Applied', 'Assessment', 'Interview', 'Offer', 'Rejected', 'Accepted') OR \"AppliedAtUtc\" IS NOT NULL");
                    table.CheckConstraint("CK_JobApplication_audit_order", "\"UpdatedAtUtc\" >= \"CreatedAtUtc\"");
                    table.CheckConstraint("CK_JobApplication_revision", "\"StatusRevision\" >= 0");
                    table.CheckConstraint("CK_JobApplication_Status_values", "\"Status\" IN ('Saved', 'Applied', 'Assessment', 'Interview', 'Offer', 'Rejected', 'Withdrawn', 'Accepted', 'Archived')");
                    table.ForeignKey(
                        name: "FK_job_applications_jobs_UserId_JobId",
                        columns: x => new { x.UserId, x.JobId },
                        principalTable: "jobs",
                        principalColumns: new[] { "UserId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_job_applications_resumes_UserId_ResumeId",
                        columns: x => new { x.UserId, x.ResumeId },
                        principalTable: "resumes",
                        principalColumns: new[] { "UserId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_job_applications_users_UserId",
                        column: x => x.UserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "resume_skills",
                columns: table => new
                {
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    ResumeId = table.Column<Guid>(type: "uuid", nullable: false),
                    SkillId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_resume_skills", x => new { x.UserId, x.ResumeId, x.SkillId });
                    table.ForeignKey(
                        name: "FK_resume_skills_resumes_UserId_ResumeId",
                        columns: x => new { x.UserId, x.ResumeId },
                        principalTable: "resumes",
                        principalColumns: new[] { "UserId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_resume_skills_skills_SkillId",
                        column: x => x.SkillId,
                        principalTable: "skills",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_resume_skills_users_UserId",
                        column: x => x.UserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "application_notes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    ApplicationId = table.Column<Guid>(type: "uuid", nullable: false),
                    Body = table.Column<string>(type: "character varying(20000)", maxLength: 20000, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_application_notes", x => x.Id);
                    table.CheckConstraint("CK_ApplicationNote_audit_order", "\"UpdatedAtUtc\" >= \"CreatedAtUtc\"");
                    table.CheckConstraint("CK_ApplicationNote_Body_not_blank", "length(btrim(\"Body\")) > 0");
                    table.ForeignKey(
                        name: "FK_application_notes_job_applications_UserId_ApplicationId",
                        columns: x => new { x.UserId, x.ApplicationId },
                        principalTable: "job_applications",
                        principalColumns: new[] { "UserId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_application_notes_users_UserId",
                        column: x => x.UserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "application_status_history",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    ApplicationId = table.Column<Guid>(type: "uuid", nullable: false),
                    FromStatus = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    ToStatus = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Sequence = table.Column<int>(type: "integer", nullable: false),
                    ChangedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_application_status_history", x => x.Id);
                    table.CheckConstraint("CK_ApplicationStatusHistory_FromStatus_values", "\"FromStatus\" IN ('Saved', 'Applied', 'Assessment', 'Interview', 'Offer', 'Rejected', 'Withdrawn', 'Accepted', 'Archived')");
                    table.CheckConstraint("CK_ApplicationStatusHistory_initial_or_transition", "(\"Sequence\" = 0 AND \"FromStatus\" IS NULL AND \"ToStatus\" = 'Saved') OR (\"Sequence\" > 0 AND \"FromStatus\" IS NOT NULL AND \"FromStatus\" <> \"ToStatus\")");
                    table.CheckConstraint("CK_ApplicationStatusHistory_ToStatus_values", "\"ToStatus\" IN ('Saved', 'Applied', 'Assessment', 'Interview', 'Offer', 'Rejected', 'Withdrawn', 'Accepted', 'Archived')");
                    table.ForeignKey(
                        name: "FK_application_status_history_job_applications_UserId_Applicat~",
                        columns: x => new { x.UserId, x.ApplicationId },
                        principalTable: "job_applications",
                        principalColumns: new[] { "UserId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_application_status_history_users_UserId",
                        column: x => x.UserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "career_tasks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    ApplicationId = table.Column<Guid>(type: "uuid", nullable: true),
                    Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "character varying(10000)", maxLength: 10000, nullable: true),
                    DueAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CompletedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Priority = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_career_tasks", x => x.Id);
                    table.CheckConstraint("CK_CareerTask_audit_order", "\"UpdatedAtUtc\" >= \"CreatedAtUtc\"");
                    table.CheckConstraint("CK_CareerTask_completion_order", "\"CompletedAtUtc\" >= \"CreatedAtUtc\"");
                    table.CheckConstraint("CK_CareerTask_Priority_values", "\"Priority\" IN ('Low', 'Normal', 'High')");
                    table.CheckConstraint("CK_CareerTask_Title_not_blank", "length(btrim(\"Title\")) > 0");
                    table.ForeignKey(
                        name: "FK_career_tasks_job_applications_UserId_ApplicationId",
                        columns: x => new { x.UserId, x.ApplicationId },
                        principalTable: "job_applications",
                        principalColumns: new[] { "UserId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_career_tasks_users_UserId",
                        column: x => x.UserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "interviews",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    ApplicationId = table.Column<Guid>(type: "uuid", nullable: false),
                    Type = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    ScheduledStartUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ScheduledEndUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Location = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    MeetingUrl = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: true),
                    Outcome = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_interviews", x => x.Id);
                    table.UniqueConstraint("AK_interviews_UserId_Id", x => new { x.UserId, x.Id });
                    table.CheckConstraint("CK_Interview_audit_order", "\"UpdatedAtUtc\" >= \"CreatedAtUtc\"");
                    table.CheckConstraint("CK_Interview_MeetingUrl_http", "\"MeetingUrl\" IS NULL OR \"MeetingUrl\" ~* '^https?://[^/@[:space:]]+([/?#][^[:space:]]*)?$'");
                    table.CheckConstraint("CK_Interview_schedule_order", "\"ScheduledEndUtc\" > \"ScheduledStartUtc\"");
                    table.CheckConstraint("CK_Interview_Type_values", "\"Type\" IN ('Recruiter', 'HR', 'Technical', 'Behavioral', 'HiringManager', 'CaseStudy', 'Final', 'Other')");
                    table.ForeignKey(
                        name: "FK_interviews_job_applications_UserId_ApplicationId",
                        columns: x => new { x.UserId, x.ApplicationId },
                        principalTable: "job_applications",
                        principalColumns: new[] { "UserId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_interviews_users_UserId",
                        column: x => x.UserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "interview_notes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    InterviewId = table.Column<Guid>(type: "uuid", nullable: false),
                    Body = table.Column<string>(type: "character varying(20000)", maxLength: 20000, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_interview_notes", x => x.Id);
                    table.CheckConstraint("CK_InterviewNote_audit_order", "\"UpdatedAtUtc\" >= \"CreatedAtUtc\"");
                    table.CheckConstraint("CK_InterviewNote_Body_not_blank", "length(btrim(\"Body\")) > 0");
                    table.ForeignKey(
                        name: "FK_interview_notes_interviews_UserId_InterviewId",
                        columns: x => new { x.UserId, x.InterviewId },
                        principalTable: "interviews",
                        principalColumns: new[] { "UserId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_interview_notes_users_UserId",
                        column: x => x.UserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_application_notes_UserId_ApplicationId_CreatedAtUtc",
                table: "application_notes",
                columns: new[] { "UserId", "ApplicationId", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_application_status_history_ApplicationId_ChangedAtUtc_Seque~",
                table: "application_status_history",
                columns: new[] { "ApplicationId", "ChangedAtUtc", "Sequence" });

            migrationBuilder.CreateIndex(
                name: "IX_application_status_history_ApplicationId_Sequence",
                table: "application_status_history",
                columns: new[] { "ApplicationId", "Sequence" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_application_status_history_UserId_ApplicationId",
                table: "application_status_history",
                columns: new[] { "UserId", "ApplicationId" });

            migrationBuilder.CreateIndex(
                name: "IX_career_tasks_UserId_ApplicationId",
                table: "career_tasks",
                columns: new[] { "UserId", "ApplicationId" });

            migrationBuilder.CreateIndex(
                name: "IX_career_tasks_UserId_CompletedAtUtc",
                table: "career_tasks",
                columns: new[] { "UserId", "CompletedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_career_tasks_UserId_DueAtUtc",
                table: "career_tasks",
                columns: new[] { "UserId", "DueAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_companies_UserId_Name",
                table: "companies",
                columns: new[] { "UserId", "Name" });

            migrationBuilder.CreateIndex(
                name: "IX_documents_StorageKey",
                table: "documents",
                column: "StorageKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_documents_UserId_CreatedAtUtc",
                table: "documents",
                columns: new[] { "UserId", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_interview_notes_UserId_InterviewId_CreatedAtUtc",
                table: "interview_notes",
                columns: new[] { "UserId", "InterviewId", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_interviews_UserId_ApplicationId",
                table: "interviews",
                columns: new[] { "UserId", "ApplicationId" });

            migrationBuilder.CreateIndex(
                name: "IX_interviews_UserId_ScheduledStartUtc",
                table: "interviews",
                columns: new[] { "UserId", "ScheduledStartUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_job_applications_UserId_CreatedAtUtc",
                table: "job_applications",
                columns: new[] { "UserId", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_job_applications_UserId_JobId",
                table: "job_applications",
                columns: new[] { "UserId", "JobId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_job_applications_UserId_ResumeId",
                table: "job_applications",
                columns: new[] { "UserId", "ResumeId" });

            migrationBuilder.CreateIndex(
                name: "IX_job_applications_UserId_Status_CreatedAtUtc",
                table: "job_applications",
                columns: new[] { "UserId", "Status", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_job_skills_SkillId",
                table: "job_skills",
                column: "SkillId");

            migrationBuilder.CreateIndex(
                name: "IX_jobs_UserId_CompanyId",
                table: "jobs",
                columns: new[] { "UserId", "CompanyId" });

            migrationBuilder.CreateIndex(
                name: "IX_jobs_UserId_CreatedAtUtc",
                table: "jobs",
                columns: new[] { "UserId", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_jobs_UserId_PostedAtUtc",
                table: "jobs",
                columns: new[] { "UserId", "PostedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_notifications_unread",
                table: "notifications",
                columns: new[] { "UserId", "CreatedAtUtc" },
                filter: "\"ReadAtUtc\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_notifications_UserId_CreatedAtUtc",
                table: "notifications",
                columns: new[] { "UserId", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_resume_skills_SkillId",
                table: "resume_skills",
                column: "SkillId");

            migrationBuilder.CreateIndex(
                name: "IX_resumes_DocumentId",
                table: "resumes",
                column: "DocumentId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_resumes_UserId",
                table: "resumes",
                column: "UserId",
                unique: true,
                filter: "\"IsPrimary\" = TRUE");

            migrationBuilder.CreateIndex(
                name: "IX_resumes_UserId_CreatedAtUtc",
                table: "resumes",
                columns: new[] { "UserId", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_resumes_UserId_DocumentId_DocumentType",
                table: "resumes",
                columns: new[] { "UserId", "DocumentId", "DocumentType" });

            migrationBuilder.CreateIndex(
                name: "IX_skills_NormalizedName",
                table: "skills",
                column: "NormalizedName",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_user_skills_SkillId",
                table: "user_skills",
                column: "SkillId");

            migrationBuilder.CreateIndex(
                name: "IX_users_EmailNormalized",
                table: "users",
                column: "EmailNormalized",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "application_notes");

            migrationBuilder.DropTable(
                name: "application_status_history");

            migrationBuilder.DropTable(
                name: "career_profiles");

            migrationBuilder.DropTable(
                name: "career_tasks");

            migrationBuilder.DropTable(
                name: "interview_notes");

            migrationBuilder.DropTable(
                name: "job_skills");

            migrationBuilder.DropTable(
                name: "notifications");

            migrationBuilder.DropTable(
                name: "resume_skills");

            migrationBuilder.DropTable(
                name: "user_skills");

            migrationBuilder.DropTable(
                name: "interviews");

            migrationBuilder.DropTable(
                name: "skills");

            migrationBuilder.DropTable(
                name: "job_applications");

            migrationBuilder.DropTable(
                name: "jobs");

            migrationBuilder.DropTable(
                name: "resumes");

            migrationBuilder.DropTable(
                name: "companies");

            migrationBuilder.DropTable(
                name: "documents");

            migrationBuilder.DropTable(
                name: "users");
        }
    }
}
