using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Dip.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class TidpFolderSync : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ContentHash",
                table: "TidpFiles",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DisciplineTag",
                table: "TidpFiles",
                type: "nvarchar(10)",
                maxLength: 10,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "FolderDisciplineId",
                table: "TidpFiles",
                type: "uniqueidentifier",
                nullable: true);

            // "Present", not EF's empty-string default: the column is the string form of
            // an enum, and every TidpFile that predates the folder sync would otherwise
            // read back as a value TidpFolderStatus has never heard of. A file uploaded
            // one at a time has no folder to be missing from, so Present is the truth.
            migrationBuilder.AddColumn<string>(
                name: "FolderStatus",
                table: "TidpFiles",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "Present");

            migrationBuilder.AddColumn<DateTime>(
                name: "LastModifiedUtc",
                table: "TidpFiles",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "LastSeenAt",
                table: "TidpFiles",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "MissingSince",
                table: "TidpFiles",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "NameContract",
                table: "TidpFiles",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "NameDocType",
                table: "TidpFiles",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "NameLevel",
                table: "TidpFiles",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "NameOriginator",
                table: "TidpFiles",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "NameProjectCode",
                table: "TidpFiles",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "NameZone",
                table: "TidpFiles",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "OwnerId",
                table: "TidpFiles",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RelativePath",
                table: "TidpFiles",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Sequence",
                table: "TidpFiles",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "SizeBytes",
                table: "TidpFiles",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.CreateTable(
                name: "TidpFolderOwners",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RelativePath = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    FolderName = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: true),
                    OwnerName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    OwnerType = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    FolderStatus = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    LastSeenAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    MissingSince = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    RowVersion = table.Column<byte[]>(type: "varbinary(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TidpFolderOwners", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TidpFolderOwners_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "Projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "TidpFolderSyncs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RootName = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: false),
                    StartedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    StartedBy = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    TotalFiles = table.Column<int>(type: "int", nullable: false),
                    Added = table.Column<int>(type: "int", nullable: false),
                    Updated = table.Column<int>(type: "int", nullable: false),
                    Skipped = table.Column<int>(type: "int", nullable: false),
                    Missing = table.Column<int>(type: "int", nullable: false),
                    Failed = table.Column<int>(type: "int", nullable: false),
                    ResultJson = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TidpFolderSyncs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TidpFolderSyncs_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "Projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "TidpFolderDisciplines",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OwnerId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RelativePath = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    FolderName = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: false),
                    DisciplineCode = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    DisciplineName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    FolderStatus = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    LastSeenAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    MissingSince = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    RowVersion = table.Column<byte[]>(type: "varbinary(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TidpFolderDisciplines", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TidpFolderDisciplines_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "Projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TidpFolderDisciplines_TidpFolderOwners_OwnerId",
                        column: x => x.OwnerId,
                        principalTable: "TidpFolderOwners",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TidpFiles_FolderDisciplineId",
                table: "TidpFiles",
                column: "FolderDisciplineId");

            migrationBuilder.CreateIndex(
                name: "IX_TidpFiles_OwnerId",
                table: "TidpFiles",
                column: "OwnerId");

            migrationBuilder.CreateIndex(
                name: "IX_TidpFiles_ProjectId_DisciplineTag",
                table: "TidpFiles",
                columns: new[] { "ProjectId", "DisciplineTag" });

            migrationBuilder.CreateIndex(
                name: "IX_TidpFiles_ProjectId_RelativePath",
                table: "TidpFiles",
                columns: new[] { "ProjectId", "RelativePath" },
                unique: true,
                filter: "[RelativePath] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_TidpFolderDisciplines_OwnerId",
                table: "TidpFolderDisciplines",
                column: "OwnerId");

            migrationBuilder.CreateIndex(
                name: "IX_TidpFolderDisciplines_ProjectId_RelativePath",
                table: "TidpFolderDisciplines",
                columns: new[] { "ProjectId", "RelativePath" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TidpFolderOwners_ProjectId_RelativePath",
                table: "TidpFolderOwners",
                columns: new[] { "ProjectId", "RelativePath" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TidpFolderSyncs_ProjectId_StartedAt",
                table: "TidpFolderSyncs",
                columns: new[] { "ProjectId", "StartedAt" });

            migrationBuilder.AddForeignKey(
                name: "FK_TidpFiles_TidpFolderDisciplines_FolderDisciplineId",
                table: "TidpFiles",
                column: "FolderDisciplineId",
                principalTable: "TidpFolderDisciplines",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_TidpFiles_TidpFolderOwners_OwnerId",
                table: "TidpFiles",
                column: "OwnerId",
                principalTable: "TidpFolderOwners",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_TidpFiles_TidpFolderDisciplines_FolderDisciplineId",
                table: "TidpFiles");

            migrationBuilder.DropForeignKey(
                name: "FK_TidpFiles_TidpFolderOwners_OwnerId",
                table: "TidpFiles");

            migrationBuilder.DropTable(
                name: "TidpFolderDisciplines");

            migrationBuilder.DropTable(
                name: "TidpFolderSyncs");

            migrationBuilder.DropTable(
                name: "TidpFolderOwners");

            migrationBuilder.DropIndex(
                name: "IX_TidpFiles_FolderDisciplineId",
                table: "TidpFiles");

            migrationBuilder.DropIndex(
                name: "IX_TidpFiles_OwnerId",
                table: "TidpFiles");

            migrationBuilder.DropIndex(
                name: "IX_TidpFiles_ProjectId_DisciplineTag",
                table: "TidpFiles");

            migrationBuilder.DropIndex(
                name: "IX_TidpFiles_ProjectId_RelativePath",
                table: "TidpFiles");

            migrationBuilder.DropColumn(
                name: "ContentHash",
                table: "TidpFiles");

            migrationBuilder.DropColumn(
                name: "DisciplineTag",
                table: "TidpFiles");

            migrationBuilder.DropColumn(
                name: "FolderDisciplineId",
                table: "TidpFiles");

            migrationBuilder.DropColumn(
                name: "FolderStatus",
                table: "TidpFiles");

            migrationBuilder.DropColumn(
                name: "LastModifiedUtc",
                table: "TidpFiles");

            migrationBuilder.DropColumn(
                name: "LastSeenAt",
                table: "TidpFiles");

            migrationBuilder.DropColumn(
                name: "MissingSince",
                table: "TidpFiles");

            migrationBuilder.DropColumn(
                name: "NameContract",
                table: "TidpFiles");

            migrationBuilder.DropColumn(
                name: "NameDocType",
                table: "TidpFiles");

            migrationBuilder.DropColumn(
                name: "NameLevel",
                table: "TidpFiles");

            migrationBuilder.DropColumn(
                name: "NameOriginator",
                table: "TidpFiles");

            migrationBuilder.DropColumn(
                name: "NameProjectCode",
                table: "TidpFiles");

            migrationBuilder.DropColumn(
                name: "NameZone",
                table: "TidpFiles");

            migrationBuilder.DropColumn(
                name: "OwnerId",
                table: "TidpFiles");

            migrationBuilder.DropColumn(
                name: "RelativePath",
                table: "TidpFiles");

            migrationBuilder.DropColumn(
                name: "Sequence",
                table: "TidpFiles");

            migrationBuilder.DropColumn(
                name: "SizeBytes",
                table: "TidpFiles");
        }
    }
}
