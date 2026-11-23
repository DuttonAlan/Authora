// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

using Authora.Core.Domain.Entities;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Authora.Infrastructure.Persistence.Configurations;

/// <summary>
///     Configures the database mapping of the outbox of back-channel logout notifications.
/// </summary>
public sealed class BackchannelLogoutNotificationConfiguration : IEntityTypeConfiguration<BackchannelLogoutNotification>
{
    /// <summary>
    ///     Name of the table in the <c>auth</c> schema.
    /// </summary>
    internal const string TableName = "backchannel_logout_notifications";

    private const string Table = Schemas.Auth + "." + TableName;

    /// <summary>
    ///     Writes one notification, due at <c>{1}</c>, for every Client with a Back-Channel Logout URI that has a Grant
    ///     approved in one of the Sessions <c>{0}</c> (an array), see <c>BackchannelLogoutRepository</c>. Sessions
    ///     without a Sid, from before it was recorded, are skipped. The Ids are random (v4), not time-ordered like those
    ///     created in .NET; nothing depends on their order.
    /// </summary>
    internal const string EnqueueSql =
        "INSERT INTO " + Table
        + " (\"" + nameof(BackchannelLogoutNotification.Id) + "\", \"" + nameof(BackchannelLogoutNotification.ClientId)
        + "\", \"" + nameof(BackchannelLogoutNotification.UserId) + "\", \"" + nameof(BackchannelLogoutNotification.Sid)
        + "\", \"" + nameof(BackchannelLogoutNotification.Attempts) + "\", \"" + nameof(BackchannelLogoutNotification.NextAttemptAt)
        + "\", \"" + nameof(BackchannelLogoutNotification.CreatedAt) + "\")"
        + " SELECT gen_random_uuid(), pairs.\"ClientId\", pairs.\"UserId\", pairs.\"Sid\", 0, {1}, {1} FROM ("
        + "SELECT DISTINCT g.\"" + nameof(Grant.ClientId) + "\" AS \"ClientId\", s.\"" + nameof(Session.UserId) + "\" AS \"UserId\", s.\""
        + nameof(Session.Sid) + "\" AS \"Sid\""
        + " FROM " + Schemas.Auth + ".grants g"
        + " JOIN " + Schemas.Auth + "." + SessionConfiguration.TableName + " s ON s.\"" + nameof(Session.Id) + "\" = g.\"" + nameof(Grant.SessionId) + "\""
        + " JOIN " + Schemas.Identity + ".clients c ON c.\"" + nameof(Client.Id) + "\" = g.\"" + nameof(Grant.ClientId) + "\""
        + " WHERE g.\"" + nameof(Grant.SessionId) + "\" = ANY({0})"
        + " AND c.\"" + nameof(Client.BackchannelLogoutUri) + "\" IS NOT NULL"
        + " AND s.\"" + nameof(Session.Sid) + "\" <> '') AS pairs";

    /// <summary>
    ///     Claims up to <c>{1}</c> notifications that are due at <c>{0}</c> by moving their next attempt to <c>{2}</c>,
    ///     and returns them, see <c>BackchannelLogoutDeliverer</c>. Rows another instance is claiming at the same time
    ///     are skipped (<c>FOR UPDATE SKIP LOCKED</c>), so no notification is sent twice at once.
    /// </summary>
    internal const string ClaimDueSql =
        "UPDATE " + Table + " SET \"" + nameof(BackchannelLogoutNotification.NextAttemptAt) + "\" = {2}"
        + " WHERE \"" + nameof(BackchannelLogoutNotification.Id) + "\" IN ("
        + "SELECT \"" + nameof(BackchannelLogoutNotification.Id) + "\" FROM " + Table
        + " WHERE \"" + nameof(BackchannelLogoutNotification.NextAttemptAt) + "\" <= {0}"
        + " ORDER BY \"" + nameof(BackchannelLogoutNotification.NextAttemptAt) + "\""
        + " LIMIT {1} FOR UPDATE SKIP LOCKED)"
        + " RETURNING *";

    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<BackchannelLogoutNotification> builder)
    {
        builder.ToTable(TableName, Schemas.Auth);

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id)
            .ValueGeneratedNever();

        builder.Property(x => x.Sid)
            .IsRequired()
            .HasMaxLength(Session.SidMaxLength);

        builder.Property(x => x.LastError)
            .HasMaxLength(BackchannelLogoutNotification.LastErrorMaxLength);

        builder.Ignore(x => x.Subject);

        // Used to find the due notifications.
        builder.HasIndex(x => x.NextAttemptAt);

        // A deleted Client is not notified any more.
        builder.HasOne<Client>()
            .WithMany()
            .HasForeignKey(x => x.ClientId)
            .OnDelete(DeleteBehavior.Cascade);

        // UserId has no foreign key: the notification is sent even after the User was deleted, which only marks them.
    }
}
