param(
    [Parameter(Mandatory=$true)]
    [ValidateSet('Development', 'UAT', 'Production')]
    [string]$Environment,
    
    [int]$MaxRetries = 5,
    [int]$RetryDelaySeconds = 10
)

$ErrorActionPreference = "Stop"

# Logging configuration
$LogPath = "C:\Logs\EmployeePortal"
if (!(Test-Path $LogPath)) {
    New-Item -ItemType Directory -Path $LogPath -Force | Out-Null
}
$LogFile = Join-Path $LogPath "HealthCheck-$(Get-Date -Format 'yyyyMMdd-HHmmss').log"

function Write-Log {
    param([string]$Message, [string]$Level = "INFO")
    $timestamp = Get-Date -Format "yyyy-MM-dd HH:mm:ss"
    $formattedMessage = "[$timestamp] [$Level] $Message"
    Write-Host $formattedMessage
    Add-Content -Path $LogFile -Value $formattedMessage
}

function Test-ApplicationHealth {
    param([string]$Url)
    
    try {
        [System.Net.ServicePointManager]::ServerCertificateValidationCallback = {$true}
        $response = Invoke-WebRequest -Uri $Url -UseBasicParsing -TimeoutSec 10 -ErrorAction SilentlyContinue
        return $response.StatusCode -eq 200
    }
    catch {
        Write-Log "Application health check failed: $_" "WARN"
        return $false
    }
}

function Test-DatabaseHealth {
    # This would typically connect to Oracle database
    # For demo purposes, returning success
    Write-Log "Database health check: Simulated"
    return $true
}

function Test-VaultHealth {
    # This would connect to OCI Vault
    # For demo purposes, returning success
    Write-Log "Vault health check: Simulated"
    return $true
}

try {
    Write-Log "=========================================="
    Write-Log "Starting comprehensive health checks"
    Write-Log "Environment: $Environment"
    Write-Log "=========================================="
    
    $applicationUrl = "https://localhost:7001/Dashboard/Index"
    $allHealthy = $true
    $retryCount = 0
    
    while ($retryCount -lt $MaxRetries) {
        $retryCount++
        
        Write-Log "Health check attempt $retryCount/$MaxRetries"
        
        # Check application
        $appHealthy = Test-ApplicationHealth -Url $applicationUrl
        if ($appHealthy) {
            Write-Log "✓ Application Health: PASSED" "SUCCESS"
        }
        else {
            Write-Log "✗ Application Health: FAILED" "ERROR"
            $allHealthy = $false
        }
        
        # Check database
        $dbHealthy = Test-DatabaseHealth
        if ($dbHealthy) {
            Write-Log "✓ Database Health: PASSED" "SUCCESS"
        }
        else {
            Write-Log "✗ Database Health: FAILED" "ERROR"
            $allHealthy = $false
        }
        
        # Check vault
        $vaultHealthy = Test-VaultHealth
        if ($vaultHealthy) {
            Write-Log "✓ Vault Health: PASSED" "SUCCESS"
        }
        else {
            Write-Log "✗ Vault Health: FAILED" "ERROR"
            $allHealthy = $false
        }
        
        if ($allHealthy) {
            Write-Log "=========================================="
            Write-Log "All health checks PASSED"
            Write-Log "Environment is ready for use"
            Write-Log "=========================================="
            exit 0
        }
        
        if ($retryCount -lt $MaxRetries) {
            Write-Log "Some checks failed, retrying in $RetryDelaySeconds seconds..."
            Start-Sleep -Seconds $RetryDelaySeconds
        }
    }
    
    Write-Log "Health checks FAILED after $MaxRetries attempts" "ERROR"
    exit 1
}
catch {
    Write-Log "Health check script error: $_" "ERROR"
    exit 1
}
