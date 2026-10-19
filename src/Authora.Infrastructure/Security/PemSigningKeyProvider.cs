// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

using Authora.Core.Abstractions;
using Authora.Core.Application.Oidc;

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Authora.Infrastructure.Security;

/// <summary>
///     Loads the signing keys from the PEM files of <see cref="OidcOptions"/>, and loads them again when the files or
///     the settings change.
/// </summary>
/// <remarks>
///     <para>
///         The keys are loaded when the provider is created, so a missing or invalid file stops the start.
///         <see cref="OidcSigningKeysValidator"/> checks them at startup too. Afterwards
///         <see cref="SigningKeyReloadService"/> calls <see cref="Reload"/> every
///         <see cref="OidcOptions.KeyReloadInterval"/>. A reload that fails keeps the previous keys, so a half-written
///         file never stops sign-ins.
///     </para>
///     <para>
///         Replaced keys are only disposed with the provider, because a request may still be signing with them.
///         Rotations are rare, so they use little memory.
///     </para>
/// </remarks>
public sealed class PemSigningKeyProvider : ISigningKeyProvider, IDisposable
{
    private readonly IOptionsMonitor<OidcOptions> _options;
    private readonly string _contentRootPath;
    private readonly ILogger<PemSigningKeyProvider> _logger;
    private readonly Lock _reloadLock = new();
    private readonly List<SigningKeySet> _replacedSets = [];

    private volatile SigningKeySet _current;

    /// <summary>
    ///     Loads the keys.
    /// </summary>
    /// <param name="options">The OpenID Connect settings with the key files.</param>
    /// <param name="environment">The host environment, whose content root relative paths are resolved against.</param>
    /// <param name="logger">The Logger to record the loaded keys.</param>
    /// <exception cref="InvalidOperationException">Thrown when a key file is missing or invalid.</exception>
    public PemSigningKeyProvider(
        IOptionsMonitor<OidcOptions> options,
        IHostEnvironment environment,
        ILogger<PemSigningKeyProvider> logger)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(environment);
        ArgumentNullException.ThrowIfNull(logger);

        _options = options;
        _contentRootPath = environment.ContentRootPath;
        _logger = logger;

        var files = SigningKeySet.ReadFiles(options.CurrentValue, _contentRootPath);
        _current = SigningKeySet.Parse(files);

        WarnIfReadableByOthers(files[0].FullPath);
        logger.SigningKeysLoaded(SigningKey.KeyId, PublishedKeys.Count);
    }

    /// <inheritdoc/>
    public SigningKey SigningKey => _current.SigningKey;

    /// <inheritdoc/>
    public IReadOnlyList<SigningKey> PublishedKeys => _current.PublishedKeys;

    /// <summary>
    ///     The reason the last reload failed, or <see langword="null"/> if it succeeded. Reported by the health check.
    /// </summary>
    public string? ReloadFailure { get; private set; }

    /// <summary>
    ///     Loads the key files again and uses the new keys if a file or a path changed.
    /// </summary>
    /// <returns>True if the keys changed.</returns>
    /// <exception cref="InvalidOperationException">
    ///     Thrown when a key file is missing or invalid. The previous keys stay in use.
    /// </exception>
    /// <exception cref="OptionsValidationException">
    ///     Thrown when the changed settings are invalid. The previous keys stay in use.
    /// </exception>
    public bool Reload()
    {
        lock (_reloadLock)
        {
            try
            {
                var files = SigningKeySet.ReadFiles(_options.CurrentValue, _contentRootPath);

                if (SigningKeySet.ComputeFingerprint(files) == _current.Fingerprint)
                {
                    ReloadFailure = null;
                    return false;
                }

                var loaded = SigningKeySet.Parse(files);

                _replacedSets.Add(_current);
                _current = loaded;
                ReloadFailure = null;

                WarnIfReadableByOthers(files[0].FullPath);
                _logger.SigningKeysReloaded(SigningKey.KeyId, PublishedKeys.Count);

                return true;
            }
            catch (Exception exception) when (exception is InvalidOperationException or OptionsValidationException)
            {
                ReloadFailure = exception.Message;
                throw;
            }
        }
    }

    /// <summary>
    ///     Disposes all keys, including replaced ones.
    /// </summary>
    public void Dispose()
    {
        lock (_reloadLock)
        {
            _current.Dispose();
            _replacedSets.ForEach(set => set.Dispose());
            _replacedSets.Clear();
        }
    }

    // Only the owner of Authora should be able to read the private key. Windows uses ACLs, which are not checked.
    private void WarnIfReadableByOthers(string path)
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        var mode = File.GetUnixFileMode(path);

        if ((mode & (UnixFileMode.GroupRead | UnixFileMode.OtherRead | UnixFileMode.GroupWrite | UnixFileMode.OtherWrite)) != 0)
        {
            _logger.SigningKeyFileReadableByOthers(path);
        }
    }
}
