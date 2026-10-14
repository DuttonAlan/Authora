// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

using System.Security.Cryptography;
using System.Text;

using Authora.Core.Application.Oidc;

namespace Authora.Infrastructure.Security;

/// <summary>
///     The signing key and the published keys, loaded together from the PEM files of <see cref="OidcOptions"/>.
/// </summary>
/// <remarks>
///     A set is never changed: a reload loads a new set and swaps it in, so a request always sees a consistent set.
/// </remarks>
internal sealed class SigningKeySet : IDisposable
{
    private SigningKeySet(SigningKey signingKey, IReadOnlyList<SigningKey> publishedKeys, string fingerprint)
    {
        SigningKey = signingKey;
        PublishedKeys = publishedKeys;
        Fingerprint = fingerprint;
    }

    /// <summary>
    ///     The key that signs new ID Tokens.
    /// </summary>
    public SigningKey SigningKey { get; }

    /// <summary>
    ///     <see cref="SigningKey"/> first, then the keys that are only published.
    /// </summary>
    public IReadOnlyList<SigningKey> PublishedKeys { get; }

    /// <summary>
    ///     A hash of the paths and contents of the key files, to detect changed files without parsing them.
    /// </summary>
    public string Fingerprint { get; }

    /// <summary>
    ///     Reads the key files of the settings.
    /// </summary>
    /// <param name="settings">The settings with the paths.</param>
    /// <param name="contentRootPath">The directory relative paths are resolved against.</param>
    /// <returns>The full paths and contents, the signing key first.</returns>
    /// <exception cref="InvalidOperationException">Thrown when a file cannot be read.</exception>
    public static IReadOnlyList<KeyFile> ReadFiles(OidcOptions settings, string contentRootPath)
    {
        return [.. new[] { settings.SigningKeyPath }
            .Concat(settings.AdditionalKeyPaths)
            .Select(path => Read(Path.Combine(contentRootPath, path)))];
    }

    /// <summary>
    ///     Computes the fingerprint of key files, see <see cref="Fingerprint"/>.
    /// </summary>
    /// <param name="files">The files, the signing key first.</param>
    /// <returns>The fingerprint.</returns>
    public static string ComputeFingerprint(IReadOnlyList<KeyFile> files)
    {
        var builder = new StringBuilder();

        foreach (var file in files)
        {
            builder.Append(file.FullPath).Append('\n').Append(file.Pem).Append('\n');
        }

        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(builder.ToString())));
    }

    /// <summary>
    ///     Parses key files into a set.
    /// </summary>
    /// <param name="files">The files, the signing key first.</param>
    /// <returns>The set. The caller owns it and must dispose it.</returns>
    /// <exception cref="InvalidOperationException">
    ///     Thrown when a file has no valid RSA key, the signing key has no private part, or a key is configured twice.
    /// </exception>
    public static SigningKeySet Parse(IReadOnlyList<KeyFile> files)
    {
        var keys = new List<SigningKey>();

        try
        {
            for (var index = 0; index < files.Count; index++)
            {
                var key = Parse(files[index], mustSign: index == 0);

                if (keys.Any(existing => existing.KeyId == key.KeyId))
                {
                    key.Dispose();
                    throw new InvalidOperationException($"The key in '{files[index].FullPath}' is configured more than once.");
                }

                keys.Add(key);
            }
        }
        catch
        {
            // Nobody else can dispose the keys loaded so far.
            keys.ForEach(key => key.Dispose());
            throw;
        }

        return new SigningKeySet(keys[0], keys, ComputeFingerprint(files));
    }

    /// <summary>
    ///     Disposes all keys of the set.
    /// </summary>
    public void Dispose()
    {
        foreach (var key in PublishedKeys)
        {
            key.Dispose();
        }
    }

    private static KeyFile Read(string fullPath)
    {
        try
        {
            return new KeyFile(fullPath, File.ReadAllText(fullPath));
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            throw new InvalidOperationException($"The key file '{fullPath}' cannot be read: {exception.Message}", exception);
        }
    }

    private static SigningKey Parse(KeyFile file, bool mustSign)
    {
        RSA? rsa = RSA.Create();
        SigningKey key;

        try
        {
            rsa.ImportFromPem(file.Pem);

            key = new SigningKey(rsa);
            rsa = null;
        }
        catch (Exception exception) when (exception is ArgumentException or CryptographicException)
        {
            // The message of ImportFromPem never contains key material.
            throw new InvalidOperationException(
                $"The key file '{file.FullPath}' must contain one unencrypted RSA key of at least {SigningKey.MinKeySize} bits in PEM format: {exception.Message}",
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
                $"The key file '{file.FullPath}' contains only a public key, but the signing key needs the private key.");
        }

        return key;
    }

    /// <summary>
    ///     The content of a key file.
    /// </summary>
    /// <param name="FullPath">The full path of the file.</param>
    /// <param name="Pem">The PEM content. Contains the private key, so it is never logged.</param>
    internal sealed record KeyFile(string FullPath, string Pem)
    {
        /// <summary>
        ///     Returns the path only, so the key is not written to logs by accident.
        /// </summary>
        /// <returns>A description of the file.</returns>
        public override string ToString() => $"KeyFile({FullPath})";
    }
}
