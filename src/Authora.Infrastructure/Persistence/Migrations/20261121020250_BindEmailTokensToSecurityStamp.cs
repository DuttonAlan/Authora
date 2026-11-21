using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Authora.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class BindEmailTokensToSecurityStamp : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "SecurityStamp",
                schema: "auth",
                table: "email_tokens",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SecurityStamp",
                schema: "auth",
                table: "email_tokens");
        }
    }
}
