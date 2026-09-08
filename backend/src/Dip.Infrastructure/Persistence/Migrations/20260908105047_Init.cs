using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Dip.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Init : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AuditLogs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: false),
                    EntityName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    EntityId = table.Column<Guid>(type: "uuid", nullable: false),
                    Action = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Field = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    OldValue = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    NewValue = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    UserId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    At = table.Column<DateTime>(type: "timestamp without time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AuditLogs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Projects",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Client = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Organisation = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Approver = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    CostCenter = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    ScheduleMode = table.Column<int>(type: "integer", nullable: false),
                    WorkingPlanApprovalDays = table.Column<int>(type: "integer", nullable: false),
                    ReportDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    BaselineStartDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    WeightPending = table.Column<decimal>(type: "numeric(6,4)", precision: 6, scale: 4, nullable: false),
                    WeightSub1 = table.Column<decimal>(type: "numeric(6,4)", precision: 6, scale: 4, nullable: false),
                    WeightSub2 = table.Column<decimal>(type: "numeric(6,4)", precision: 6, scale: 4, nullable: false),
                    WeightApproved = table.Column<decimal>(type: "numeric(6,4)", precision: 6, scale: 4, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Projects", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PromoteBatches",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: false),
                    FolderId = table.Column<Guid>(type: "uuid", nullable: false),
                    FolderFileId = table.Column<Guid>(type: "uuid", nullable: false),
                    At = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    By = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Added = table.Column<int>(type: "integer", nullable: false),
                    Updated = table.Column<int>(type: "integer", nullable: false),
                    Deleted = table.Column<int>(type: "integer", nullable: false),
                    Skipped = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PromoteBatches", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Roles",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    NormalizedName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    ConcurrencyStamp = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Roles", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "TidpDrafts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: false),
                    DisciplineId = table.Column<Guid>(type: "uuid", nullable: false),
                    FolderFileId = table.Column<Guid>(type: "uuid", nullable: false),
                    ImportBatchId = table.Column<Guid>(type: "uuid", nullable: false),
                    DocumentReference = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    RevisionNumber = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    DateCreated = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    DateLastUpdated = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    SourceFileName = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    State = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    UpdatedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    RowVersion = table.Column<byte[]>(type: "bytea", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TidpDrafts", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Users",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    FullName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    UserName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    NormalizedUserName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    Email = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    NormalizedEmail = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    EmailConfirmed = table.Column<bool>(type: "boolean", nullable: false),
                    PasswordHash = table.Column<string>(type: "text", nullable: true),
                    SecurityStamp = table.Column<string>(type: "text", nullable: true),
                    ConcurrencyStamp = table.Column<string>(type: "text", nullable: true),
                    PhoneNumber = table.Column<string>(type: "text", nullable: true),
                    PhoneNumberConfirmed = table.Column<bool>(type: "boolean", nullable: false),
                    TwoFactorEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    LockoutEnd = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    LockoutEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    AccessFailedCount = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Users", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AconexRevisions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: false),
                    ImportBatchId = table.Column<Guid>(type: "uuid", nullable: false),
                    FileType = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    FileName = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    AconexDocNo = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    DocNoFinal = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Revision = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    Title = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    AconexStatus = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    ReviewStatus = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    DateModified = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    Type = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Discipline = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Area = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Venue = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    FloorLevel = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    TransmittalIn = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    IsTerminated = table.Column<bool>(type: "boolean", nullable: false),
                    IsLatest = table.Column<bool>(type: "boolean", nullable: false),
                    InMidp = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AconexRevisions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AconexRevisions_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "Projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "BaselineActivities",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: false),
                    ActivityCode = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Package = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    WbsLevel1 = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    WbsLevel2 = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    WbsLevel3 = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    WbsLevel4 = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    WbsLevel5 = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    WbsLevel6 = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    WbsLevel7 = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    Type = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    OriginalDuration = table.Column<int>(type: "integer", nullable: false),
                    Start = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    Finish = table.Column<DateTime>(type: "timestamp without time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BaselineActivities", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BaselineActivities_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "Projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Disciplines",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: false),
                    Code = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    CorporateName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Disciplines", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Disciplines_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "Projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "DocumentSnapshots",
                columns: table => new
                {
                    DocumentId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: false),
                    Layer = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    FolderFileId = table.Column<Guid>(type: "uuid", nullable: true),
                    ComputedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    DocumentNumber = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Title = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    Type = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Discipline = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Building = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Level = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    Trade = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Author = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    DeliveryMilestone = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    ActivityId = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    PackageName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    SubmissionsCount = table.Column<int>(type: "integer", nullable: true),
                    Revision = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    AconexStatus = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    SubmissionDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    DateModified = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    Transmittal = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    PlannedStart = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    PlannedFinish = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    ActualStart = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    ActualFinish = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DocumentSnapshots", x => x.DocumentId);
                    table.ForeignKey(
                        name: "FK_DocumentSnapshots_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "Projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PicklistItems",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: false),
                    Field = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    Code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Description = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PicklistItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PicklistItems_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "Projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "StatusMappings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: false),
                    AconexStatus = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    IsLegacy = table.Column<bool>(type: "boolean", nullable: false),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StatusMappings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_StatusMappings_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "Projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "RoleClaims",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    RoleId = table.Column<Guid>(type: "uuid", nullable: false),
                    ClaimType = table.Column<string>(type: "text", nullable: true),
                    ClaimValue = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RoleClaims", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RoleClaims_Roles_RoleId",
                        column: x => x.RoleId,
                        principalTable: "Roles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "DocumentDrafts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: false),
                    TidpDraftId = table.Column<Guid>(type: "uuid", nullable: false),
                    DisciplineId = table.Column<Guid>(type: "uuid", nullable: false),
                    FolderFileId = table.Column<Guid>(type: "uuid", nullable: false),
                    ImportBatchId = table.Column<Guid>(type: "uuid", nullable: false),
                    DocumentNumber = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Title = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    ExtractedFromModel = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    ScopeArea = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    AuthoringSoftware = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    ExchangeFormat = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Scale = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    DeliveryMilestone = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    PackageName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    ActivityId = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    ClassificationCode = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    F01Project = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    F02Originator = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    F03Contract = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    F04DocType = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    F05Discipline = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    F06Zone = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    F07Building = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    F08ADrawingType = table.Column<string>(type: "character varying(5)", maxLength: 5, nullable: false),
                    F08BLevel = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    F08CSequence = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    CorporateDiscipline = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    BudgetWeight = table.Column<decimal>(type: "numeric(10,4)", precision: 10, scale: 4, nullable: false),
                    State = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    LiveDocumentId = table.Column<Guid>(type: "uuid", nullable: true),
                    ConflictReason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    IsDuplicate = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    UpdatedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    RowVersion = table.Column<byte[]>(type: "bytea", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DocumentDrafts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DocumentDrafts_TidpDrafts_TidpDraftId",
                        column: x => x.TidpDraftId,
                        principalTable: "TidpDrafts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "RefreshTokens",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Token = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    CreatedByIp = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    RevokedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    RevokedByIp = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    ReplacedByToken = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RefreshTokens", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RefreshTokens_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "UserClaims",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    ClaimType = table.Column<string>(type: "text", nullable: true),
                    ClaimValue = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserClaims", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UserClaims_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "UserLogins",
                columns: table => new
                {
                    LoginProvider = table.Column<string>(type: "text", nullable: false),
                    ProviderKey = table.Column<string>(type: "text", nullable: false),
                    ProviderDisplayName = table.Column<string>(type: "text", nullable: true),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserLogins", x => new { x.LoginProvider, x.ProviderKey });
                    table.ForeignKey(
                        name: "FK_UserLogins_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "UserRoles",
                columns: table => new
                {
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    RoleId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserRoles", x => new { x.UserId, x.RoleId });
                    table.ForeignKey(
                        name: "FK_UserRoles_Roles_RoleId",
                        column: x => x.RoleId,
                        principalTable: "Roles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_UserRoles_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "UserTokens",
                columns: table => new
                {
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    LoginProvider = table.Column<string>(type: "text", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    Value = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserTokens", x => new { x.UserId, x.LoginProvider, x.Name });
                    table.ForeignKey(
                        name: "FK_UserTokens_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Folders",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: false),
                    ParentId = table.Column<Guid>(type: "uuid", nullable: true),
                    Name = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    Path = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    DriveFolderId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Target = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    LastSyncedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    IsCompany = table.Column<bool>(type: "boolean", nullable: false),
                    AuthorId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Folders", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Folders_Folders_ParentId",
                        column: x => x.ParentId,
                        principalTable: "Folders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Folders_PicklistItems_AuthorId",
                        column: x => x.AuthorId,
                        principalTable: "PicklistItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Folders_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "Projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "DataExchangeDrafts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    DocumentDraftId = table.Column<Guid>(type: "uuid", nullable: false),
                    Number = table.Column<int>(type: "integer", nullable: false),
                    Stage = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    ProgrammeRef = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    Author = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Geometrical = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    NonGeometrical = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    DurationDays = table.Column<int>(type: "integer", nullable: true),
                    Predecessor = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    ExchangeDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DataExchangeDrafts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DataExchangeDrafts_DocumentDrafts_DocumentDraftId",
                        column: x => x.DocumentDraftId,
                        principalTable: "DocumentDrafts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "FolderFiles",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    FolderId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    Kind = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    DriveFileId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    DriveModifiedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    ContentSource = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    ContentModifiedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    ContentMd5 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    SizeBytes = table.Column<long>(type: "bigint", nullable: false),
                    State = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    ImportError = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    LastImportBatchId = table.Column<Guid>(type: "uuid", nullable: true),
                    LastImportedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    NameLower = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true, computedColumnSql: "lower(\"Name\")", stored: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FolderFiles", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FolderFiles_Folders_FolderId",
                        column: x => x.FolderId,
                        principalTable: "Folders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "FileBlobs",
                columns: table => new
                {
                    FolderFileId = table.Column<Guid>(type: "uuid", nullable: false),
                    Content = table.Column<byte[]>(type: "bytea", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FileBlobs", x => x.FolderFileId);
                    table.ForeignKey(
                        name: "FK_FileBlobs_FolderFiles_FolderFileId",
                        column: x => x.FolderFileId,
                        principalTable: "FolderFiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ImportBatches",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: false),
                    Kind = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Target = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    FolderFileId = table.Column<Guid>(type: "uuid", nullable: true),
                    FileName = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    ImportedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    ImportedBy = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    RowsRead = table.Column<int>(type: "integer", nullable: false),
                    RowsInserted = table.Column<int>(type: "integer", nullable: false),
                    RowsUpdated = table.Column<int>(type: "integer", nullable: false),
                    RowsSkipped = table.Column<int>(type: "integer", nullable: false),
                    Log = table.Column<string>(type: "jsonb", nullable: true),
                    Completed = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ImportBatches", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ImportBatches_FolderFiles_FolderFileId",
                        column: x => x.FolderFileId,
                        principalTable: "FolderFiles",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_ImportBatches_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "Projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Tidps",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: false),
                    DisciplineId = table.Column<Guid>(type: "uuid", nullable: false),
                    FolderFileId = table.Column<Guid>(type: "uuid", nullable: true),
                    DocumentReference = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    RevisionNumber = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    DateCreated = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    DateLastUpdated = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    SourceFileName = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    UpdatedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    RowVersion = table.Column<byte[]>(type: "bytea", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Tidps", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Tidps_Disciplines_DisciplineId",
                        column: x => x.DisciplineId,
                        principalTable: "Disciplines",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Tidps_FolderFiles_FolderFileId",
                        column: x => x.FolderFileId,
                        principalTable: "FolderFiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_Tidps_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "Projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Documents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: false),
                    TidpId = table.Column<Guid>(type: "uuid", nullable: false),
                    DisciplineId = table.Column<Guid>(type: "uuid", nullable: false),
                    FolderFileId = table.Column<Guid>(type: "uuid", nullable: true),
                    DocumentNumber = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Title = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    ExtractedFromModel = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    ScopeArea = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    AuthoringSoftware = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    ExchangeFormat = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Scale = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    DeliveryMilestone = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    PackageName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    ActivityId = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    ClassificationCode = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    F01Project = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    F02Originator = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    F03Contract = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    F04DocType = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    F05Discipline = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    F06Zone = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    F07Building = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    F08ADrawingType = table.Column<string>(type: "character varying(5)", maxLength: 5, nullable: false),
                    F08BLevel = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    F08CSequence = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    CorporateDiscipline = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    BudgetWeight = table.Column<decimal>(type: "numeric(10,4)", precision: 10, scale: 4, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    UpdatedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    RowVersion = table.Column<byte[]>(type: "bytea", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Documents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Documents_Disciplines_DisciplineId",
                        column: x => x.DisciplineId,
                        principalTable: "Disciplines",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Documents_FolderFiles_FolderFileId",
                        column: x => x.FolderFileId,
                        principalTable: "FolderFiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_Documents_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "Projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Documents_Tidps_TidpId",
                        column: x => x.TidpId,
                        principalTable: "Tidps",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "DataExchanges",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    DocumentId = table.Column<Guid>(type: "uuid", nullable: false),
                    Number = table.Column<int>(type: "integer", nullable: false),
                    Stage = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    ProgrammeRef = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    Author = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Geometrical = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    NonGeometrical = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    DurationDays = table.Column<int>(type: "integer", nullable: true),
                    Predecessor = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    ExchangeDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DataExchanges", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DataExchanges_Documents_DocumentId",
                        column: x => x.DocumentId,
                        principalTable: "Documents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AconexRevisions_ProjectId_AconexDocNo_Revision_DateModified",
                table: "AconexRevisions",
                columns: new[] { "ProjectId", "AconexDocNo", "Revision", "DateModified" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AconexRevisions_ProjectId_DocNoFinal",
                table: "AconexRevisions",
                columns: new[] { "ProjectId", "DocNoFinal" });

            migrationBuilder.CreateIndex(
                name: "IX_AconexRevisions_ProjectId_DocNoFinal_IsLatest",
                table: "AconexRevisions",
                columns: new[] { "ProjectId", "DocNoFinal", "IsLatest" });

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogs_At",
                table: "AuditLogs",
                column: "At");

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogs_ProjectId_EntityName_EntityId",
                table: "AuditLogs",
                columns: new[] { "ProjectId", "EntityName", "EntityId" });

            migrationBuilder.CreateIndex(
                name: "IX_BaselineActivities_ProjectId_ActivityCode",
                table: "BaselineActivities",
                columns: new[] { "ProjectId", "ActivityCode" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_BaselineActivities_ProjectId_Package_Type",
                table: "BaselineActivities",
                columns: new[] { "ProjectId", "Package", "Type" });

            migrationBuilder.CreateIndex(
                name: "IX_DataExchangeDrafts_DocumentDraftId_Number",
                table: "DataExchangeDrafts",
                columns: new[] { "DocumentDraftId", "Number" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DataExchanges_DocumentId_Number",
                table: "DataExchanges",
                columns: new[] { "DocumentId", "Number" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Disciplines_ProjectId_Code",
                table: "Disciplines",
                columns: new[] { "ProjectId", "Code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DocumentDrafts_ImportBatchId",
                table: "DocumentDrafts",
                column: "ImportBatchId");

            migrationBuilder.CreateIndex(
                name: "IX_DocumentDrafts_LiveDocumentId",
                table: "DocumentDrafts",
                column: "LiveDocumentId");

            migrationBuilder.CreateIndex(
                name: "IX_DocumentDrafts_ProjectId_FolderFileId_DocumentNumber",
                table: "DocumentDrafts",
                columns: new[] { "ProjectId", "FolderFileId", "DocumentNumber" });

            migrationBuilder.CreateIndex(
                name: "IX_DocumentDrafts_TidpDraftId",
                table: "DocumentDrafts",
                column: "TidpDraftId");

            migrationBuilder.CreateIndex(
                name: "IX_Documents_DisciplineId",
                table: "Documents",
                column: "DisciplineId");

            migrationBuilder.CreateIndex(
                name: "IX_Documents_FolderFileId",
                table: "Documents",
                column: "FolderFileId");

            migrationBuilder.CreateIndex(
                name: "IX_Documents_ProjectId_ActivityId",
                table: "Documents",
                columns: new[] { "ProjectId", "ActivityId" });

            migrationBuilder.CreateIndex(
                name: "IX_Documents_ProjectId_CorporateDiscipline",
                table: "Documents",
                columns: new[] { "ProjectId", "CorporateDiscipline" });

            migrationBuilder.CreateIndex(
                name: "IX_Documents_ProjectId_DocumentNumber",
                table: "Documents",
                columns: new[] { "ProjectId", "DocumentNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Documents_TidpId",
                table: "Documents",
                column: "TidpId");

            migrationBuilder.CreateIndex(
                name: "IX_DocumentSnapshots_FolderFileId",
                table: "DocumentSnapshots",
                column: "FolderFileId");

            migrationBuilder.CreateIndex(
                name: "IX_DocumentSnapshots_ProjectId_Discipline",
                table: "DocumentSnapshots",
                columns: new[] { "ProjectId", "Discipline" });

            migrationBuilder.CreateIndex(
                name: "IX_DocumentSnapshots_ProjectId_DocumentNumber",
                table: "DocumentSnapshots",
                columns: new[] { "ProjectId", "DocumentNumber" });

            migrationBuilder.CreateIndex(
                name: "IX_DocumentSnapshots_ProjectId_Layer",
                table: "DocumentSnapshots",
                columns: new[] { "ProjectId", "Layer" });

            migrationBuilder.CreateIndex(
                name: "IX_DocumentSnapshots_ProjectId_Status",
                table: "DocumentSnapshots",
                columns: new[] { "ProjectId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_FolderFiles_DriveFileId",
                table: "FolderFiles",
                column: "DriveFileId");

            migrationBuilder.CreateIndex(
                name: "IX_FolderFiles_FolderId_NameLower_Active",
                table: "FolderFiles",
                columns: new[] { "FolderId", "NameLower" },
                unique: true,
                filter: "NOT \"IsDeleted\"");

            migrationBuilder.CreateIndex(
                name: "IX_Folders_AuthorId",
                table: "Folders",
                column: "AuthorId");

            migrationBuilder.CreateIndex(
                name: "IX_Folders_DriveFolderId",
                table: "Folders",
                column: "DriveFolderId");

            migrationBuilder.CreateIndex(
                name: "IX_Folders_ParentId",
                table: "Folders",
                column: "ParentId");

            migrationBuilder.CreateIndex(
                name: "IX_Folders_ProjectId_Path",
                table: "Folders",
                columns: new[] { "ProjectId", "Path" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ImportBatches_FolderFileId",
                table: "ImportBatches",
                column: "FolderFileId");

            migrationBuilder.CreateIndex(
                name: "IX_ImportBatches_ProjectId_ImportedAt",
                table: "ImportBatches",
                columns: new[] { "ProjectId", "ImportedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_PicklistItems_ProjectId_Field_Code",
                table: "PicklistItems",
                columns: new[] { "ProjectId", "Field", "Code" },
                unique: true,
                filter: "NOT \"IsDeleted\"");

            migrationBuilder.CreateIndex(
                name: "IX_Projects_Code",
                table: "Projects",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PromoteBatches_ProjectId_FolderFileId_At",
                table: "PromoteBatches",
                columns: new[] { "ProjectId", "FolderFileId", "At" });

            migrationBuilder.CreateIndex(
                name: "IX_RefreshTokens_Token",
                table: "RefreshTokens",
                column: "Token",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RefreshTokens_UserId",
                table: "RefreshTokens",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_RoleClaims_RoleId",
                table: "RoleClaims",
                column: "RoleId");

            migrationBuilder.CreateIndex(
                name: "RoleNameIndex",
                table: "Roles",
                column: "NormalizedName",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_StatusMappings_ProjectId_AconexStatus",
                table: "StatusMappings",
                columns: new[] { "ProjectId", "AconexStatus" },
                unique: true,
                filter: "NOT \"IsDeleted\"");

            migrationBuilder.CreateIndex(
                name: "IX_TidpDrafts_ImportBatchId",
                table: "TidpDrafts",
                column: "ImportBatchId");

            migrationBuilder.CreateIndex(
                name: "IX_TidpDrafts_ProjectId_FolderFileId",
                table: "TidpDrafts",
                columns: new[] { "ProjectId", "FolderFileId" });

            migrationBuilder.CreateIndex(
                name: "IX_Tidps_DisciplineId",
                table: "Tidps",
                column: "DisciplineId");

            migrationBuilder.CreateIndex(
                name: "IX_Tidps_FolderFileId",
                table: "Tidps",
                column: "FolderFileId");

            migrationBuilder.CreateIndex(
                name: "IX_Tidps_ProjectId_DisciplineId",
                table: "Tidps",
                columns: new[] { "ProjectId", "DisciplineId" });

            migrationBuilder.CreateIndex(
                name: "IX_UserClaims_UserId",
                table: "UserClaims",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_UserLogins_UserId",
                table: "UserLogins",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_UserRoles_RoleId",
                table: "UserRoles",
                column: "RoleId");

            migrationBuilder.CreateIndex(
                name: "EmailIndex",
                table: "Users",
                column: "NormalizedEmail");

            migrationBuilder.CreateIndex(
                name: "UserNameIndex",
                table: "Users",
                column: "NormalizedUserName",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AconexRevisions");

            migrationBuilder.DropTable(
                name: "AuditLogs");

            migrationBuilder.DropTable(
                name: "BaselineActivities");

            migrationBuilder.DropTable(
                name: "DataExchangeDrafts");

            migrationBuilder.DropTable(
                name: "DataExchanges");

            migrationBuilder.DropTable(
                name: "DocumentSnapshots");

            migrationBuilder.DropTable(
                name: "FileBlobs");

            migrationBuilder.DropTable(
                name: "ImportBatches");

            migrationBuilder.DropTable(
                name: "PromoteBatches");

            migrationBuilder.DropTable(
                name: "RefreshTokens");

            migrationBuilder.DropTable(
                name: "RoleClaims");

            migrationBuilder.DropTable(
                name: "StatusMappings");

            migrationBuilder.DropTable(
                name: "UserClaims");

            migrationBuilder.DropTable(
                name: "UserLogins");

            migrationBuilder.DropTable(
                name: "UserRoles");

            migrationBuilder.DropTable(
                name: "UserTokens");

            migrationBuilder.DropTable(
                name: "DocumentDrafts");

            migrationBuilder.DropTable(
                name: "Documents");

            migrationBuilder.DropTable(
                name: "Roles");

            migrationBuilder.DropTable(
                name: "Users");

            migrationBuilder.DropTable(
                name: "TidpDrafts");

            migrationBuilder.DropTable(
                name: "Tidps");

            migrationBuilder.DropTable(
                name: "Disciplines");

            migrationBuilder.DropTable(
                name: "FolderFiles");

            migrationBuilder.DropTable(
                name: "Folders");

            migrationBuilder.DropTable(
                name: "PicklistItems");

            migrationBuilder.DropTable(
                name: "Projects");
        }
    }
}
