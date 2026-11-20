// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

using Authora.Core.Domain.Entities;

namespace Authora.Core.Application.Consents;

/// <summary>
///     Builds the Details of the Consent audit events, which end with a list of Scopes.
/// </summary>
internal static class ConsentAuditDetails
{
    /// <summary>
    ///     Shortens the Details visibly to <see cref="AuditEvent.DetailsMaxLength"/>, because a list of Scopes can be
    ///     longer.
    /// </summary>
    /// <param name="details">The Details, with the Scopes at the end.</param>
    /// <returns>The Details, shortened if necessary.</returns>
    public static string Shorten(string details)
    {
        const string Shortened = "…' (shortened)";

        return details.Length <= AuditEvent.DetailsMaxLength
            ? details
            : string.Concat(details.AsSpan(0, AuditEvent.DetailsMaxLength - Shortened.Length), Shortened);
    }
}
