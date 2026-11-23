using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Authora.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class BackchannelLogout : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Sid",
                schema: "auth",
                table: "sessions",
                type: "character varying(64)",
                maxLength: 64,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<Guid>(
                name: "SessionId",
                schema: "auth",
                table: "grants",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "BackchannelLogoutSessionRequired",
                schema: "identity",
                table: "clients",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "BackchannelLogoutUri",
                schema: "identity",
                table: "clients",
                type: "character varying(2048)",
                maxLength: 2048,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "backchannel_logout_notifications",
                schema: "auth",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ClientId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Sid = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Attempts = table.Column<int>(type: "integer", nullable: false),
                    NextAttemptAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    LastError = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_backchannel_logout_notifications", x => x.Id);
                    table.ForeignKey(
                        name: "FK_backchannel_logout_notifications_clients_ClientId",
                        column: x => x.ClientId,
                        principalSchema: "identity",
                        principalTable: "clients",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_grants_SessionId",
                schema: "auth",
                table: "grants",
                column: "SessionId");

            migrationBuilder.CreateIndex(
                name: "IX_backchannel_logout_notifications_ClientId",
                schema: "auth",
                table: "backchannel_logout_notifications",
                column: "ClientId");

            migrationBuilder.CreateIndex(
                name: "IX_backchannel_logout_notifications_NextAttemptAt",
                schema: "auth",
                table: "backchannel_logout_notifications",
                column: "NextAttemptAt");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "backchannel_logout_notifications",
                schema: "auth");

            migrationBuilder.DropIndex(
                name: "IX_grants_SessionId",
                schema: "auth",
                table: "grants");

            migrationBuilder.DropColumn(
                name: "Sid",
                schema: "auth",
                table: "sessions");

            migrationBuilder.DropColumn(
                name: "SessionId",
                schema: "auth",
                table: "grants");

            migrationBuilder.DropColumn(
                name: "BackchannelLogoutSessionRequired",
                schema: "identity",
                table: "clients");

            migrationBuilder.DropColumn(
                name: "BackchannelLogoutUri",
                schema: "identity",
                table: "clients");
        }
    }
}
