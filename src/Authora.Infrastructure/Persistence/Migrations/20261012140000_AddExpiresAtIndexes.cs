using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Authora.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddExpiresAtIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_access_tokens_ExpiresAt",
                schema: "auth",
                table: "access_tokens",
                column: "ExpiresAt");

            migrationBuilder.CreateIndex(
                name: "IX_authorization_codes_ExpiresAt",
                schema: "auth",
                table: "authorization_codes",
                column: "ExpiresAt");

            migrationBuilder.CreateIndex(
                name: "IX_refresh_tokens_ExpiresAt",
                schema: "auth",
                table: "refresh_tokens",
                column: "ExpiresAt");

            migrationBuilder.CreateIndex(
                name: "IX_sessions_ExpiresAt",
                schema: "auth",
                table: "sessions",
                column: "ExpiresAt");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_access_tokens_ExpiresAt",
                schema: "auth",
                table: "access_tokens");

            migrationBuilder.DropIndex(
                name: "IX_authorization_codes_ExpiresAt",
                schema: "auth",
                table: "authorization_codes");

            migrationBuilder.DropIndex(
                name: "IX_refresh_tokens_ExpiresAt",
                schema: "auth",
                table: "refresh_tokens");

            migrationBuilder.DropIndex(
                name: "IX_sessions_ExpiresAt",
                schema: "auth",
                table: "sessions");
        }
    }
}
