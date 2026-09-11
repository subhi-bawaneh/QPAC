using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Dip.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class S2_RemoveDriveAndLayers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Documents_FolderFiles_FolderFileId",
                table: "Documents");

            migrationBuilder.DropForeignKey(
                name: "FK_Documents_Tidps_TidpId",
                table: "Documents");

            migrationBuilder.DropForeignKey(
                name: "FK_ImportBatches_FolderFiles_FolderFileId",
                table: "ImportBatches");

            migrationBuilder.DropTable(
                name: "DataExchangeDrafts");

            migrationBuilder.DropTable(
                name: "FileBlobs");

            migrationBuilder.DropTable(
                name: "PromoteBatches");

            migrationBuilder.DropTable(
                name: "Tidps");

            migrationBuilder.DropTable(
                name: "DocumentDrafts");

            migrationBuilder.DropTable(
                name: "FolderFiles");

            migrationBuilder.DropTable(
                name: "TidpDrafts");

            migrationBuilder.DropTable(
                name: "Folders");

            migrationBuilder.DropIndex(
                name: "IX_DocumentSnapshots_FolderFileId",
                table: "DocumentSnapshots");

            migrationBuilder.DropIndex(
                name: "IX_DocumentSnapshots_ProjectId_Layer",
                table: "DocumentSnapshots");

            migrationBuilder.DropIndex(
                name: "IX_Documents_FolderFileId",
                table: "Documents");

            migrationBuilder.DropColumn(
                name: "Completed",
                table: "ImportBatches");

            migrationBuilder.DropColumn(
                name: "Target",
                table: "ImportBatches");

            migrationBuilder.DropColumn(
                name: "FolderFileId",
                table: "DocumentSnapshots");

            migrationBuilder.DropColumn(
                name: "Layer",
                table: "DocumentSnapshots");

            migrationBuilder.DropColumn(
                name: "FolderFileId",
                table: "Documents");

            migrationBuilder.RenameColumn(
                name: "ImportedBy",
                table: "ImportBatches",
                newName: "UploadedBy");

            migrationBuilder.RenameColumn(
                name: "FolderFileId",
                table: "ImportBatches",
                newName: "TidpFileId");

            migrationBuilder.RenameIndex(
                name: "IX_ImportBatches_FolderFileId",
                table: "ImportBatches",
                newName: "IX_ImportBatches_TidpFileId");

            migrationBuilder.RenameColumn(
                name: "TidpId",
                table: "Documents",
                newName: "TidpFileId");

            migrationBuilder.RenameIndex(
                name: "IX_Documents_TidpId",
                table: "Documents",
                newName: "IX_Documents_TidpFileId");

            migrationBuilder.AddColumn<DateTime>(
                name: "EditedAt",
                table: "StatusMappings",
                type: "timestamp without time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "EditedBy",
                table: "StatusMappings",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsEdited",
                table: "StatusMappings",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "EditedAt",
                table: "PicklistItems",
                type: "timestamp without time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "EditedBy",
                table: "PicklistItems",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsEdited",
                table: "PicklistItems",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "RowsDuplicate",
                table: "ImportBatches",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "Status",
                table: "ImportBatches",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<Guid>(
                name: "TidpFileId",
                table: "DocumentSnapshots",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<DateTime>(
                name: "EditedAt",
                table: "Documents",
                type: "timestamp without time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "EditedBy",
                table: "Documents",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsEdited",
                table: "Documents",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "EditedAt",
                table: "BaselineActivities",
                type: "timestamp without time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "EditedBy",
                table: "BaselineActivities",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsEdited",
                table: "BaselineActivities",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AlterColumn<string>(
                name: "OldValue",
                table: "AuditLogs",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(2000)",
                oldMaxLength: 2000,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "NewValue",
                table: "AuditLogs",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(2000)",
                oldMaxLength: 2000,
                oldNullable: true);

            migrationBuilder.CreateTable(
                name: "TidpFiles",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: false),
                    DisciplineId = table.Column<Guid>(type: "uuid", nullable: false),
                    DocumentReference = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    RevisionNumber = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    DateCreated = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    DateLastUpdated = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    FileName = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    RowsRead = table.Column<int>(type: "integer", nullable: false),
                    RowsImported = table.Column<int>(type: "integer", nullable: false),
                    RowsSkipped = table.Column<int>(type: "integer", nullable: false),
                    UploadedBy = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    UploadedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Error = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    UpdatedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    RowVersion = table.Column<byte[]>(type: "bytea", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TidpFiles", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TidpFiles_Disciplines_DisciplineId",
                        column: x => x.DisciplineId,
                        principalTable: "Disciplines",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TidpFiles_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "Projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DocumentSnapshots_TidpFileId",
                table: "DocumentSnapshots",
                column: "TidpFileId");

            migrationBuilder.CreateIndex(
                name: "IX_Documents_ProjectId_TidpFileId",
                table: "Documents",
                columns: new[] { "ProjectId", "TidpFileId" });

            migrationBuilder.CreateIndex(
                name: "IX_TidpFiles_DisciplineId",
                table: "TidpFiles",
                column: "DisciplineId");

            migrationBuilder.CreateIndex(
                name: "IX_TidpFiles_ProjectId_DisciplineId",
                table: "TidpFiles",
                columns: new[] { "ProjectId", "DisciplineId" });

            migrationBuilder.CreateIndex(
                name: "IX_TidpFiles_ProjectId_UploadedAt",
                table: "TidpFiles",
                columns: new[] { "ProjectId", "UploadedAt" });

            migrationBuilder.AddForeignKey(
                name: "FK_Documents_TidpFiles_TidpFileId",
                table: "Documents",
                column: "TidpFileId",
                principalTable: "TidpFiles",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_DocumentSnapshots_Documents_DocumentId",
                table: "DocumentSnapshots",
                column: "DocumentId",
                principalTable: "Documents",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_ImportBatches_TidpFiles_TidpFileId",
                table: "ImportBatches",
                column: "TidpFileId",
                principalTable: "TidpFiles",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Documents_TidpFiles_TidpFileId",
                table: "Documents");

            migrationBuilder.DropForeignKey(
                name: "FK_DocumentSnapshots_Documents_DocumentId",
                table: "DocumentSnapshots");

            migrationBuilder.DropForeignKey(
                name: "FK_ImportBatches_TidpFiles_TidpFileId",
                table: "ImportBatches");

            migrationBuilder.DropTable(
                name: "TidpFiles");

            migrationBuilder.DropIndex(
                name: "IX_DocumentSnapshots_TidpFileId",
                table: "DocumentSnapshots");

            migrationBuilder.DropIndex(
                name: "IX_Documents_ProjectId_TidpFileId",
                table: "Documents");

            migrationBuilder.DropColumn(
                name: "EditedAt",
                table: "StatusMappings");

            migrationBuilder.DropColumn(
                name: "EditedBy",
                table: "StatusMappings");

            migrationBuilder.DropColumn(
                name: "IsEdited",
                table: "StatusMappings");

            migrationBuilder.DropColumn(
                name: "EditedAt",
                table: "PicklistItems");

            migrationBuilder.DropColumn(
                name: "EditedBy",
                table: "PicklistItems");

            migrationBuilder.DropColumn(
                name: "IsEdited",
                table: "PicklistItems");

            migrationBuilder.DropColumn(
                name: "RowsDuplicate",
                table: "ImportBatches");

            migrationBuilder.DropColumn(
                name: "Status",
                table: "ImportBatches");

            migrationBuilder.DropColumn(
                name: "TidpFileId",
                table: "DocumentSnapshots");

            migrationBuilder.DropColumn(
                name: "EditedAt",
                table: "Documents");

            migrationBuilder.DropColumn(
                name: "EditedBy",
                table: "Documents");

            migrationBuilder.DropColumn(
                name: "IsEdited",
                table: "Documents");

            migrationBuilder.DropColumn(
                name: "EditedAt",
                table: "BaselineActivities");

            migrationBuilder.DropColumn(
                name: "EditedBy",
                table: "BaselineActivities");

            migrationBuilder.DropColumn(
                name: "IsEdited",
                table: "BaselineActivities");

            migrationBuilder.RenameColumn(
                name: "UploadedBy",
                table: "ImportBatches",
                newName: "ImportedBy");

            migrationBuilder.RenameColumn(
                name: "TidpFileId",
                table: "ImportBatches",
                newName: "FolderFileId");

            migrationBuilder.RenameIndex(
                name: "IX_ImportBatches_TidpFileId",
                table: "ImportBatches",
                newName: "IX_ImportBatches_FolderFileId");

            migrationBuilder.RenameColumn(
                name: "TidpFileId",
                table: "Documents",
                newName: "TidpId");

            migrationBuilder.RenameIndex(
                name: "IX_Documents_TidpFileId",
                table: "Documents",
                newName: "IX_Documents_TidpId");

            migrationBuilder.AddColumn<bool>(
                name: "Completed",
                table: "ImportBatches",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "Target",
                table: "ImportBatches",
                type: "character varying(10)",
                maxLength: 10,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<Guid>(
                name: "FolderFileId",
                table: "DocumentSnapshots",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Layer",
                table: "DocumentSnapshots",
                type: "character varying(10)",
                maxLength: 10,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<Guid>(
                name: "FolderFileId",
                table: "Documents",
                type: "uuid",
                nullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "OldValue",
                table: "AuditLogs",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "NewValue",
                table: "AuditLogs",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.CreateTable(
                name: "Folders",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AuthorId = table.Column<Guid>(type: "uuid", nullable: true),
                    ParentId = table.Column<Guid>(type: "uuid", nullable: true),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: false),
                    DriveFolderId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    IsCompany = table.Column<bool>(type: "boolean", nullable: false),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    LastSyncedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    Name = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    Path = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    Target = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false)
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
                name: "PromoteBatches",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Added = table.Column<int>(type: "integer", nullable: false),
                    At = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    By = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Deleted = table.Column<int>(type: "integer", nullable: false),
                    FolderFileId = table.Column<Guid>(type: "uuid", nullable: false),
                    FolderId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: false),
                    Skipped = table.Column<int>(type: "integer", nullable: false),
                    Updated = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PromoteBatches", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "TidpDrafts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    DateCreated = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    DateLastUpdated = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    DisciplineId = table.Column<Guid>(type: "uuid", nullable: false),
                    DocumentReference = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    FolderFileId = table.Column<Guid>(type: "uuid", nullable: false),
                    ImportBatchId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: false),
                    RevisionNumber = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    RowVersion = table.Column<byte[]>(type: "bytea", nullable: false),
                    SourceFileName = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    State = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    UpdatedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TidpDrafts", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "FolderFiles",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    FolderId = table.Column<Guid>(type: "uuid", nullable: false),
                    ContentMd5 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    ContentModifiedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    ContentSource = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    DriveFileId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    DriveModifiedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    ImportError = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    Kind = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    LastImportBatchId = table.Column<Guid>(type: "uuid", nullable: true),
                    LastImportedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    Name = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    NameLower = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true, computedColumnSql: "lower(\"Name\")", stored: true),
                    SizeBytes = table.Column<long>(type: "bigint", nullable: false),
                    State = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false)
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
                name: "DocumentDrafts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ActivityId = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    AuthoringSoftware = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    BudgetWeight = table.Column<decimal>(type: "numeric(10,4)", precision: 10, scale: 4, nullable: false),
                    ClassificationCode = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    ConflictReason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    CorporateDiscipline = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    DeliveryMilestone = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    DisciplineId = table.Column<Guid>(type: "uuid", nullable: false),
                    DocumentNumber = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    ExchangeFormat = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    ExtractedFromModel = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
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
                    FolderFileId = table.Column<Guid>(type: "uuid", nullable: false),
                    ImportBatchId = table.Column<Guid>(type: "uuid", nullable: false),
                    IsDuplicate = table.Column<bool>(type: "boolean", nullable: false),
                    LiveDocumentId = table.Column<Guid>(type: "uuid", nullable: true),
                    PackageName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "bytea", nullable: false),
                    Scale = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    ScopeArea = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    State = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    TidpDraftId = table.Column<Guid>(type: "uuid", nullable: false),
                    Title = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    UpdatedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false)
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
                name: "Tidps",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    DisciplineId = table.Column<Guid>(type: "uuid", nullable: false),
                    FolderFileId = table.Column<Guid>(type: "uuid", nullable: true),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    DateCreated = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    DateLastUpdated = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    DocumentReference = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    RevisionNumber = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    RowVersion = table.Column<byte[]>(type: "bytea", nullable: false),
                    SourceFileName = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    UpdatedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false)
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
                name: "DataExchangeDrafts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Author = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    DocumentDraftId = table.Column<Guid>(type: "uuid", nullable: false),
                    DurationDays = table.Column<int>(type: "integer", nullable: true),
                    ExchangeDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    Geometrical = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    NonGeometrical = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Number = table.Column<int>(type: "integer", nullable: false),
                    Predecessor = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    ProgrammeRef = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    Stage = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true)
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

            migrationBuilder.CreateIndex(
                name: "IX_DocumentSnapshots_FolderFileId",
                table: "DocumentSnapshots",
                column: "FolderFileId");

            migrationBuilder.CreateIndex(
                name: "IX_DocumentSnapshots_ProjectId_Layer",
                table: "DocumentSnapshots",
                columns: new[] { "ProjectId", "Layer" });

            migrationBuilder.CreateIndex(
                name: "IX_Documents_FolderFileId",
                table: "Documents",
                column: "FolderFileId");

            migrationBuilder.CreateIndex(
                name: "IX_DataExchangeDrafts_DocumentDraftId_Number",
                table: "DataExchangeDrafts",
                columns: new[] { "DocumentDraftId", "Number" },
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
                name: "IX_PromoteBatches_ProjectId_FolderFileId_At",
                table: "PromoteBatches",
                columns: new[] { "ProjectId", "FolderFileId", "At" });

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

            migrationBuilder.AddForeignKey(
                name: "FK_Documents_FolderFiles_FolderFileId",
                table: "Documents",
                column: "FolderFileId",
                principalTable: "FolderFiles",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_Documents_Tidps_TidpId",
                table: "Documents",
                column: "TidpId",
                principalTable: "Tidps",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_ImportBatches_FolderFiles_FolderFileId",
                table: "ImportBatches",
                column: "FolderFileId",
                principalTable: "FolderFiles",
                principalColumn: "Id");
        }
    }
}
