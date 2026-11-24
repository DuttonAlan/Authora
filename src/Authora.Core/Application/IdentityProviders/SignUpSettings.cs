// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

using Authora.Core.Abstractions;
using Authora.Core.Domain.Entities;
using Authora.Core.Domain.Exceptions;

namespace Authora.Core.Application.IdentityProviders;

/// <summary>
///     Applies the sign-up settings of a create or update request to an identity provider.
/// </summary>
internal static class SignUpSettings
{
    /// <summary>
    ///     Applies the settings that are set; null leaves a setting unchanged.
    /// </summary>
    /// <param name="provider">The tracked provider, with its default Groups.</param>
    /// <param name="allowSignUp">Whether people can sign up, or null.</param>
    /// <param name="allowedEmailDomains">The allowed domains, or null.</param>
    /// <param name="defaultGroupIds">The Ids of the default Groups, or null.</param>
    /// <param name="groupRepository">An Instance of a IGroupRepository to load the Groups.</param>
    /// <param name="cancellationToken">CancellationToken to cancel the request.</param>
    /// <returns>A Task that completes once the settings are applied.</returns>
    /// <exception cref="DomainValidationException">Thrown when a setting is invalid or a Group does not exist.</exception>
    public static async Task ApplyAsync(
        IdentityProvider provider,
        bool? allowSignUp,
        IReadOnlyList<string>? allowedEmailDomains,
        IReadOnlyList<Guid>? defaultGroupIds,
        IGroupRepository groupRepository,
        CancellationToken cancellationToken)
    {
        if (allowSignUp is not null || allowedEmailDomains is not null)
        {
            provider.ConfigureSignUp(allowSignUp ?? provider.AllowSignUp, allowedEmailDomains ?? provider.AllowedEmailDomains);
        }

        if (defaultGroupIds is null)
        {
            return;
        }

        var ids = defaultGroupIds.Distinct().ToList();

        if (ids.Count > IdentityProvider.MaxDefaultGroups)
        {
            throw new DomainValidationException($"At most {IdentityProvider.MaxDefaultGroups} default Groups can be set.", nameof(defaultGroupIds));
        }

        var groups = new List<Group>(ids.Count);

        foreach (var id in ids)
        {
            groups.Add(await groupRepository.GetGroupById(id, cancellationToken)
                ?? throw new DomainValidationException($"Group '{id}' does not exist.", nameof(defaultGroupIds)));
        }

        provider.SetDefaultGroups(groups);
    }

    /// <summary>
    ///     Describes the settings for the audit log.
    /// </summary>
    /// <param name="provider">The provider.</param>
    /// <returns>A description, e.g. <c>sign-up on, allowed domains [example.com], default Groups [employees]</c>.</returns>
    public static string Describe(IdentityProvider provider)
    {
        var domains = string.Join(", ", provider.AllowedEmailDomains);
        var groups = string.Join(", ", provider.DefaultGroups.Select(group => group.Name).Order(StringComparer.Ordinal));

        return $"sign-up {(provider.AllowSignUp ? "on" : "off")}, allowed domains [{domains}], default Groups [{groups}]";
    }
}
