using System;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ventagram.Migrations
{
    /// <inheritdoc />
    public partial class add_publication_media_table : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PublicationMedia",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    PublicationId = table.Column<int>(type: "int", nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false),
                    MediaType = table.Column<byte>(type: "tinyint unsigned", nullable: false),
                    Url = table.Column<string>(type: "varchar(1000)", maxLength: 1000, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    IsPrimary = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PublicationMedia", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PublicationMedia_Publications_PublicationId",
                        column: x => x.PublicationId,
                        principalTable: "Publications",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_PublicationMedia_PublicationId_MediaType_IsPrimary",
                table: "PublicationMedia",
                columns: new[] { "PublicationId", "MediaType", "IsPrimary" });

            migrationBuilder.CreateIndex(
                name: "IX_PublicationMedia_PublicationId_SortOrder",
                table: "PublicationMedia",
                columns: new[] { "PublicationId", "SortOrder" },
                unique: true);

            migrationBuilder.DropColumn(
                name: "ImagesCsv",
                table: "Publications");

            migrationBuilder.DropColumn(
                name: "VideoUrl",
                table: "Publications");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ImagesCsv",
                table: "Publications",
                type: "longtext",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "VideoUrl",
                table: "Publications",
                type: "varchar(400)",
                maxLength: 400,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.DropTable(
                name: "PublicationMedia");
        }
    }
}
