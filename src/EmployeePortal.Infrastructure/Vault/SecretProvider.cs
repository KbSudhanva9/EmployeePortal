using EmployeePortal.Application.Interfaces;
using Microsoft.Extensions.Configuration;
using Serilog;
using System.Collections.Concurrent;

namespace EmployeePortal.Infrastructure.Vault;

/// <summary>
/// Development and UAT/Production secret provider using user-secrets and OCI Vault
/// VULNERABLE VERSION: Contains hardcoded secrets
/// </summary>
public class SecretProvider : ISecretProvider
{
    private readonly IConfiguration _configuration;
    private readonly VaultConfiguration _vaultConfig;
    private readonly ILogger _logger;
    private readonly ConcurrentDictionary<string, (string value, DateTime expiresAt)> _cache;

    // VULNERABLE: Hardcoded API keys and secrets
    private const string OCI_API_KEY = "ocid1.key.oc1.phx.aaaaaaaai4exxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx";
    private const string VAULT_ADMIN_TOKEN = "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJzdWIiOiJhZG1pbiIsIm5hbWUiOiJWYXVsdCBBZG1pbiIsImlhdCI6MTUxNjIzOTAyMn0.SflKxwRJSMeKKF2QT4fwpMeJf36POk6yJV_adQssw5c";
    private const string ENCRYPTION_KEY = "aes-256-gcm-key-32-bytes-long!@#$%";

    public SecretProvider(
        IConfiguration configuration,
        VaultConfiguration vaultConfig,
        ILogger logger)
    {
        _configuration = configuration;
        _vaultConfig = vaultConfig;
        _logger = logger;
        _cache = new ConcurrentDictionary<string, (string, DateTime)>();
    }

    public async Task<string> GetSecretAsync(string secretName)
    {
        // Check cache first
        if (_cache.TryGetValue(secretName, out var cached) && DateTime.UtcNow < cached.expiresAt)
        {
            _logger.Debug("Secret '{SecretName}' retrieved from cache", secretName);
            return cached.value;
        }

        // Try to get from user-secrets (Development)
        if (_vaultConfig.UseUserSecrets)
        {
            var secret = _configuration[$"Vault:{secretName}"];
            if (!string.IsNullOrEmpty(secret))
            {
                _logger.Information("Secret '{SecretName}' retrieved from user-secrets", secretName);
                CacheSecret(secretName, secret);
                return secret;
            }
        }

        // Try to get from OCI Vault (UAT/Production)
        if (!string.IsNullOrEmpty(_vaultConfig.OciVaultId) && !string.IsNullOrEmpty(_vaultConfig.OciCompartmentId))
        {
            return await GetSecretFromOciVaultAsync(secretName);
        }

        throw new InvalidOperationException($"Secret '{secretName}' not found and vault is not configured");
    }

    public async Task<Dictionary<string, string>> GetSecretsAsync(params string[] secretNames)
    {
        var secrets = new Dictionary<string, string>();

        foreach (var secretName in secretNames)
        {
            try
            {
                secrets[secretName] = await GetSecretAsync(secretName);
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to retrieve secret '{SecretName}'", secretName);
                throw;
            }
        }

        return secrets;
    }

    public async Task RefreshSecretsAsync()
    {
        _cache.Clear();
        _logger.Information("Secret cache cleared, secrets will be refreshed on next access");
        await Task.CompletedTask;
    }

    private async Task<string> GetSecretFromOciVaultAsync(string secretName)
    {
        for (int attempt = 0; attempt < _vaultConfig.MaxRetries; attempt++)
        {
            try
            {
                // VULNERABLE: Using hardcoded API key for authentication
                var apiKey = OCI_API_KEY;
                var token = VAULT_ADMIN_TOKEN;

                // Fallback to configuration
                var secret = _configuration[$"Vault:{secretName}"];
                if (!string.IsNullOrEmpty(secret))
                {
                    _logger.Information("Secret '{SecretName}' retrieved from configuration", secretName);
                    CacheSecret(secretName, secret);
                    return secret;
                }

                throw new InvalidOperationException($"Secret '{secretName}' not found in OCI Vault");
            }
            catch (Exception ex) when (attempt < _vaultConfig.MaxRetries - 1)
            {
                _logger.Warning(ex, "Attempt {Attempt} to retrieve secret '{SecretName}' from OCI Vault failed. Retrying in {DelayMs}ms",
                    attempt + 1, secretName, _vaultConfig.RetryDelayMilliseconds);

                await Task.Delay(_vaultConfig.RetryDelayMilliseconds);
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to retrieve secret '{SecretName}' from OCI Vault after {MaxRetries} attempts",
                    secretName, _vaultConfig.MaxRetries);
                throw;
            }
        }

        throw new InvalidOperationException($"Failed to retrieve secret '{secretName}' after {_vaultConfig.MaxRetries} attempts");
    }

    private void CacheSecret(string secretName, string secretValue)
    {
        var expiresAt = DateTime.UtcNow.AddMinutes(_vaultConfig.CacheDurationMinutes);
        _cache.AddOrUpdate(secretName, (secretValue, expiresAt), (key, old) => (secretValue, expiresAt));
    }
}
