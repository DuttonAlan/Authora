// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

using System.Security.Cryptography;

using Authora.Core.Abstractions;
using Authora.Core.Application.Oidc;

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Authora.Infrastructure.Security;

/// <summary>
///     Loads the signing keys from the PEM files of <see cref="OidcOptions"/> once, when it is first used.
/// </summary>
/// <remarks>
///     The files are read only at startup, so rotating keys needs a restart. <see cref="OidcSigningKeysValidator"/>
///     loads them at startup too, so a missing or invalid file stops the start instead of failing the first request.
///     The provider owns the keys and disposes them when it is disposed.
/// </remarks>
public sealed class PemSigningKeyProvider : ISigningKeyProvider, IDisposable
{
    /// <summary>
    ///     Loads the keys.
    /// </summary>
    /// <param name="options">The OpenID Connect settings with the key files.</param>
    /// <param name="environment">The host environment, whose content root relative paths are resolved against.</param>
    /// <param name="logger">The Logger to record the loaded keys.</param>
    /// <exception cref="InvalidOperationException">Thrown when a key file is missing or invalid.</exception>
    public PemSigningKeyProvider(
        IOptions<OidcOptions> options,
        IHostEnvironment environment,
        ILogger<PemSigningKeyProvider> logger)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(environment);

        var settings = options.Value;

        SigningKey = Load(settings.SigningKeyPath, environment.ContentRootPath, mustSign: true);

        var publishedKeys = new List<SigningKey> { SigningKey };

        try
        {
            foreach (var path in settings.AdditionalKeyPaths)
            {
                var key = Load(path, environment.ContentRootPath, mustSign: false);

                if (publishedKeys.Any(existing => existing.KeyId == key.KeyId))
                {
                    key.Dispose();
                    throw new InvalidOperationException($"The key in '{path}' is configured more than once.");
                }

                publishedKeys.Add(key);
            }
        }
        catch
        {
            // The constructor fails, so nobody else can dispose the keys loaded so far.
            publishedKeys.ForEach(key => key.Dispose());
            throw;
        }

        PublishedKeys = publishedKeys;

        logger.SigningKeysLoaded(SigningKey.KeyId, PublishedKeys.Count);
    }

    /// <inheritdoc/>
    public SigningKey SigningKey { get; }

    /// <inheritdoc/>
    public IReadOnlyList<SigningKey> PublishedKeys { get; }

    /// <summary>
    ///     Disposes all keys.
    /// </summary>
    public void Dispose()
    {
        foreach (var key in PublishedKeys)
        {
            key.Dispose();
        }
    }

    private static SigningKey Load(string path, string contentRootPath, bool mustSign)
    {
        var fullPath = Path.Combine(contentRootPath, path);

        string pem;

        try
        {
            pem = File.ReadAllText(fullPath);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            throw new InvalidOperationException($"The key file '{fullPath}' cannot be read: {exception.Message}", exception);
        }

        RSA? rsa = RSA.Create();
        SigningKey key;

        try
        {
            rsa.ImportFromPem(pem);

            key = new SigningKey(rsa);
            rsa = null;
        }
        catch (Exception exception) when (exception is ArgumentException or CryptographicException)
        {
            // The message of ImportFromPem never contains key material.
            throw new InvalidOperationException(
                $"The key file '{fullPath}' must contain one unencrypted RSA key of at least {SigningKey.MinKeySize} bits in PEM format: {exception.Message}",
                exception);
        }
        finally
        {
            rsa?.Dispose();
        }

        if (mustSign && !key.CanSign)
        {
            key.Dispose();
            throw new InvalidOperationException(
                $"The key file '{fullPath}' contains only a public key, but the signing key needs the private key.");
        }

        return key;
    }
}
