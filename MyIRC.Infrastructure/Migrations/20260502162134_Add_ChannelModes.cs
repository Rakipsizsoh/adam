using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MyIRC.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Add_ChannelModes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Modes",
                table: "ChannelRegistrations",
                type: "longtext",
                nullable: false)
                .Annotation("MySql:CharSet", "utf8mb4");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Modes",
                table: "ChannelRegistrations");
        }
    }
}
