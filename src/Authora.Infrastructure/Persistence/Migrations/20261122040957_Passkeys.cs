using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Authora.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Passkeys : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "passkey_challenges",
                schema: "auth",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ChallengeHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Ceremony = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ConsumedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_passkey_challenges", x => x.Id);
                    table.ForeignKey(
                        name: "FK_passkey_challenges_users_UserId",
                        column: x => x.UserId,
                        principalSchema: "identity",
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "passkeys",
                schema: "identity",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    CredentialId = table.Column<byte[]>(type: "bytea", maxLength: 1023, nullable: false),
                    PublicKey = table.Column<byte[]>(type: "bytea", maxLength: 2048, nullable: false),
                    SignCount = table.Column<long>(type: "bigint", nullable: false),
                    AaGuid = table.Column<Guid>(type: "uuid", nullable: false),
                    Transports = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    IsBackupEligible = table.Column<bool>(type: "boolean", nullable: false),
                    IsBackedUp = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    LastUsedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_passkeys", x => x.Id);
                    table.ForeignKey(
                        name: "FK_passkeys_users_UserId",
                        column: x => x.UserId,
                        principalSchema: "identity",
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_passkey_challenges_ChallengeHash",
                schema: "auth",
                table: "passkey_challenges",
                column: "ChallengeHash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_passkey_challenges_ExpiresAt",
                schema: "auth",
                table: "passkey_challenges",
                column: "ExpiresAt");

            migrationBuilder.CreateIndex(
                name: "IX_passkey_challenges_UserId",
                schema: "auth",
                table: "passkey_challenges",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_passkeys_CredentialId",
                schema: "identity",
                table: "passkeys",
                column: "CredentialId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_passkeys_UserId",
                schema: "identity",
                table: "passkeys",
                column: "UserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "passkey_challenges",
                schema: "auth");

            migrationBuilder.DropTable(
                name: "passkeys",
                schema: "identity");
        }
    }
}
