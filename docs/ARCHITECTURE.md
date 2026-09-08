# Architecture Overview

## System Architecture

```
┌─────────────────────────────────────────────────────────────┐
│                    Client Browser                           │
│                  (Bootstrap 5 UI)                           │
└────────────────────────┬────────────────────────────────────┘
                         │ HTTPS
                         ▼
┌─────────────────────────────────────────────────────────────┐
│              ASP.NET Core 8 MVC Application                  │
│  ┌──────────────────────────────────────────────────────┐  │
│  │  Controllers (Presentation Layer)                    │  │
│  │  - DashboardController                              │  │
│  │  - EmployeeController                               │  │
│  │  - AccountController                                │  │
│  └──────────────────────────────────────────────────────┘  │
│  ┌──────────────────────────────────────────────────────┐  │
│  │  Views & Shared Components                          │  │
│  │  - _Layout.cshtml (Navigation & Styling)           │  │
│  │  - Employee CRUD Forms                              │  │
│  │  - Dashboard with Chart.js                          │  │
│  └──────────────────────────────────────────────────────┘  │
└──────────────────────────┬─────────────────────────────────┘
                           │
        ┌──────────────────┼──────────────────┐
        ▼                  ▼                  ▼
┌──────────────────┐  ┌──────────────────┐  ┌──────────────────┐
│   Application    │  │ Infrastructure   │  │  Persistence     │
│ Layer (DTOs,    │  │  Layer (Services)│  │  Layer (EF Core) │
│ Validators)      │  │                  │  │                  │
│                  │  │ - SecretProvider │  │ - DbContext      │
│ - EmployeeDto   │  │ - HealthCheck    │  │ - Repositories   │
│ - CreateEmployee │  │   Service        │  │ - Unit of Work   │
│ - UpdateEmployee │  │ - ServiceCollect.│  │                  │
│ - Validators    │  │   Ext.           │  │                  │
└──────────────────┘  └──────────────────┘  └──────────────────┘
        │                  │                  │
        └──────────────────┼──────────────────┘
                           ▼
                    ┌──────────────────┐
                    │  Domain Layer    │
                    │   (Entities)     │
                    │                  │
                    │ - Employee       │
                    │ - Department     │
                    └──────────────────┘
                           │
        ┌──────────────────┼──────────────────┐
        ▼                  ▼                  ▼
┌──────────────────┐  ┌──────────────────┐  ┌──────────────────┐
│   Oracle DB      │  │   OCI Vault      │  │  Serilog Logs    │
│                  │  │                  │  │                  │
│ - Employees      │  │ - Connection     │  │ - Console        │
│ - Departments    │  │   Strings        │  │ - File Rotation  │
│ - AspNetUsers    │  │ - Secrets        │  │ - Structured     │
│ - AspNetRoles    │  │ - Rotation       │  │                  │
└──────────────────┘  └──────────────────┘  └──────────────────┘
```

## Clean Architecture Layers

### 1. Domain Layer (EmployeePortal.Domain)
**Purpose**: Core business entities and domain logic

- **Entities**:
  - `Employee`: Represents an employee with personal and employment information
  - `Department`: Represents a department

**Characteristics**:
- No external dependencies
- Pure C# classes
- Contains only business rules
- Highest level of reusability

### 2. Application Layer (EmployeePortal.Application)
**Purpose**: Business logic, DTOs, and validators

- **DTOs**:
  - `EmployeeDto`: Read model for employee
  - `CreateEmployeeDto`: Create request
  - `UpdateEmployeeDto`: Update request

- **Validators**:
  - `CreateEmployeeValidator`: Fluent validation rules
  - `UpdateEmployeeValidator`: Fluent validation rules

- **Interfaces**:
  - `IEmployeeRepository`: Repository contract
  - `IUnitOfWork`: Transaction management
  - `ISecretProvider`: Secrets management
  - `IHealthCheckService`: System health monitoring

**Characteristics**:
- Depends only on Domain layer
- Contains all business validation logic
- Platform-independent

### 3. Infrastructure Layer (EmployeePortal.Infrastructure)
**Purpose**: External service implementations

**Components**:
- **Vault Service**:
  - `VaultConfiguration`: Configuration model
  - `SecretProvider`: OCI Vault integration with retry logic and caching
  
- **Health Checks**:
  - `HealthCheckService`: Monitors Database, Vault, and Application health
  
- **Dependency Injection**:
  - `ServiceCollectionExtensions`: Configures all services

**Characteristics**:
- Depends on Application and Domain layers
- Handles external systems
- Implements error handling and resilience

### 4. Persistence Layer (EmployeePortal.Persistence)
**Purpose**: Data access and database operations

**Components**:
- **ApplicationDbContext**: Entity Framework Core DbContext
  - Fluent API configuration
  - Shadow properties for audit
  - Soft delete implementation
  
- **Repositories**:
  - `EmployeeRepository`: Employee data access
  - `EmployeeRepository.IEmployeeRepository`
  
- **Unit of Work**:
  - `UnitOfWork`: Transaction management and repository coordination

**Characteristics**:
- Depends on Application, Domain, and Infrastructure layers
- All database operations go through repositories
- Supports transactions and rollback

### 5. Web Layer (EmployeePortal.Web)
**Purpose**: MVC presentation layer

**Controllers**:
- `DashboardController`: Dashboard and health checks
- `EmployeeController`: Employee CRUD operations
- `AccountController`: Authentication and authorization

**Views**:
- Razor templates with Bootstrap 5
- Responsive design
- Form validation client-side and server-side

**Shared Components**:
- `_Layout.cshtml`: Master layout with sidebar navigation
- `_ViewStart.cshtml`: Default layout assignment
- `_ViewImports.cshtml`: Common imports

**Characteristics**:
- Depends on all layers
- Handles HTTP requests/responses
- Implements security headers

## Data Flow

### Employee CRUD Operation

```
1. User Request
   ↓
2. EmployeeController.Create (HTTP POST)
   ├── Validate CSRF token
   ├── Validate input with CreateEmployeeValidator
   ├── Map DTO to Entity
   ↓
3. UnitOfWork.Employees (Repository)
   ├── Check for duplicates
   ├── Add to DbContext
   ↓
4. UnitOfWork.SaveChangesAsync()
   ├── BEGIN TRANSACTION
   ├── INSERT into EMPLOYEES table
   ├── COMMIT TRANSACTION
   ↓
5. Controller returns RedirectToAction
   ↓
6. View renders success message
```

### Secret Retrieval

```
1. Application Startup
   ↓
2. ServiceCollectionExtensions registers SecretProvider
   ↓
3. SecretProvider.GetSecretAsync("DefaultConnection")
   ├── Check cache (if valid TTL)
   │  ├── YES → Return cached value
   │  └── NO → Continue
   ├── Try user-secrets (Development)
   │  ├── FOUND → Cache and return
   │  └── NOT FOUND → Continue
   ├── Try OCI Vault (Production)
   │  ├── Retry logic with exponential backoff
   │  ├── Max 3 retries
   │  ├── FOUND → Cache and return
   │  └── FAILED → Throw exception
   ↓
4. DbContext connects using retrieved secret
```

## Security Architecture

### Authentication Flow
```
1. User Login
   ├── POST to /Account/Login
   ├── Username/Password validation
   ├── ASP.NET Identity check
   ├── Password hash verification
   ├── Lockout check (5 failed attempts)
   ├── Generate authentication cookie
   │  ├── Secure flag (HTTPS only)
   │  ├── HttpOnly flag (No JavaScript access)
   │  ├── SameSite=Strict (CSRF protection)
   ├── Return JWT or session
   ↓
2. Authorized Request
   ├── Middleware validates cookie/token
   ├── Extract claims and roles
   ├── Authorize based on [Authorize] attributes
   ├── Grant access or redirect to login
```

### Secret Management Flow
```
Development:
  .NET User Secrets
    ↓
  SecretProvider (cache)
    ↓
  Application

UAT/Production:
  OCI Vault
    ↓
  SecretProvider (retry + cache)
    ↓
  Application
    
Secret Rotation:
  1. Update in OCI Vault
  2. HealthCheck detects change
  3. Cache invalidation
  4. Next request fetches fresh value
  5. Zero-downtime transition
```

## Database Design

### Soft Delete Pattern
```sql
-- All tables include:
IS_DELETED NUMBER(1) DEFAULT 0

-- Queries filter by:
WHERE IS_DELETED = 0

-- Advantages:
- Preserves historical data
- Allows audit trails
- Simplifies recovery
- No cascade delete issues
```

### Audit Columns
```sql
-- All tables include:
CREATED_AT TIMESTAMP DEFAULT SYSTIMESTAMP
UPDATED_AT TIMESTAMP
CREATED_BY VARCHAR2(128)
UPDATED_BY VARCHAR2(128)

-- Tracking:
- Creation timestamp and user
- Last modification timestamp and user
- Business intelligence queries
```

### Indexes Strategy
```
Employee Table:
- EMPLOYEE_NUMBER (UNIQUE) - PK lookup
- EMAIL (UNIQUE) - Duplicate prevention
- DEPARTMENT - Filter by dept
- STATUS - Filter active/inactive
- IS_DELETED - Soft delete filter
- DATE_OF_JOINING - Range queries

Benefit:
- Fast searches
- Prevent duplicates
- Enable complex queries
- Optimize pagination
```

## Dependency Injection Container

```csharp
// Services registered in order:
1. Serilog Logger
2. FluentValidation validators
3. DbContext (Oracle)
4. IUnitOfWork → UnitOfWork
5. IEmployeeRepository → EmployeeRepository
6. ISecretProvider → SecretProvider
7. IHealthCheckService → HealthCheckService
8. ASP.NET Identity
9. CORS & HTTPS
10. MVC & Razor Pages
```

## Error Handling Strategy

```
Try-Catch Hierarchy:
├── Global Exception Middleware
│  ├── Logs error details via Serilog
│  ├── Returns generic error to user
│  ├── Returns detailed error in Development
│  └── Returns HTTP 500 in Production
│
├── Controller level
│  ├── Catches DbUpdateException
│  ├── Catches ValidationException
│  ├── Returns TempData with friendly message
│  └── Logs warning with details
│
└── Repository level
   ├── Catches DbException
   ├── Applies retry logic
   └── Logs and re-throws
```

## Performance Optimization

1. **Caching**:
   - Secret cache with 60-minute TTL
   - Prevents repeated OCI Vault calls

2. **Pagination**:
   - Default 10 items per page
   - Skip/Take in LINQ
   - Count query optimized with index

3. **Database Indexes**:
   - Strategic indexes on filter columns
   - Composite indexes for common queries

4. **Lazy Loading Prevention**:
   - Use Include() for required relationships
   - Explicit queries in repositories

5. **Connection Pooling**:
   - EF Core manages Oracle connection pool
   - Configurable pool size

## Monitoring & Observability

### Health Checks
```
Endpoint: /Dashboard/HealthCheck
Response:
{
  "Application": {
    "Service": "Application",
    "IsHealthy": true,
    "Message": "Application is healthy",
    "CheckedAt": "2024-01-01T12:00:00Z",
    "ResponseTimeMs": 5
  },
  "Database": { ... },
  "Vault": { ... }
}
```

### Structured Logging
```
Info: "Employee '{EmployeeNumber}' created successfully"
Warn: "Vault health check failed - {Message}"
Error: "Database health check failed with exception", exception
```

### Metrics Captured
- Response times
- Failed authentication attempts
- Secret cache hit rates
- Database query performance
- Vault API latency
