# Deployment Guide

## Overview

This guide covers deployment of the Employee Portal application across Development, UAT, and Production environments using Azure DevOps pipelines and PowerShell deployment scripts.

## Architecture

```
Developer → GitHub Push
    ↓
Azure DevOps Trigger
    ↓
Build Pipeline (16 Stages)
    ├── Restore & Build
    ├── Unit Tests
    ├── Security Scanning (SAST/SCA/DAST)
    ├── Artifact Creation
    └── Stage Gate
    ↓
Development Deployment (Automatic)
    ├── Stop IIS Site
    ├── Backup Current Version
    ├── Deploy New Code
    ├── Start IIS Site
    └── Health Checks
    ↓
UAT Deployment (Manual Approval)
    └── Same process as Development
    ↓
Production (Manual Approval - Not in Demo)
```

## Prerequisites

### System Requirements
- **Server OS**: Windows Server 2019 or later
- **IIS**: Version 10.0 or later
- **.NET**: 8.0 or later
- **PowerShell**: 5.1 or later (for deployment scripts)
- **Disk Space**: Minimum 20GB for application and backups

### Software Installation

#### 1. Install .NET 8 Runtime
```powershell
# Download and install .NET 8 Runtime
# https://dotnet.microsoft.com/download/dotnet/8.0

# Verify installation
dotnet --version
```

#### 2. Enable IIS Features
```powershell
# Run as Administrator
Enable-WindowsOptionalFeature -Online -FeatureName IIS-WebServerRole
Enable-WindowsOptionalFeature -Online -FeatureName IIS-WebServer
Enable-WindowsOptionalFeature -Online -FeatureName IIS-CommonHttpFeatures
Enable-WindowsOptionalFeature -Online -FeatureName IIS-HealthAndDiagnostics
Enable-WindowsOptionalFeature -Online -FeatureName IIS-Performance
Enable-WindowsOptionalFeature -Online -FeatureName IIS-Security
Enable-WindowsOptionalFeature -Online -FeatureName IIS-ApplicationDevelopment
Enable-WindowsOptionalFeature -Online -FeatureName IIS-NetFxExtensibility45
Enable-WindowsOptionalFeature -Online -FeatureName IIS-Asp
Enable-WindowsOptionalFeature -Online -FeatureName IIS-ASPNET45
Enable-WindowsOptionalFeature -Online -FeatureName IIS-WebSockets
Enable-WindowsOptionalFeature -Online -FeatureName NetFx4Extended-ASPNET45
```

#### 3. Install URL Rewrite Module
```powershell
# Download from Microsoft
# https://www.iis.net/downloads/microsoft/url-rewrite

# Install using msiexec
msiexec /i rewrite_amd64_en-US.msi /quiet /norestart
```

#### 4. Install Application Request Routing (ARR)
```powershell
# Optional: For load balancing across multiple servers
# https://www.iis.net/downloads/microsoft/application-request-routing
```

### Directory Structure

Create the following directory structure:

```powershell
# Run as Administrator
$dirs = @(
    "C:\inetpub\EmployeePortal",
    "C:\inetpub\EmployeePortal-UAT",
    "C:\IIS\Backups\EmployeePortal",
    "C:\IIS\Backups\EmployeePortal-UAT",
    "C:\Logs\EmployeePortal"
)

foreach ($dir in $dirs) {
    New-Item -ItemType Directory -Path $dir -Force | Out-Null
    Write-Host "Created: $dir"
}
```

## Azure DevOps Pipeline Configuration

### 1. Setup Self-Hosted Agent

```powershell
# On the deployment server
cd C:\DevOps\Agent

# Download agent
# https://github.com/microsoft/azure-pipelines-agent/releases

# Extract and configure
.\config.cmd --url https://dev.azure.com/YOUR_ORG `
             --auth pat `
             --token YOUR_PAT `
             --pool YOUR_POOL `
             --agent DEPLOYMENT-SERVER-01 `
             --acceptTeeEula

# Install as Windows Service
.\config.cmd
```

### 2. Configure Service Connection

In Azure DevOps:
1. Project Settings → Service Connections
2. Create new service connection
3. Select "Deployment machine group"
4. Add deployment servers
5. Test connection

### 3. Create Pipeline Variable Groups

```yaml
# Development Variables
- VaultEnvironment: Development
- IISAppPoolName: EmployeePortal
- IISSiteName: EmployeePortal
- DeploymentPath: C:\inetpub\EmployeePortal
- LogPath: C:\Logs\EmployeePortal

# UAT Variables
- VaultEnvironment: UAT
- IISAppPoolName: EmployeePortal-UAT
- IISSiteName: EmployeePortal-UAT
- DeploymentPath: C:\inetpub\EmployeePortal-UAT
- LogPath: C:\Logs\EmployeePortal
```

## IIS Configuration

### 1. Create Application Pool

```powershell
# Create app pool for Development
New-IISAppPool -Name "EmployeePortal" `
    -ManagedRuntimeVersion "v4.0" `
    -ManagedPipelineMode "Integrated" `
    -Force

# Configure app pool
$appPool = Get-IISAppPool -Name "EmployeePortal"
$appPool.ProcessModel.IdentityType = "ApplicationPoolIdentity"
$appPool.Recycling.PeriodicRestart.Time = [TimeSpan]::FromHours(24)
$appPool | Set-IISAppPool

# Start app pool
Start-IISAppPool -Name "EmployeePortal"
```

### 2. Create Website

```powershell
# Create website binding
New-IISSite -Name "EmployeePortal" `
    -BindingInformation "*:443:localhost" `
    -PhysicalPath "C:\inetpub\EmployeePortal" `
    -ApplicationPool "EmployeePortal" `
    -Protocol "https" `
    -Force

# Add HTTP binding for redirect
New-IISBinding -Name "EmployeePortal" `
    -BindingInformation "*:80:localhost" `
    -Protocol "http"

# Start website
Start-IISSite -Name "EmployeePortal"
```

### 3. Configure SSL/HTTPS

```powershell
# Import certificate (or create self-signed for development)
$cert = New-SelfSignedCertificate -DnsName "localhost", "*.employeeportal.local" `
    -CertStoreLocation "cert:\LocalMachine\My"

# Bind certificate to website
New-IISBinding -Name "EmployeePortal" `
    -Protocol "https" `
    -BindingInformation "*:443:" `
    -CertificateThumbprint $cert.Thumbprint `
    -CertStoreLocation "cert:\LocalMachine\My" `
    -Force
```

### 4. Configure web.config

Create or update `C:\inetpub\EmployeePortal\web.config`:

```xml
<?xml version="1.0" encoding="utf-8"?>
<configuration>
  <system.webServer>
    <!-- HTTPS Redirect -->
    <rewrite>
      <rules>
        <rule name="HTTP to HTTPS" stopProcessing="true">
          <match url="(.*)" />
          <conditions>
            <add input="{HTTPS}" pattern="^OFF$" />
          </conditions>
          <action type="Redirect" url="https://{HTTP_HOST}/{R:1}" redirectType="Permanent" />
        </rule>
      </rules>
    </rewrite>

    <!-- Security Headers -->
    <httpProtocol>
      <customHeaders>
        <add name="Strict-Transport-Security" value="max-age=31536000; includeSubDomains" />
        <add name="X-Content-Type-Options" value="nosniff" />
        <add name="X-Frame-Options" value="DENY" />
        <add name="X-XSS-Protection" value="1; mode=block" />
        <add name="Referrer-Policy" value="strict-origin-when-cross-origin" />
        <add name="Permissions-Policy" value="geolocation=(), microphone=(), camera=()" />
      </customHeaders>
    </httpProtocol>

    <!-- Compression -->
    <urlCompression doStatic="true" doDynamic="true" />

    <!-- Request Filtering -->
    <security>
      <requestFiltering>
        <fileExtensions>
          <add fileExtension=".exe" allowed="false" />
          <add fileExtension=".bat" allowed="false" />
          <add fileExtension=".cmd" allowed="false" />
          <add fileExtension=".com" allowed="false" />
        </fileExtensions>
      </requestFiltering>
    </security>
  </system.webServer>
</configuration>
```

## Deployment Process

### 1. Automatic Development Deployment

**Triggered automatically after:**
- All security gates pass
- Build artifacts available
- No manual approval required

**Steps:**
```powershell
# Deployment script execution
.\Deploy-IIS.ps1 -SiteName "EmployeePortal" `
                 -Environment "Development" `
                 -ArtifactPath "\\build-server\builds\EmployeePortal-latest"
```

### 2. Manual UAT Deployment

**Requires approval from:**
- DevOps Lead
- QA Manager
- Security Officer (for Production)

**Approval checklist:**
- [ ] All unit tests passed
- [ ] Security scanning completed
- [ ] No critical/high vulnerabilities
- [ ] Code review approved
- [ ] Change request documented
- [ ] Rollback plan verified

**Steps:**
```powershell
# Manual deployment with logging
$deployParams = @{
    SiteName = "EmployeePortal-UAT"
    Environment = "UAT"
    ArtifactPath = "\\build-server\builds\EmployeePortal-1.0.0"
}

.\Deploy-IIS.ps1 @deployParams
```

### 3. Rollback Procedure

**Automatic rollback triggers:**
- Health check failures
- HTTP response codes != 200
- Database connectivity issues
- Exception during deployment

**Manual rollback:**
```powershell
# Initiate rollback
.\Rollback.ps1 -Environment "UAT" `
               -SiteName "EmployeePortal-UAT" `
               -PreviousVersion "C:\IIS\Backups\EmployeePortal-UAT\Backup-20240115-120000"
```

**Verification:**
```powershell
# Run health checks after rollback
.\HealthCheck.ps1 -Environment "UAT" `
                  -MaxRetries 5 `
                  -RetryDelaySeconds 10
```

## OCI Vault Integration

### 1. Setup OCI Vault

```bash
# Create vault
oci vault vault create \
  --compartment-id ocid1.compartment.oc1..xxxxx \
  --display-name "EmployeePortal-Vault" \
  --vault-type VIRTUAL_PRIVATE

# Create encryption key
oci kms key create \
  --compartment-id ocid1.compartment.oc1..xxxxx \
  --display-name "EmployeePortal-Key" \
  --key-shape-algorithm RSA \
  --key-shape-length 4096
```

### 2. Store Secrets

```bash
# Database connection string
oci secrets secret create \
  --compartment-id ocid1.compartment.oc1..xxxxx \
  --vault-id ocid1.vault.oc1..xxxxx \
  --secret-name "DefaultConnection" \
  --secret-content-type "BASE64" \
  --secret-content "Data Source=prod-db.company.com:1521/PRODDB;User Id=appuser;Password=SecurePassword123!;"

# SMTP credentials
oci secrets secret create \
  --compartment-id ocid1.compartment.oc1..xxxxx \
  --vault-id ocid1.vault.oc1..xxxxx \
  --secret-name "SmtpPassword" \
  --secret-content "SmtpPassword123!"
```

### 3. Configure Application for OCI Vault

Update `appsettings.Production.json`:

```json
{
  "Vault": {
    "UseUserSecrets": false,
    "OciCompartmentId": "ocid1.compartment.oc1..xxxxx",
    "OciVaultId": "ocid1.vault.oc1..xxxxx",
    "OciAuthFilePath": "/home/app/.oci/config",
    "OciProfile": "DEFAULT",
    "CacheDurationMinutes": 60,
    "MaxRetries": 3,
    "RetryDelayMilliseconds": 1000
  }
}
```

### 4. Secret Rotation

**Procedure:**
1. Update secret in OCI Vault
2. Application cache TTL expires
3. Next request fetches fresh secret
4. Application reconnects with new credentials
5. Zero downtime transition

```bash
# Update secret (rotate password)
oci secrets secret update \
  --secret-id ocid1.secret.oc1..xxxxx \
  --secret-content-details secretContentType=BASE64,content=$(echo -n "NewPassword456!" | base64)

# Verify update
oci secrets secret get --secret-id ocid1.secret.oc1..xxxxx
```

## Monitoring & Logs

### 1. Application Logs

```
Location: C:\Logs\EmployeePortal\
Format: ApplicationLog-yyyyMMdd.txt
Structure: [Timestamp] [Level] Message
```

### 2. Deployment Logs

```
Location: C:\Logs\EmployeePortal\Deploy-*.log
Events: Backup, Stop, Deploy, Start, Health Check
```

### 3. Health Check Logs

```
Location: C:\Logs\EmployeePortal\HealthCheck-*.log
Checks: Application, Database, Vault connectivity
```

### 4. View IIS Logs

```powershell
# Application logs
Get-Content C:\Logs\EmployeePortal\*.log | Select-String "ERROR|WARNING"

# IIS logs
Get-Content C:\inetpub\logs\LogFiles\W3SVC1\*.log | Select-String "400|500"
```

## Troubleshooting

### Issue: Deployment Timeout

**Solution:**
```powershell
# Increase timeout in azure-pipelines.yml
timeoutInMinutes: 30

# Manually stop site and retry
Stop-IISSite -Name "EmployeePortal"
Start-Sleep -Seconds 10
.\Deploy-IIS.ps1 -SiteName "EmployeePortal" -Environment "Development" -ArtifactPath "..."
```

### Issue: Health Check Failing

**Check:**
1. Application pool running
2. Port 443 accessible
3. Certificate valid
4. DNS resolution

```powershell
# Restart application pool
Restart-IISAppPool -Name "EmployeePortal"

# Test connectivity
[System.Net.ServicePointManager]::ServerCertificateValidationCallback = {$true}
Invoke-WebRequest -Uri "https://localhost:7001/Dashboard" -UseBasicParsing
```

### Issue: Vault Connection Failed

**Check:**
1. OCI credentials valid
2. Network connectivity to vault
3. Firewall rules allow outbound HTTPS

```powershell
# Test OCI Vault connectivity
$vaultUrl = "https://vault.us-phoenix-1.oci.oraclecloud.com"
Invoke-WebRequest -Uri $vaultUrl -UseBasicParsing
```

## Rollback Strategy

### Version Control
- Each deployment creates timestamped backup
- Backups retained for 30 days minimum
- Critical production backups retained indefinitely

### Rollback Triggers
- Automatic: Health check failure
- Manual: Operator decision
- Scheduled: Planned maintenance rollback

### RTO/RPO Targets
- **RTO (Recovery Time Objective)**: < 5 minutes
- **RPO (Recovery Point Objective)**: < 1 minute

## Change Management

### Required Approvals
1. **Development**: Automatic after security gate
2. **UAT**: QA Manager + Security Officer
3. **Production**: All of above + Release Manager + Business Owner

### Change Log
All deployments logged with:
- Timestamp
- Environment
- Version
- Approver
- Artifacts deployed
- Rollback status

## Checklist for Production Deployment

- [ ] All tests passing
- [ ] Security scanning complete - no critical findings
- [ ] Performance testing completed
- [ ] Capacity planning verified
- [ ] Backup strategy tested
- [ ] Rollback procedure validated
- [ ] Monitoring and alerting configured
- [ ] Documentation updated
- [ ] Stakeholders notified
- [ ] Maintenance window scheduled
- [ ] Support team briefed
- [ ] Post-deployment validation plan ready
