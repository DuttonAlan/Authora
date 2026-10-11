// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

using System.Text.Json.Serialization;

namespace Authora.Api.OAuth;

/// <summary>
///     An OAuth error response (RFC 6749, Section 5.2).
/// </summary>
/// <param name="Error">The error code, e.g. <c>invalid_grant</c>.</param>
/// <param name="ErrorDescription">A human-readable description of the error.</param>
internal sealed record OAuthErrorResponse(
    [property: JsonPropertyName("error")] string Error,
    [property: JsonPropertyName("error_description")] string ErrorDescription);
