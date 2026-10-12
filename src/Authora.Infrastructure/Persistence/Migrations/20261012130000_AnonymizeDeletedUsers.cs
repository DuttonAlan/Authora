using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Authora.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AnonymizeDeletedUsers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // User.SetEmail did not reserve the placeholder domain yet, so a User may hold the placeholder of a deleted
            // User, and the update below would fail on the unique index. Such an address is undeliverable anyway
            // (.invalid), so it is moved to a prefix that MarkDeleted never produces; the User must set a real Email.
            migrationBuilder.Sql("""
                UPDATE identity.users
                SET "Email" = 'reserved-' || replace("Id"::text, '-', '') || '@deleted.invalid',
                    "NormalizedEmail" = 'reserved-' || replace("Id"::text, '-', '') || '@deleted.invalid'
                WHERE "State" <> 3 AND "NormalizedEmail" LIKE '%@deleted.invalid';
                """);

            // Same values as User.MarkDeleted for Users that were deleted before it anonymized them (State 3 = Deleted).
            migrationBuilder.Sql("""
                UPDATE identity.users
                SET "Email" = 'deleted-' || replace("Id"::text, '-', '') || '@deleted.invalid',
                    "NormalizedEmail" = 'deleted-' || replace("Id"::text, '-', '') || '@deleted.invalid',
                    "FirstName" = 'Deleted',
                    "LastName" = 'User',
                    "PasswordHash" = 'deleted'
                WHERE "State" = 3;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // The erased personal data cannot be restored.
        }
    }
}
