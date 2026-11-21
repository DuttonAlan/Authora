// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

namespace Authora.Core.Application.Consents.Check;

/// <summary>
///     The result of <see cref="CheckConsentService"/>. An enum instead of a boolean, so the meaning cannot be read the
///     wrong way round; the default value is the safe one.
/// </summary>
public enum ConsentCheckResult
{
    /// <summary>
    ///     The consent screen must be shown: no stored Consent covers the requested Scopes, or the request contains
    ///     <c>prompt=consent</c>.
    /// </summary>
    Required = 0,

    /// <summary>
    ///     The User need not be asked: the Client is first-party, or a stored Consent covers the requested Scopes.
    /// </summary>
    NotRequired = 1
}
