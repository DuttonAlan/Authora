using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Authora.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AudienceRestrictedAccessTokens : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid[]>(
                name: "Resources",
                schema: "auth",
                table: "grants",
                type: "uuid[]",
                nullable: false,
                defaultValue: new Guid[0]);

            migrationBuilder.AddColumn<bool>(
                name: "RequiresResourceIndicator",
                schema: "identity",
                table: "clients",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "ResourceIdentifier",
                schema: "identity",
                table: "clients",
                type: "character varying(2048)",
                maxLength: 2048,
                nullable: true);

            migrationBuilder.AddColumn<Guid[]>(
                name: "Resources",
                schema: "auth",
                table: "authorization_codes",
                type: "uuid[]",
                nullable: false,
                defaultValue: new Guid[0]);

            migrationBuilder.AddColumn<Guid[]>(
                name: "Audience",
                schema: "auth",
                table: "access_tokens",
                type: "uuid[]",
                nullable: false,
                defaultValue: new Guid[0]);

            migrationBuilder.CreateIndex(
                name: "IX_clients_ResourceIdentifier",
                schema: "identity",
                table: "clients",
                column: "ResourceIdentifier",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_clients_ResourceIdentifier",
                schema: "identity",
                table: "clients");

            migrationBuilder.DropColumn(
                name: "Resources",
                schema: "auth",
                table: "grants");

            migrationBuilder.DropColumn(
                name: "RequiresResourceIndicator",
                schema: "identity",
                table: "clients");

            migrationBuilder.DropColumn(
                name: "ResourceIdentifier",
                schema: "identity",
                table: "clients");

            migrationBuilder.DropColumn(
                name: "Resources",
                schema: "auth",
                table: "authorization_codes");

            migrationBuilder.DropColumn(
                name: "Audience",
                schema: "auth",
                table: "access_tokens");
        }
    }
}
