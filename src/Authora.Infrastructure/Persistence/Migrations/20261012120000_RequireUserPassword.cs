using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Authora.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RequireUserPassword : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Deleted Users (State 3) can never sign in again, so the hash is never verified. It only fills the column.
            migrationBuilder.Sql("""
                UPDATE identity.users SET "PasswordHash" = 'deleted-user-without-password'
                WHERE "PasswordHash" IS NULL AND "State" = 3;
                """);

            // Other Users without a password cannot be migrated automatically: there is no password to set for them.
            migrationBuilder.Sql("""
                DO $$
                BEGIN
                    IF EXISTS (SELECT 1 FROM identity.users WHERE "PasswordHash" IS NULL) THEN
                        RAISE EXCEPTION 'Users without a password exist. Set a password via PUT /api/users/{id}/password or delete them, then run the migration again.';
                    END IF;
                END $$;
                """);

            migrationBuilder.AlterColumn<string>(
                name: "PasswordHash",
                schema: "identity",
                table: "users",
                type: "character varying(256)",
                maxLength: 256,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(256)",
                oldMaxLength: 256,
                oldNullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
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
        }
    }
}
