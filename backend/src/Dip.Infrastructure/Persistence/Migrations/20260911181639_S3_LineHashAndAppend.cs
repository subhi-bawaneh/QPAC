using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Dip.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class S3_LineHashAndAppend : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_AconexRevisions_ProjectId_AconexDocNo_Revision_DateModified",
                table: "AconexRevisions");

            migrationBuilder.DropColumn(
                name: "InMidp",
                table: "AconexRevisions");

            migrationBuilder.AddColumn<string>(
                name: "LineHash",
                table: "AconexRevisions",
                type: "character varying(64)",
                maxLength: 64,
                nullable: false,
                defaultValue: "");


            // The unique index below cannot be created while every row carries the
            // default empty hash, so the existing history is hashed first. The
            // expression is AconexLineHashSql, which is pinned to AconexLineHasher by
            // AconexAppendTests against the real export.
            migrationBuilder.Sql(
                $@"UPDATE ""AconexRevisions"" a SET ""LineHash"" = {AconexLineHashSql.HashExpression("a")};");

            migrationBuilder.CreateIndex(
                name: "IX_AconexRevisions_ProjectId_AconexDocNo_Revision_DateModified",
                table: "AconexRevisions",
                columns: new[] { "ProjectId", "AconexDocNo", "Revision", "DateModified" });

            migrationBuilder.CreateIndex(
                name: "IX_AconexRevisions_ProjectId_LineHash",
                table: "AconexRevisions",
                columns: new[] { "ProjectId", "LineHash" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_AconexRevisions_ProjectId_AconexDocNo_Revision_DateModified",
                table: "AconexRevisions");

            migrationBuilder.DropIndex(
                name: "IX_AconexRevisions_ProjectId_LineHash",
                table: "AconexRevisions");

            migrationBuilder.DropColumn(
                name: "LineHash",
                table: "AconexRevisions");

            migrationBuilder.AddColumn<bool>(
                name: "InMidp",
                table: "AconexRevisions",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateIndex(
                name: "IX_AconexRevisions_ProjectId_AconexDocNo_Revision_DateModified",
                table: "AconexRevisions",
                columns: new[] { "ProjectId", "AconexDocNo", "Revision", "DateModified" },
                unique: true);
        }
    }
}
