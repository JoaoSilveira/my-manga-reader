using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MangaMan.Migrations
{
    /// <inheritdoc />
    public partial class PersistOpenTabs : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "OpenTabs",
                columns: table => new
                {
                    MangaArchiveId = table.Column<Guid>(type: "TEXT", nullable: false),
                    CurrentPage = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OpenTabs", x => x.MangaArchiveId);
                    table.ForeignKey(
                        name: "FK_OpenTabs_MangaArchives_MangaArchiveId",
                        column: x => x.MangaArchiveId,
                        principalTable: "MangaArchives",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "OpenTabs");
        }
    }
}
