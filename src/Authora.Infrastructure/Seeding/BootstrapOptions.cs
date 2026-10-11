// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

namespace Authora.Infrastructure.Seeding;

/// <summary>
///     Settings to create the first administrator (US-003), bound from the section <see cref="SectionName"/>.
/// </summary>
/// <remarks>
///     Only used while no administrator who can sign in exists. Set the password through an environment variable
///     or a secret store, not in a configuration file, and remove it once the administrator has signed in.
/// </remarks>
public sealed class BootstrapOptions
{
    /// <summary>
    ///     The configuration section of the settings.
    /// </summary>
    public const string SectionName = "Authora:Bootstrap";

    /// <summary>
    ///     The first administrator. Nothing is created if its Email is not set.
    /// </summary>
    public BootstrapAdministratorOptions Administrator { get; set; } = new();

    /// <summary>
    ///     The Client the administrator uses to get an Access Token for the admin API.
    ///     Only created together with the administrator, and only if it has Redirect URIs.
    /// </summary>
    public BootstrapClientOptions Client { get; set; } = new();
}

/// <summary>
///     Settings of the first administrator.
/// </summary>
public sealed class BootstrapAdministratorOptions
{
    /// <summary>
    ///     Email of the administrator, used to sign in.
    /// </summary>
    public string? Email { get; set; }

    /// <summary>
    ///     Initial password of the administrator. Required if <see cref="Email"/> is set.
    /// </summary>
    public string? Password { get; set; }

    /// <summary>
    ///     First Name of the administrator. Defaults to "Authora".
    /// </summary>
    public string FirstName { get; set; } = "Authora";

    /// <summary>
    ///     Last Name of the administrator. Defaults to "Administrator".
    /// </summary>
    public string LastName { get; set; } = "Administrator";
}

/// <summary>
///     Settings of the Client for the admin API.
/// </summary>
public sealed class BootstrapClientOptions
{
    /// <summary>
    ///     Name of the Client. Defaults to "Authora Admin".
    /// </summary>
    public string Name { get; set; } = "Authora Admin";

    /// <summary>
    ///     Redirect URIs of the Client. The Client is a Public Client that uses PKCE and is allowed the
    ///     <c>authora.admin</c> Scope.
    /// </summary>
    public IList<string> RedirectUris { get; set; } = [];
}
