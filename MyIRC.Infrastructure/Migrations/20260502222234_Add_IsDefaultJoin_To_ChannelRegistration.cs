using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MyIRC.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Add_IsDefaultJoin_To_ChannelRegistration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsDefaultJoin",
                table: "ChannelRegistrations",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsDefaultJoin",
                table: "ChannelRegistrations");
        }
    }
}
