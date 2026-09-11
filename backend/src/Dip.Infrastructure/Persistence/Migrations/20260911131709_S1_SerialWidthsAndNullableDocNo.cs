using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Dip.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class S1_SerialWidthsAndNullableDocNo : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "DocNoFinal",
                table: "AconexRevisions",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(200)",
                oldMaxLength: 200);

            migrationBuilder.CreateTable(
                name: "DocumentTypeSerials",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: false),
                    DocType = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    SequenceWidth = table.Column<int>(type: "integer", nullable: false),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DocumentTypeSerials", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DocumentTypeSerials_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "Projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DocumentTypeSerials_ProjectId_DocType",
                table: "DocumentTypeSerials",
                columns: new[] { "ProjectId", "DocType" },
                unique: true,
                filter: "NOT \"IsDeleted\"");

            // The 22 rows already carrying the old "XXX" sentinel become what they
            // always meant: a row whose raw Aconex value is not a document number.
            migrationBuilder.Sql(
                "UPDATE \"AconexRevisions\" SET \"DocNoFinal\" = NULL WHERE \"DocNoFinal\" = 'XXX';");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DocumentTypeSerials");

            migrationBuilder.Sql(
                "UPDATE \"AconexRevisions\" SET \"DocNoFinal\" = 'XXX' WHERE \"DocNoFinal\" IS NULL;");

            migrationBuilder.AlterColumn<string>(
                name: "DocNoFinal",
                table: "AconexRevisions",
                type: "character varying(200)",
                maxLength: 200,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "character varying(200)",
                oldMaxLength: 200,
                oldNullable: true);
        }
    }
}
