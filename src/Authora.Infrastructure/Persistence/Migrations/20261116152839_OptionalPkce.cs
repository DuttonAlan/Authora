using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Authora.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class OptionalPkce : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsPkceOptional",
                schema: "identity",
                table: "clients",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AlterColumn<string>(
                name: "CodeChallenge",
                schema: "auth",
                table: "authorization_codes",
                type: "character varying(43)",
                maxLength: 43,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(43)",
                oldMaxLength: 43);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsPkceOptional",
                schema: "identity",
                table: "clients");

            migrationBuilder.AlterColumn<string>(
                name: "CodeChallenge",
                schema: "auth",
                table: "authorization_codes",
                type: "character varying(43)",
                maxLength: 43,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "character varying(43)",
                oldMaxLength: 43,
                oldNullable: true);
        }
    }
}
