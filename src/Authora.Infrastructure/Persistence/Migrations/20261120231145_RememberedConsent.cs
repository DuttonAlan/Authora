using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Authora.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RememberedConsent : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "consents",
                schema: "auth",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    ClientId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ModifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_consents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_consents_clients_ClientId",
                        column: x => x.ClientId,
                        principalSchema: "identity",
                        principalTable: "clients",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_consents_users_UserId",
                        column: x => x.UserId,
                        principalSchema: "identity",
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "consent_scopes",
                schema: "auth",
                columns: table => new
                {
                    ConsentId = table.Column<Guid>(type: "uuid", nullable: false),
                    ScopeId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_consent_scopes", x => new { x.ConsentId, x.ScopeId });
                    table.ForeignKey(
                        name: "FK_consent_scopes_consents_ConsentId",
                        column: x => x.ConsentId,
                        principalSchema: "auth",
                        principalTable: "consents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_consent_scopes_scopes_ScopeId",
                        column: x => x.ScopeId,
                        principalSchema: "identity",
                        principalTable: "scopes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_consent_scopes_ScopeId",
                schema: "auth",
                table: "consent_scopes",
                column: "ScopeId");

            migrationBuilder.CreateIndex(
                name: "IX_consents_ClientId",
                schema: "auth",
                table: "consents",
                column: "ClientId");

            migrationBuilder.CreateIndex(
                name: "IX_consents_UserId_ClientId",
                schema: "auth",
                table: "consents",
                columns: new[] { "UserId", "ClientId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "consent_scopes",
                schema: "auth");

            migrationBuilder.DropTable(
                name: "consents",
                schema: "auth");
        }
    }
}
