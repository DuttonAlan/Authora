// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

using Authora.Core.Abstractions;
using Authora.Core.Domain.Entities;

namespace Authora.Core.Application.Clients;

/// <summary>
///     Generates a new Client Secret and adds its hash to the Client.
/// </summary>
/// <param name="secretGenerator">The generator for the plain-text Secret.</param>
/// <param name="secretHasher">The hasher for the stored Secret.</param>
public sealed class ClientSecretIssuer(ISecretGenerator secretGenerator, ISecretHasher secretHasher)
{
    /// <summary>
    ///     Generates a new Secret and adds its hash to the Client. The Client still has to be saved.
    /// </summary>
    /// <param name="client">The Confidential Client to add the Secret to.</param>
    /// <param name="expiresAt">Point in time (UTC) when the Secret expires, or null if it never expires.</param>
    /// <returns>The new Secret including its plain-text value.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the Client is not a Confidential Client.</exception>
    public IssuedClientSecretResponse Issue(Client client, DateTime? expiresAt)
    {
        ArgumentNullException.ThrowIfNull(client);

        var plainTextSecret = secretGenerator.Generate();
        var secret = client.AddSecret(secretHasher.Hash(plainTextSecret), expiresAt);

        return new IssuedClientSecretResponse(secret.Id, plainTextSecret, secret.CreatedAt, secret.ExpiresAt);
    }
}
