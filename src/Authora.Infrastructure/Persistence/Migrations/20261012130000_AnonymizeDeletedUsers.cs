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
