# DevSecOps Demonstration Guide

## Overview

This guide demonstrates a complete DevSecOps implementation for the Employee Portal application. The demonstration includes intentional vulnerabilities on the `feature/devsecops-demo` branch that are detected by security scanning tools, and their resolution on the `main` branch.

## Demo Stages

### Stage 1: Create Demo Branch with Vulnerabilities (30-45 minutes)

#### 1.1 Create Feature Branch
```bash
git checkout -b feature/devsecops-demo
```

#### 1.2 Introduce Hardcoded Secrets
Add to `appsettings.json`:
```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Data Source=(DESCRIPTION=(ADDRESS=(PROTOCOL=TCP)(HOST=prod-db.company.com)(PORT=1521))(CONNECT_DATA=(SERVICE_NAME=PRODDB)));User Id=produser;Password=Pr0d#Pass123!;"
  },
  "Vault": {
    "UseUserSecrets": false,
    "OciVaultPassword": "vault_admin_password_12345"
  }
}
```

#### 1.3 Introduce SQL Injection Vulnerability
Modify `src/EmployeePortal.Persistence/Repositories/EmployeeRepository.cs`:
```csharp
// VULNERABLE CODE - DO NOT USE IN PRODUCTION
public async Task<IEnumerable<Employee>> GetByDepartmentVulnerable(string department)
{
    // SQL Injection vulnerability
    var sql = $"SELECT * FROM EMPLOYEES WHERE DEPARTMENT = '{department}'";
    return await _context.Employees.FromSqlRaw(sql).ToListAsync();
}
```

#### 1.4 Use Outdated NuGet Package
Modify `src/EmployeePortal.Web/EmployeePortal.Web.csproj`:
```xml
<PackageReference Include="Newtonsoft.Json" Version="11.0.1" />  <!-- VULNERABLE VERSION -->
<PackageReference Include="log4net" Version="2.0.8" />  <!-- VULNERABLE VERSION -->
```

#### 1.5 Add Hardcoded API Keys
Add to `src/EmployeePortal.Infrastructure/Vault/SecretProvider.cs`:
```csharp
// VULNERABLE CODE
private const string OCI_API_KEY = "ocid1.key.oc1..aaaaaaaa1234567890abcdefghijklmnopqrstuvwxyz";
private const string ADMIN_TOKEN = "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJzdWIiOiIxMjM0NTY3ODkwIiwibmFtZSI6IkFkbWluIiwiaWF0IjoxNTE2MjM5MDIyfQ.SflKxwRJSMeKKF2QT4fwpMeJf36POk6yJV_adQssw5c";
```

#### 1.6 Commit and Push
```bash
git add .
git commit -m "Add employee management features (with demo vulnerabilities)"
git push origin feature/devsecops-demo
```

### Stage 2: Create Pull Request and Trigger Pipeline (5-10 minutes)

#### 2.1 Create Pull Request
```bash
# On GitHub UI or via GitHub CLI
gh pr create --title "Feature: Employee Management Demo" \
             --body "Demonstrates DevSecOps scanning" \
             --base main \
             --head feature/devsecops-demo
```

#### 2.2 Observe Pipeline Execution

**Expected Pipeline Results:**

```
Pipeline: Feature/DevSecOps Demo
├── ✅ Restore (PASSED) - 2m
├── ✅ Build (PASSED) - 3m
├── ✅ Unit Tests (PASSED) - 2m
├── ✅ SonarQube SAST (PASSED) - 4m
│  └─ Issues Found:
│     - SQL Injection in EmployeeRepository.cs line 85
│     - Code Quality: 45 issues
├── ❌ GitLeaks (FAILED) - 1m
│  └─ Secrets Detected:
│     - OCI API Key in SecretProvider.cs
│     - Database password in appsettings.json
│     - Admin token in SecretProvider.cs
├── ⚠️  Microsoft Security DevOps (BLOCKED) - 2m
│  └─ Findings:
│     - Secrets exposure
│     - Weak cryptography
├── ❌ OWASP Dependency Check (FAILED) - 3m
│  └─ Vulnerabilities Found:
│     - CVE-2019-0943 (Newtonsoft.Json 11.0.1)
│     - CVE-2015-5287 (log4net 2.0.8)
│     - 4 Medium/High severity issues
├── ❌ OWASP ZAP DAST (FAILED)
│  └─ Security Issues:
│     - Weak security headers
│     - Exposed sensitive data
└── ❌ Security Gate (FAILED)
    └─ Action Required:
       - Fix all CRITICAL issues
       - Update dependencies
       - Remove hardcoded secrets
```

### Stage 3: Demonstrate Vulnerability Detection (5-10 minutes)

#### 3.1 GitLeaks Detection

**Show Detection Output:**
```bash
$ gitleaks detect --source feature/devsecops-demo

    ⚠️  WARNING: engine running in verification mode
    
    Finding:     {
                    "Description": "OCI API Key",
                    "StartLine": 23,
                    "EndLine": 23,
                    "StartColumn": 51,
                    "EndColumn": 119,
                    "Secret": "ocid1.key.oc1..aaaaaaaa1234567890abcdefghijklmnopqrstuvwxyz",
                    "File": "src/EmployeePortal.Infrastructure/Vault/SecretProvider.cs",
                    "Commit": "abc1234567890def",
                    "Match": "private const string OCI_API_KEY = \"ocid1.key.oc1..aaaaaaaa1234567890abcdefghijklmnopqrstuvwxyz\";",
                    "Entropy": 4.52,
                    "Author": "Developer",
                    "Email": "dev@example.com",
                    "Message": "Add employee management features",
                    "Date": "2024-01-15"
                }
                
    Finding:     {
                    "Description": "Database Credentials",
                    "File": "src/EmployeePortal.Web/appsettings.json",
                    "Secret": "Pr0d#Pass123!",
                    "Match": "Password=Pr0d#Pass123!;"
                }
```

**Key Findings:**
- 3 hardcoded secrets detected
- API keys exposed
- Database credentials in configuration
- Entropy analysis identifies suspicious patterns

#### 3.2 SonarQube SAST Detection

**Show SonarQube Dashboard:**
- SQL Injection in EmployeeRepository.cs (Line 85)
- Missing input validation
- Code smell: Code duplication
- Security hotspot: Weak encryption

#### 3.3 OWASP Dependency Check

**Show Vulnerability Report:**
```
Dependency-Check Report
========================

newtonsoft.json-11.0.1
├── CVE-2019-0943: Deserialization RCE
├── CVSS Score: 8.1 (High)
├── Description: Newtonsoft.Json does not use the correct base class for validation
└── Recommendation: Update to 13.0.1 or later

log4net-2.0.8
├── CVE-2015-5287: Denial of Service
├── CVSS Score: 5.5 (Medium)
├── Description: Unsafe deserialization
└── Recommendation: Update to 2.0.10 or later

Total Vulnerabilities: 6
├── Critical: 0
├── High: 2
├── Medium: 3
└── Low: 1
```

### Stage 4: Fix Vulnerabilities (20-30 minutes)

#### 4.1 Remove Hardcoded Secrets

**Remove from appsettings.json:**
```bash
git checkout main -- src/EmployeePortal.Web/appsettings.json
```

**Remove from SecretProvider.cs:**
```bash
# Remove all hardcoded API keys and tokens
# Use only configuration-based secrets
```

#### 4.2 Fix SQL Injection

**Update EmployeeRepository.cs:**
```csharp
// FIXED CODE - Parameterized Query
public async Task<IEnumerable<Employee>> GetByDepartment(string department)
{
    return await _context.Employees
        .Where(e => e.Department == department && !e.IsDeleted)
        .OrderBy(e => e.EmployeeNumber)
        .ToListAsync();
}
```

#### 4.3 Update Vulnerable Dependencies

**Update EmployeePortal.Web.csproj:**
```xml
<!-- Remove vulnerable versions -->
<!-- Add updated safe versions -->
<PackageReference Include="Newtonsoft.Json" Version="13.0.3" />
<PackageReference Include="log4net" Version="2.0.15" />
```

#### 4.4 Commit Fixes

```bash
git add .
git commit -m "Fix: Remove hardcoded secrets, patch SQL injection, update dependencies"
git push origin main
```

### Stage 5: Verify Fixes with Pipeline (10-15 minutes)

#### 5.1 Pipeline Execution on Main

```
Pipeline: Main Branch Security Verification
├── ✅ Restore (PASSED)
├── ✅ Build (PASSED)
├── ✅ Unit Tests (PASSED)
├── ✅ SonarQube SAST (PASSED) - All security issues resolved
├── ✅ GitLeaks (PASSED) - No secrets detected
├── ✅ Microsoft Security DevOps (PASSED)
├── ✅ OWASP Dependency Check (PASSED) - All vulnerable packages patched
├── ✅ OWASP ZAP DAST (PASSED)
├── ✅ Security Gate (PASSED)
├── ✅ Deploy to Development
├── ✅ Health Checks (PASSED)
├── ✅ Manual Approval (APPROVED)
├── ✅ Deploy to UAT
└── ✅ Post-Deployment Verification (PASSED)
```

### Stage 6: Demonstrate Secret Rotation (5-10 minutes)

#### 6.1 OCI Vault Secret Update

```bash
# Update secret in OCI Vault (simulated)
oci secrets secret update \
  --secret-id ocid1.secret.oc1..xxxxxxxxxxxxx \
  --secret-content-details secretContentType=BASE64,content=$(echo -n "NewSecretValue" | base64)
```

#### 6.2 Application Detects Change

**Application logs show:**
```
[INFO] Secret cache invalidated for 'DefaultConnection'
[INFO] Attempting to retrieve secret from OCI Vault
[INFO] Secret 'DefaultConnection' retrieved from OCI Vault (Attempt 1/3)
[INFO] Secret cached with TTL: 60 minutes
[WARN] Database connection established with new secret
```

#### 6.3 Zero-Downtime Verification

**Health check endpoint:**
```bash
curl -k https://localhost:7001/Dashboard/HealthCheck

{
  "Application": {
    "IsHealthy": true,
    "Service": "Application"
  },
  "Database": {
    "IsHealthy": true,
    "Service": "Oracle Database",
    "ResponseTimeMs": 12
  },
  "Vault": {
    "IsHealthy": true,
    "Service": "OCI Vault",
    "ResponseTimeMs": 145
  }
}
```

### Stage 7: Demonstrate Rollback (5 minutes)

#### 7.1 Trigger Rollback Scenario

**Simulate deployment failure:**
```powershell
# On deployment server
.\deployment\scripts\Rollback.ps1 -Version "1.0.0" -Environment "Production"
```

**Rollback script execution:**
```
[INFO] Starting rollback process...
[INFO] Backup location: C:\IIS\EmployeePortal\backups\v1.0.0_20240115_120000
[INFO] Stopping IIS Application Pool: EmployeePortal
[INFO] Removing failed deployment from C:\inetpub\EmployeePortal
[INFO] Restoring previous version from backup
[INFO] Starting IIS Application Pool: EmployeePortal
[INFO] Executing health checks...
[✓] Health check passed
[INFO] Rollback completed successfully
```

## Key Demonstration Points

### 1. Shift-Left Security
- Security issues detected before deployment
- Immediate feedback in CI/CD pipeline
- Cost-effective remediation

### 2. Multi-Layer Security Scanning
- SAST (SonarQube): Code analysis
- Secrets Scan (GitLeaks): Credential detection
- SCA (OWASP Dep-Check): Dependency vulnerabilities
- DAST (OWASP ZAP): Runtime vulnerabilities

### 3. Secret Management
- Zero hardcoded secrets
- Runtime retrieval from secure vault
- Automatic caching and refresh
- Zero-downtime rotation

### 4. Infrastructure as Code
- Reproducible deployments
- Automated testing
- Rollback capability
- Audit trail

### 5. Health & Monitoring
- Real-time service health
- Database connectivity verification
- Vault availability checks
- Performance metrics

## Timeline for Complete Demo

| Stage | Duration | Key Actions |
|-------|----------|------------|
| Setup | 5m | Branch creation, initial code |
| Vulnerabilities | 10m | Add secrets, injection, outdated deps |
| Pipeline Execution | 10m | Trigger CI/CD, observe failures |
| Vulnerability Analysis | 10m | Review GitLeaks, SonarQube, Dep-Check |
| Remediation | 15m | Fix issues, update dependencies |
| Verification | 10m | Run pipeline, confirm all pass |
| Secret Rotation | 5m | Update vault, show zero-downtime |
| Rollback Demo | 5m | Trigger and verify rollback |
| **Total** | **70 minutes** | Complete end-to-end demo |

## Live Demo Checklist

- [ ] GitHub repository cloned and ready
- [ ] Azure DevOps pipeline configured
- [ ] Self-hosted agent online and healthy
- [ ] SonarQube server accessible
- [ ] GitLeaks installed on agent
- [ ] OWASP Dependency Check ready
- [ ] OWASP ZAP configured for DAST
- [ ] Oracle database available
- [ ] OCI Vault configured (or simulation)
- [ ] IIS deployment scripts tested
- [ ] All security tools licensed/activated
- [ ] Demo credentials prepared
- [ ] Presentation slides ready
- [ ] Recording software ready (optional)

## Troubleshooting

### Pipeline Stage Failures

**SonarQube Timeout:**
```bash
# Check SonarQube logs
tail -f /opt/sonarqube/logs/sonarqube.log

# Increase timeout in azure-pipelines.yml
- task: SonarQubePrepare@5
  inputs:
    timeoutInMinutes: 30  # Increase if needed
```

**GitLeaks False Positives:**
```bash
# Update .gitleaksignore file
gitleaks detect --verbose --baseline main
```

**Dependency Check Slow:**
```bash
# Use cached database
dependency-check \
  --project "EmployeePortal" \
  --scan . \
  --database /var/cache/owasp-dependency-check/database
```

## Security Best Practices Demonstrated

✅ No secrets in source control  
✅ Parameterized database queries  
✅ Dependency vulnerability scanning  
✅ Automated security gates  
✅ Code quality analysis  
✅ Runtime health monitoring  
✅ Secret rotation support  
✅ Audit logging  
✅ HTTPS enforcement  
✅ CSRF protection  
