// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

namespace Authora.Core.Domain.Enums;

/// <summary>
///     Defines the Status of a Client.
/// </summary>
public enum ClientStatus
{
    /// <summary>
    ///     The Client is enabled and can obtain tokens.
    /// </summary>
    Enabled = 0,

    /// <summary>
    ///     The Client is disabled and cannot obtain new tokens.
    /// </summary>
    Disabled = 1,
}
