using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ventagram.Migrations
{
    /// <inheritdoc />
    public partial class usuariotildes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "AcceptsCalls",
                table: "Users",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "ContactPreference",
                table: "Users",
                type: "varchar(40)",
                maxLength: 40,
                nullable: false,
                defaultValue: "")
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<bool>(
                name: "RespondsEmails",
                table: "Users",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "RespondsWhatsApp",
                table: "Users",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AcceptsCalls",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "ContactPreference",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "RespondsEmails",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "RespondsWhatsApp",
                table: "Users");
        }
    }
}
