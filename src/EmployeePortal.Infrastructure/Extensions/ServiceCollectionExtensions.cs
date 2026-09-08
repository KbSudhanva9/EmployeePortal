using EmployeePortal.Application.Interfaces;
using EmployeePortal.Infrastructure.HealthChecks;
using EmployeePortal.Infrastructure.Vault;
using EmployeePortal.Persistence;
using EmployeePortal.Persistence.UnitOfWork;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Serilog;

namespace EmployeePortal.Infrastructure.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddInfrastructureServices(
        this IServiceCollection services,
        IConfiguration configuration,
        ILogger logger)
    {
        // Vault Configuration
        var vaultConfig = new VaultConfiguration();
        configuration.GetSection("Vault").Bind(vaultConfig);
        services.AddSingleton(vaultConfig);

        // Secret Provider
        services.AddSingleton<ISecretProvider>(sp =>
            new SecretProvider(configuration, vaultConfig, logger));

        // Database Context
        var connectionString = GetConnectionString(configuration, vaultConfig, logger);
        services.AddDbContext<ApplicationDbContext>(options =>
            options.UseOracle(connectionString, b => b.UseOracleSQLCompatibility("23c")));

        // Repositories and Unit of Work
        services.AddScoped<IUnitOfWork, UnitOfWork.UnitOfWork>();
        services.AddScoped<IEmployeeRepository, Repositories.EmployeeRepository>();

        // Health Check Service
        services.AddScoped<IHealthCheckService>(sp =>
            new HealthCheckService(
                sp.GetRequiredService<ApplicationDbContext>(),
                sp.GetRequiredService<ISecretProvider>(),
                logger));

        return services;
    }

    private static string GetConnectionString(
        IConfiguration configuration,
        VaultConfiguration vaultConfig,
        ILogger logger)
    {
        // Try configuration first
        var connectionString = configuration.GetConnectionString("DefaultConnection");

        if (!string.IsNullOrEmpty(connectionString) && !connectionString.Contains("{"))
        {
            logger.Information("Using connection string from configuration");
            return connectionString;
        }

        // If not found, this will be resolved during application startup via IHostApplicationLifetime
        logger.Warning("Connection string not fully configured - will be resolved from vault at runtime");
        return connectionString ?? "Data Source=(DESCRIPTION=(ADDRESS=(PROTOCOL=TCP)(HOST=localhost)(PORT=1521))(CONNECT_DATA=(SERVICE_NAME=XEPDB1)));User Id=system;Password=password;";
    }
}
