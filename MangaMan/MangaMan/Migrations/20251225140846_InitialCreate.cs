using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MangaMan.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "SyncFolders",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    ParentId = table.Column<Guid>(type: "TEXT", nullable: true),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    Path = table.Column<string>(type: "TEXT", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    LastSyncAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SyncFolders", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SyncFolders_SyncFolders_ParentId",
                        column: x => x.ParentId,
                        principalTable: "SyncFolders",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "MangaArchives",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    SyncFolderId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    Path = table.Column<string>(type: "TEXT", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    LastOpenedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    WasRead = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MangaArchives", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MangaArchives_SyncFolders_SyncFolderId",
                        column: x => x.SyncFolderId,
                        principalTable: "SyncFolders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_MangaArchives_SyncFolderId",
                table: "MangaArchives",
                column: "SyncFolderId");

            migrationBuilder.CreateIndex(
                name: "IX_SyncFolders_ParentId",
                table: "SyncFolders",
                column: "ParentId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MangaArchives");

            migrationBuilder.DropTable(
                name: "SyncFolders");
        }
    }
}
