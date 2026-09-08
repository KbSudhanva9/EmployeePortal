using EmployeePortal.Application.Interfaces;
using Serilog;
using System.Diagnostics;

namespace EmployeePortal.Infrastructure.HealthChecks;

public class HealthCheckService : IHealthCheckService
{
    private readonly ApplicationDbContext _dbContext;
    private readonly ISecretProvider _secretProvider;
    private readonly ILogger _logger;

    public HealthCheckService(
        ApplicationDbContext dbContext,
        ISecretProvider secretProvider,
        ILogger logger)
    {
        _dbContext = dbContext;
        _secretProvider = secretProvider;
        _logger = logger;
    }

    public async Task<HealthCheckResult> GetDatabaseHealthAsync()
    {
        var stopwatch = Stopwatch.StartNew();
        try
        {
            var canConnect = await _dbContext.Database.CanConnectAsync();
            stopwatch.Stop();

            if (!canConnect)
            {
                _logger.Warning("Database health check failed - cannot connect");
                return new HealthCheckResult
                {
                    Service = "Oracle Database",
                    IsHealthy = false,
                    Message = "Cannot establish connection to database",
                    CheckedAt = DateTime.UtcNow,
                    ResponseTimeMs = stopwatch.ElapsedMilliseconds
                };
            }

            _logger.Information("Database health check passed - response time {ResponseTime}ms", stopwatch.ElapsedMilliseconds);
            return new HealthCheckResult
            {
                Service = "Oracle Database",
                IsHealthy = true,
                Message = "Database connection successful",
                CheckedAt = DateTime.UtcNow,
                ResponseTimeMs = stopwatch.ElapsedMilliseconds
            };
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _logger.Error(ex, "Database health check failed with exception");
            return new HealthCheckResult
            {
                Service = "Oracle Database",
                IsHealthy = false,
                Message = $"Health check failed: {ex.Message}",
                CheckedAt = DateTime.UtcNow,
                ResponseTimeMs = stopwatch.ElapsedMilliseconds
            };
        }
    }

    public async Task<HealthCheckResult> GetVaultHealthAsync()
    {
        var stopwatch = Stopwatch.StartNew();
        try
        {
            await _secretProvider.GetSecretAsync("test-connection");
            stopwatch.Stop();

            _logger.Information("Vault health check passed - response time {ResponseTime}ms", stopwatch.ElapsedMilliseconds);
            return new HealthCheckResult
            {
                Service = "OCI Vault",
                IsHealthy = true,
                Message = "Vault connection successful",
                CheckedAt = DateTime.UtcNow,
                ResponseTimeMs = stopwatch.ElapsedMilliseconds
            };
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _logger.Warning(ex, "Vault health check failed - {Message}", ex.Message);
            return new HealthCheckResult
            {
                Service = "OCI Vault",
                IsHealthy = false,
                Message = $"Vault check failed: {ex.Message}",
                CheckedAt = DateTime.UtcNow,
                ResponseTimeMs = stopwatch.ElapsedMilliseconds
            };
        }
    }

    public async Task<HealthCheckResult> GetApplicationHealthAsync()
    {
        var stopwatch = Stopwatch.StartNew();
        try
        {
            stopwatch.Stop();
            _logger.Information("Application health check passed");
            return new HealthCheckResult
            {
                Service = "Application",
                IsHealthy = true,
                Message = "Application is healthy",
                CheckedAt = DateTime.UtcNow,
                ResponseTimeMs = stopwatch.ElapsedMilliseconds
            };
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _logger.Error(ex, "Application health check failed");
            return new HealthCheckResult
            {
                Service = "Application",
                IsHealthy = false,
                Message = $"Application check failed: {ex.Message}",
                CheckedAt = DateTime.UtcNow,
                ResponseTimeMs = stopwatch.ElapsedMilliseconds
            };
        }
    }

    public async Task<Dictionary<string, HealthCheckResult>> GetAllHealthAsync()
    {
        var results = new Dictionary<string, HealthCheckResult>();

        var appHealth = await GetApplicationHealthAsync();
        results["Application"] = appHealth;

        var dbHealth = await GetDatabaseHealthAsync();
        results["Database"] = dbHealth;

        var vaultHealth = await GetVaultHealthAsync();
        results["Vault"] = vaultHealth;

        return results;
    }
}
