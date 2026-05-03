using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MyIRC.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Update_ChannelPasswordHash : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Description",
                table: "ChannelRegistrations",
                type: "longtext",
                nullable: false)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "PasswordHash",
                table: "ChannelRegistrations",
                type: "longtext",
                nullable: false)
                .Annotation("MySql:CharSet", "utf8mb4");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Description",
                table: "ChannelRegistrations");

            migrationBuilder.DropColumn(
                name: "PasswordHash",
                table: "ChannelRegistrations");
        }
    }
}
