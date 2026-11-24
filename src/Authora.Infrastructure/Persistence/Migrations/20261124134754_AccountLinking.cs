using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Authora.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AccountLinking : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "PasswordHash",
                schema: "identity",
                table: "users",
                type: "character varying(256)",
                maxLength: 256,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(256)",
                oldMaxLength: 256);

            migrationBuilder.AddColumn<bool>(
                name: "AllowSignUp",
                schema: "identity",
                table: "identity_providers",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string[]>(
                name: "AllowedEmailDomains",
                schema: "identity",
                table: "identity_providers",
                type: "text[]",
                nullable: false,
                defaultValue: new string[0]);

            migrationBuilder.CreateTable(
                name: "identity_provider_default_groups",
                schema: "identity",
                columns: table => new
                {
                    IdentityProviderId = table.Column<Guid>(type: "uuid", nullable: false),
                    GroupId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_identity_provider_default_groups", x => new { x.IdentityProviderId, x.GroupId });
                    table.ForeignKey(
                        name: "FK_identity_provider_default_groups_groups_GroupId",
                        column: x => x.GroupId,
                        principalSchema: "identity",
                        principalTable: "groups",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_identity_provider_default_groups_identity_providers_Identit~",
                        column: x => x.IdentityProviderId,
                        principalSchema: "identity",
                        principalTable: "identity_providers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_identity_provider_default_groups_GroupId",
                schema: "identity",
                table: "identity_provider_default_groups",
                column: "GroupId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "identity_provider_default_groups",
                schema: "identity");

            migrationBuilder.DropColumn(
                name: "AllowSignUp",
                schema: "identity",
                table: "identity_providers");

            migrationBuilder.DropColumn(
                name: "AllowedEmailDomains",
                schema: "identity",
                table: "identity_providers");

            migrationBuilder.AlterColumn<string>(
                name: "PasswordHash",
                schema: "identity",
                table: "users",
                type: "character varying(256)",
                maxLength: 256,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "character varying(256)",
                oldMaxLength: 256,
                oldNullable: true);
        }
    }
}
