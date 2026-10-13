using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Authora.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddOpenIdConnect : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "AuthenticatedAt",
                schema: "auth",
                table: "authorization_codes",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            // Codes issued before the upgrade do not know when the User signed in. The sign-in was at most the Session
            // lifetime before the code was issued, and the code expires within minutes, so its creation time is used.
            migrationBuilder.Sql("""
                UPDATE auth.authorization_codes
                SET "AuthenticatedAt" = "CreatedAt";
                """);

            migrationBuilder.AddColumn<string>(
                name: "Nonce",
                schema: "auth",
                table: "authorization_codes",
                type: "character varying(512)",
                maxLength: 512,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AuthenticatedAt",
                schema: "auth",
                table: "authorization_codes");

            migrationBuilder.DropColumn(
                name: "Nonce",
                schema: "auth",
                table: "authorization_codes");
        }
    }
}
