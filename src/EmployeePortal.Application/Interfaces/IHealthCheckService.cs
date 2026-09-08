namespace EmployeePortal.Application.Interfaces;

public interface IHealthCheckService
{
    Task<HealthCheckResult> GetDatabaseHealthAsync();
    Task<HealthCheckResult> GetVaultHealthAsync();
    Task<HealthCheckResult> GetApplicationHealthAsync();
    Task<Dictionary<string, HealthCheckResult>> GetAllHealthAsync();
}

public class HealthCheckResult
{
    public string Service { get; set; } = string.Empty;
    public bool IsHealthy { get; set; }
    public string Message { get; set; } = string.Empty;
    public DateTime CheckedAt { get; set; }
    public long? ResponseTimeMs { get; set; }
}
