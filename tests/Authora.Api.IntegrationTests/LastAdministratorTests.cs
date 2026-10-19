// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

using System.Net;
using System.Net.Http.Headers;

using Authora.Core.Application.Authorization;

namespace Authora.Api.IntegrationTests;

/// <summary>
///     Checks that concurrent requests cannot remove the last administrator.
/// </summary>
/// <remarks>
///     Uses its own database, so no other test creates administrators that would be counted.
/// </remarks>
/// <param name="factory">The Api under test.</param>
public sealed class LastAdministratorTests(AuthoraApiFactory factory) : IClassFixture<AuthoraApiFactory>
{
    private const int Attempts = 10;

    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task ConcurrentDeactivationOfLastTwoAdministrators_KeepsOne()
    {
        // The caller is no administrator, so exactly the two administrators below exist.
        var callerId = await factory.CreateUserAsync(PermissionNames.Users.ManageAccess);
        var token = await factory.IssueAccessTokenAsync(callerId, [AdminApiScope.Name]);

        // Repeated, because a single run can pass by chance even without the lock.
        for (var attempt = 0; attempt < Attempts; attempt++)
        {
            var first = await factory.CreateAdministratorAsync();
            var second = await factory.CreateAdministratorAsync();

            var responses = await Task.WhenAll(DeactivateAsync(first, token), DeactivateAsync(second, token));

            Assert.Single(responses, response => response.StatusCode == HttpStatusCode.NoContent);
            Assert.Single(responses, response => response.StatusCode == HttpStatusCode.Conflict);

            foreach (var response in responses)
            {
                response.Dispose();
            }

            // Leaves no administrator who can sign in, so the next attempt starts with exactly two again.
            await factory.RemoveRolesAsync(first);
            await factory.RemoveRolesAsync(second);
        }
    }

    private async Task<HttpResponseMessage> DeactivateAsync(Guid userId, string accessToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, $"/api/users/{userId}/deactivate");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        return await _client.SendAsync(request, TestContext.Current.CancellationToken);
    }
}
