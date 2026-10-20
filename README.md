# Inventory API with ASP.NET Core

A REST API for managing products, categories, and stock movements, built with ASP.NET Core on .NET 10, Entity Framework Core, and SQL Server. It helps track inventory changes while preventing concurrent requests from silently overwriting the same product's stock.

Products start with zero stock. Inventory is updated through recorded movements: `Entrada` adds units, `Salida` removes units when enough stock is available, and `Ajuste` sets the stock to a specified positive quantity. Each movement is associated with a product and an authenticated user.

The project demonstrates REST endpoint design, JWT authentication, role-based authorization, relational persistence, optimistic concurrency, automated testing, and container validation. Earlier work with Terraform, Amazon ECR, Amazon EKS, and Kubernetes is preserved as part of the project's technical evolution.

## Features

- Product and category management, including product deactivation.
- Inventory movements with validation for positive quantities, active products, and sufficient stock for outgoing movements.
- User registration and login with BCrypt password hashing and JWT tokens.
- Administrator-only category creation, updates, and deletion. Registration assigns the `User` role; it does not provision an administrator.
- SQL Server `rowversion` concurrency tokens and HTTP `409 Conflict` responses.
- OpenAPI documentation and Swagger UI in the Development environment.
- Automated backend tests and Docker smoke tests in GitHub Actions.

## Tech Stack

| Area | Technologies used |
| --- | --- |
| API | C#, ASP.NET Core Web API, .NET 10 |
| Persistence | Entity Framework Core 10, SQL Server, EF Core migrations |
| Authentication | JWT Bearer, BCrypt.Net-Next, `User` and `Admin` roles |
| API documentation | ASP.NET Core OpenAPI, Microsoft.OpenApi, Swashbuckle Swagger UI |
| Testing | xUnit, EF Core InMemory, SQL Server integration tests |
| Local containers | Docker, Docker Compose, SQL Server 2022 container |
| Automated validation | GitHub Actions: Backend CI and Docker CI |
| Previous cloud implementation | Terraform, Amazon ECR, Amazon EKS, Kubernetes, Amazon EBS CSI |
| Optional local Kubernetes | Minikube, with the manifest adjustments described below |

AWS infrastructure is not currently provisioned because of operating costs. Cloud configuration remains available for reference and optional future use; AWS workflows require manual execution.

## Project Structure

```text
.
|-- ApiInventario/
|   |-- Controllers/
|   |-- Data/
|   |-- DTOs/
|   |-- Migrations/
|   |-- Models/
|   |-- OpenApi/
|   |-- Properties/launchSettings.json
|   |-- Services/
|   |-- ApiInventario.csproj
|   |-- Program.cs
|   |-- appsettings.json
|   `-- appsettings.Development.json
|-- ApiInventario.Tests/
|   |-- ProductoServiceTests.cs
|   |-- MovimientoServiceTests.cs
|   |-- ConcurrencyControllerTests.cs
|   |-- SqlServerConcurrencyTests.cs
|   |-- SqlServerTestSafety.cs
|   |-- SqlServerTestSafetyTests.cs
|   |-- ApiInventario.Tests.csproj
|   `-- README.md
|-- .github/workflows/
|   |-- backend-ci.yml
|   |-- docker-ci.yml
|   |-- cd-ecr.yml
|   `-- deploy-eks.yml
|-- infra/terraform/
|-- k8s/
|-- ApiInventario.slnx
|-- Dockerfile
|-- docker-compose.yml
`-- README.md
```

The solution contains both the API and the test project. There is currently no `.env.example` file.

## Run Locally with SQL Server

Kubernetes and AWS are not required to run the API. Install the .NET 10 SDK and provide access to SQL Server, for example a local SQL Server Express instance on Windows.

Run commands from the repository root. The examples below use PowerShell.

### Configuration

`ConnectionStrings:DefaultConnection` and `Jwt:Key` are intentionally empty in [appsettings.json](ApiInventario/appsettings.json). Supply them through development User Secrets or environment variables; do not commit real credentials.

For a new, separate development database, replace the placeholders before running:

```powershell
dotnet user-secrets set "ConnectionStrings:DefaultConnection" 'Server=YOUR_COMPUTER\SQLEXPRESS;Database=InventarioDevDB;Integrated Security=True;Encrypt=True;TrustServerCertificate=True' --project ApiInventario/ApiInventario.csproj
dotnet user-secrets set "Jwt:Key" '<replace-with-a-random-secret-of-at-least-32-bytes>' --project ApiInventario/ApiInventario.csproj
```

The Windows identity running the API needs appropriate SQL permissions. `TrustServerCertificate=True` is a local development convenience. The configured issuer and audience are `ApiInventario` and `ApiInventarioClient`.

User Secrets are associated with this project's `UserSecretsId` and loaded in Development. Environment variables such as `ConnectionStrings__DefaultConnection` and `Jwt__Key` override the corresponding settings. A `.env` file is not loaded automatically by the native ASP.NET Core process.

### Build and Start

**Review the migration behavior below before starting the application.** By default, startup can create a database and apply pending migrations if the SQL account has permission. Use a new development database rather than pointing an unreviewed migration at an existing database.

```powershell
dotnet restore ApiInventario.slnx
dotnet build ApiInventario.slnx --configuration Release
dotnet run --project ApiInventario/ApiInventario.csproj --launch-profile https
```

Open Swagger at [https://localhost:7057](https://localhost:7057). The HTTPS profile also listens on HTTP port 5107, but use HTTPS for Swagger so the page and its OpenAPI request share the same origin. If needed, trust the development certificate with `dotnet dev-certs https --trust`.

Register or log in through `/api/Auth/register` or `/api/Auth/login`, then use the returned token in Swagger's **Authorize** dialog. Category writes require an `Admin` account provisioned separately; a newly registered `User` receives `403 Forbidden` for those operations. Obtain a new token after changing a user's role.

## Run with Docker Compose

Install Docker with support for Linux containers. Create an untracked `.env` file in the repository root and replace these placeholders with local development secrets:

```dotenv
MSSQL_SA_PASSWORD=<replace-with-a-strong-SQL-Server-password>
JWT_KEY=<replace-with-a-random-secret-of-at-least-32-bytes>
```

The SQL password must satisfy SQL Server's password policy. `.env` is ignored by Git. These variable names are consumed by [docker-compose.yml](docker-compose.yml); Compose passes the JWT key and a SQL connection string to the API.

```powershell
docker compose up -d --build
docker compose logs -f apiinventario
```

| Component | Configuration |
| --- | --- |
| API service | `apiinventario`, container `apiinventario-api` |
| API URL | `http://localhost:8080`, with Swagger enabled in Development |
| SQL service | `sqlserver`, container `apiinventario-sqlserver` |
| SQL image | `mcr.microsoft.com/mssql/server:2022-latest` |
| API-to-SQL connection | `sqlserver:1433`, database `InventarioDb`, SQL authentication as `sa` |
| Host SQL port | `1433`; must be available on the host |
| Persistent storage | Named volume `sqlserver-data`, mounted at `/var/opt/mssql` |

This SQL instance is separate from a Windows SQL Server Express instance, even if both contain a database named `InventarioDb`. Compose uses development connection options with `Encrypt=False` and `TrustServerCertificate=true`.

Compose starts SQL Server before the API but does not wait for database readiness. The API retries startup migration failures up to ten times, with five seconds between attempts. Migrations remain enabled by default.

Check the HTTP endpoint:

```powershell
curl.exe --fail http://localhost:8080/health
```

Expected response: `Healthy`. This endpoint does not execute a SQL query and does not establish that database operations work.

Stop the stack while retaining its named volume:

```powershell
docker compose down
```

`docker compose down -v` also deletes the named SQL volume and its stored data. Use it only when that data can be discarded. Changing `MSSQL_SA_PASSWORD` does not itself rotate the password of a SQL instance already initialized on an existing volume.

## Database Migrations

[Program.cs](ApiInventario/Program.cs) calls `Database.Migrate()` at startup unless `Database:AutoMigrate` is `false`.

| Migration | Change |
| --- | --- |
| `20260410083258_InitialCreate` | Creates categories, users, products, inventory movements, indexes, and relationships. |
| `20261020011035_AddProductoRowVersion` | Adds the SQL Server `rowversion` column to `Productos`. |

For native execution, disable automatic migrations in the current PowerShell session with:

```powershell
$env:Database__AutoMigrate = 'false'
```

This setting prevents startup migrations; it does not create or repair the schema. A database used with the current model must already contain `Productos.RowVersion` and the expected schema. Compose does not forward this variable from the host as currently written; disabling migrations there requires an explicit local override. The Kubernetes ConfigMap explicitly enables them.

Do not apply the initial migration blindly to a database that already contains application tables but lacks `__EFMigrationsHistory`: EF will consider the migration pending and attempt to create existing objects. Back up valuable data and reconcile schema and history before applying changes. Neither migration files nor a successful build prove that a particular database has been migrated.

## Optimistic Concurrency Control

Two requests can read the same stock before either saves. Without a version check, the later update could overwrite the earlier update and make the recorded movements inconsistent with the final stock.

The implementation uses the following sequence:

1. `Producto.RowVersion` is a byte array mapped with `.IsRowVersion()` in [ApiInventarioDbContext](ApiInventario/Data/ApiInventarioDbContext.cs). SQL Server generates an eight-byte token on insert and changes it on updates.
2. EF Core keeps the original token when a product is loaded and includes it in the update condition.
3. If another operation has changed that row, the original token no longer matches. EF throws `DbUpdateConcurrencyException` instead of silently overwriting the new state.
4. [MovimientoService](ApiInventario/Services/MovimientoService.cs) changes the tracked product and adds its movement in one `SaveChangesAsync`. The SQL transaction rolls back the rejected operation, including its movement record.
5. Stock is explicitly marked as modified even for an `Ajuste` that retains the same quantity, so that operation also checks the product version.
6. Product and movement controllers catch the concurrency exception and return HTTP `409 Conflict`, without internal exception details or automatic retries. The caller should retrieve the current state before deciding whether to retry.

The SQL Server tests verify that the accepted operation changes the token, the stale operation is rejected, the final stock is correct, and exactly one movement is persisted. Controller tests separately verify the HTTP response.

**Scope:** the product request/response DTOs do not expose a version token or ETag. This protects operations between the server's entity read and save; it does not implement client-supplied version validation for a form loaded in an earlier request. The current tests do not establish concurrency behavior for every endpoint or every business rule.

## Testing

| Test group | What it checks |
| --- | --- |
| `ProductoServiceTests` | Zero initial stock and rejection of zero product prices. |
| `MovimientoServiceTests` | Incoming stock changes and rejection of outgoing movements with insufficient stock. |
| `ConcurrencyControllerTests` | HTTP 409 for movement creation, product updates, and deactivation; no internal detail leakage or retries. |
| `SqlServerTestSafetyTests` | Connection validation and generated database names, without opening SQL connections. |
| `SqlServerConcurrencyTests` | Real SQL Server rowversion checks and atomic persistence for `Entrada`, `Salida`, and an unchanged-stock `Ajuste`. |

Service and controller tests use EF Core InMemory. The controller tests inject a concurrency exception; InMemory alone does not prove SQL Server versioning or transaction behavior.

### Run without SQL Server

Remove the integration variable from the current session before reproducing the normal CI behavior:

```powershell
Remove-Item Env:INVENTARIO_TEST_SQLSERVER -ErrorAction SilentlyContinue
dotnet restore ApiInventario.slnx
dotnet build ApiInventario.slnx --configuration Release --no-restore
dotnet test ApiInventario.Tests/ApiInventario.Tests.csproj --configuration Release --no-build --no-restore
```

Without the variable, the integration theory is reported as skipped. Alternatively, explicitly exclude SQL integration regardless of environment configuration:

```powershell
dotnet test ApiInventario.Tests/ApiInventario.Tests.csproj --configuration Release --filter 'FullyQualifiedName!~ApiInventario.Tests.SqlServerConcurrencyTests'
```

### Run SQL Server Integration Tests

Use a SQL Server instance dedicated to testing. The account needs database creation privileges and `VIEW ANY DATABASE`. Configure the variable externally with a connection whose initial catalog is `master`; for example, using Windows authentication against a dedicated local test instance:

```powershell
$env:INVENTARIO_TEST_SQLSERVER = 'Server=YOUR_COMPUTER\TEST_INSTANCE;Database=master;Integrated Security=True;Encrypt=True;TrustServerCertificate=True'
dotnet test ApiInventario.Tests/ApiInventario.Tests.csproj --configuration Release --filter 'FullyQualifiedName~SqlServerConcurrencyTests' --logger 'console;verbosity=detailed'
Remove-Item Env:INVENTARIO_TEST_SQLSERVER -ErrorAction SilentlyContinue
```

Replace the example instance with your actual test destination. Do not use production connections or commit a credential-bearing connection string.

Each of the three cases creates a new `InventarioConcurrencyTests_<GUID>` database and applies migrations only there. The tests reject attached-file connections and `User Instance=true`, check database visibility and name availability, and create the database explicitly before migrating. They use two contexts that read the same version, followed by a third context that verifies persisted results.

**Test databases are retained, including after failures.** These tests do not delete them automatically; inspect their logged names and manage cleanup separately. See [the detailed testing guide](ApiInventario.Tests/README.md).

### Recorded CI Results

The maintainer reported the following verified GitHub Actions Backend CI execution for the concurrency/CI update:

```text
Passed:  23
Failed:   0
Skipped:  1
Total:   24
```

The skipped item is the SQL integration theory, which contains three cases when enabled. These numbers describe that specific execution, not a permanent test count or a guarantee about future runs. A run URL was not supplied for this documentation update.

The maintainer also reported a successful Docker CI run: image construction, container startup, and a `Healthy` response. These results do not establish SQL connectivity or successful migrations.

## GitHub Actions

| Workflow | Current trigger | Purpose |
| --- | --- | --- |
| [Backend CI](.github/workflows/backend-ci.yml) | Push to `main` and pull requests targeting `main` | Restore, build API and tests, run automated tests. |
| [Docker CI](.github/workflows/docker-ci.yml) | Push to `main` and pull requests targeting `main` | Build `inventory-api:ci`, start it, check `/health`, and remove the test container. |
| [CD to ECR](.github/workflows/cd-ecr.yml) | Manual `workflow_dispatch` only | Build and publish an image to Amazon ECR when requested. |
| [Deploy to EKS](.github/workflows/deploy-eks.yml) | Manual `workflow_dispatch` only | Apply Kubernetes configuration to an available EKS cluster when requested. |

Backend CI installs .NET 10 and restores/builds the test project, which references the API. This compiles both projects in the solution; the workflow does not invoke `dotnet build ApiInventario.slnx` directly. Tests then run in Release with `--no-build --no-restore`. The workflow supplies neither SQL Server nor `INVENTARIO_TEST_SQLSERVER`.

Docker CI uses the root Dockerfile, disables migrations with `Database__AutoMigrate=false`, and does not start SQL Server. It checks application startup and HTTP availability, not database persistence or OpenAPI rendering. Its image is not pushed to a registry.

A push to `main` does **not** publish to ECR or trigger EKS deployment. The earlier automatic AWS triggers remain commented out. Manually publishing an ECR image does not automatically run the EKS workflow either. The workflows do not run Terraform or gate publication on ECR scan findings.

## Previous Cloud Implementation

The project previously implemented this architecture as a learning and portfolio exercise:

```text
Terraform -> AWS network, IAM, ECR, EKS, and storage integration
GitHub Actions -> Docker image -> Amazon ECR
Amazon EKS -> API Deployment -> SQL Server Deployment -> persistent volume
```

The [Terraform configuration](infra/terraform/) describes:

- An ECR repository with mutable tags and image scanning on push.
- A VPC, public/private subnets, route tables, an internet gateway, and a NAT gateway.
- EKS cluster and managed node group, with IAM roles and policy attachments.
- EKS add-ons: VPC CNI, kube-proxy, CoreDNS, Pod Identity Agent, and the AWS EBS CSI driver.
- A Pod Identity association for the EBS CSI controller.

SQL Server runs inside Kubernetes in this design; Terraform does not provision an RDS database. Kubernetes configuration is preserved in [k8s/](k8s/).

### Current Deployment Status

AWS infrastructure is not currently provisioned due to operating costs. This repository preserves the earlier implementation and its manual deployment workflows, not an active hosted service.

The AWS workflows require explicit manual execution and valid external configuration. Disabling automatic workflows does not delete resources or stop charges in an AWS account. No current cloud availability is claimed here.

### Kubernetes Resources and Persistence

All manifests use namespace `inventory`:

| Manifest | Resource and behavior |
| --- | --- |
| `namespace.yaml` | Namespace `inventory`. |
| `api-configmap.yaml` | Development environment, automatic migrations enabled, JWT issuer/audience. |
| `api-deployment.yaml` | One API replica, port 8080, ECR image with `latest`, `imagePullPolicy: Always`, HTTP probes. |
| `api-service.yaml` | `inventory-api` LoadBalancer Service on port 8080. |
| `sqlserver-deployment.yaml` | One SQL Server 2022 replica, `Recreate` strategy, TCP probes, volume mounted at `/var/opt/mssql`. |
| `sqlserver-service.yaml` | Internal `sqlserver` ClusterIP Service on port 1433. |
| `sqlserver-pvc.yaml` | `sqlserver-pvc`, `ReadWriteOnce`, 2 GiB, StorageClass `gp2`. |

The SQL pod retains data across replacement only while using the same persistent volume. This is not a backup. Volume deletion behavior depends on the cluster's storage policy. The `gp2` class and storage provisioning must exist; installing a CSI driver alone does not verify that this PVC can bind.

Required runtime Secrets:

| Secret | Key | Consumer |
| --- | --- | --- |
| `sqlserver-secret` | `MSSQL_SA_PASSWORD` | SQL Server container. |
| `api-secret` | `JWT_KEY` | API JWT configuration. |
| `api-db-secret` | `ConnectionStrings__DefaultConnection` | API SQL connection. |

The connection Secret should target `sqlserver,1433` and the intended database for this architecture. The Secret controls the actual destination; the manifest does not enforce a database name. Its SQL credentials must match the database instance. Windows SQL Express, Compose, and Kubernetes do not share data automatically.

### Optional AWS Deployment

This procedure is retained for future use, not as a currently verified deployment. It requires an AWS account, valid credentials and permissions, AWS CLI, Terraform, cluster access, and provisioned resources. EKS, EC2, EBS, NAT gateways, and load balancers can incur costs.

Before provisioning, review Terraform variables, supported EKS/add-on versions, node capacity, storage classes, and security settings. Defaults describe the earlier environment, including `us-east-1` and cluster `apiinventario-eks`; they are not a guarantee of present compatibility.

Terraform commands, to be run only when deliberately provisioning or managing AWS resources:

```powershell
terraform -chdir=infra/terraform init
terraform -chdir=infra/terraform plan
terraform -chdir=infra/terraform apply
```

`terraform -chdir=infra/terraform destroy` is destructive and is only for an intentional teardown after reviewing persistent data and backups. None of these commands are run by the current workflows.

Configure GitHub Secrets by name, without placing real values in tracked files:

| Workflow | Required GitHub Secrets |
| --- | --- |
| CD to ECR | `AWS_ACCESS_KEY_ID`, `AWS_SECRET_ACCESS_KEY` |
| Deploy to EKS | `AWS_ACCESS_KEY_ID`, `AWS_SECRET_ACCESS_KEY`, `MSSQL_SA_PASSWORD`, `JWT_KEY`, `DB_CONNECTION_STRING` |

Then:

1. Provision and verify the infrastructure and EKS access separately.
2. Manually run **CD to ECR** from GitHub Actions. It publishes `inventory-api:latest` and `inventory-api:sha-<short-commit>` to the authenticated account's ECR registry in `us-east-1`.
3. Verify the manifest's registry matches your account. It currently references `178886967615.dkr.ecr.us-east-1.amazonaws.com/inventory-api:latest`, while the publish workflow derives its registry from AWS authentication.
4. Review the destination database, migrations, and backup before starting the API.
5. Manually run **Deploy to EKS**. It obtains kubeconfig, applies the namespace, creates/updates the three Secrets, applies `k8s/`, and waits for SQL and API rollouts.
6. Verify the running image and perform authenticated database operations. Applying an unchanged Deployment using `latest` does not force pods to restart; `Always` only pulls when a container starts. Likewise, changing Secrets does not refresh existing environment variables in running containers. An explicit reviewed rollout or manifest image change may be needed.

The preserved manifests use Development and HTTP; they do not declare TLS termination. Treat them as a learning deployment requiring review before any public or production use.

Useful inspection commands after selecting the intended AWS context:

```powershell
aws eks update-kubeconfig --region us-east-1 --name apiinventario-eks
kubectl config current-context
kubectl get nodes
kubectl get pods,svc,pvc -n inventory
kubectl logs -n inventory deployment/inventory-api
kubectl logs -n inventory deployment/sqlserver
kubectl port-forward -n inventory svc/inventory-api 8080:8080
```

With port forwarding running, open `http://localhost:8080`. External access depends on successful LoadBalancer provisioning and its network rules.

### Optional Local Minikube

The checked-in manifests are oriented toward EKS and are **not a ready-to-run Minikube configuration**. Loading a local image alone is insufficient because the API Deployment still references ECR and requests image pulls.

Prepare an untracked local copy rather than changing the cloud manifests:

```powershell
minikube start
kubectl config current-context
kubectl get nodes
kubectl get storageclass
docker build -t inventory-api:k8s .
minikube image load inventory-api:k8s
minikube image ls
New-Item -ItemType Directory -Path artifacts/minikube -Force | Out-Null
Copy-Item k8s/*.yaml artifacts/minikube/
```

Before applying the copies, manually adjust:

- `artifacts/minikube/api-deployment.yaml`: image to `inventory-api:k8s` and pull policy to `IfNotPresent` or `Never` so the loaded image is used.
- `artifacts/minikube/sqlserver-pvc.yaml`: replace `gp2` with a supported local StorageClass, or remove that field only when an appropriate default exists. Verify provisioning and available capacity.
- `artifacts/minikube/api-service.yaml`: use `NodePort` for `minikube service`, or retain `LoadBalancer` and use a separate `minikube tunnel` session. Port forwarding is another option.
- Review automatic migrations and the database target for this local environment.

The `artifacts/` directory is ignored by Git. Select the Minikube context before applying anything. Create the namespace before the Secrets:

```powershell
kubectl apply -f artifacts/minikube/namespace.yaml
```

Supply `MSSQL_SA_PASSWORD`, `JWT_KEY`, and `DB_CONNECTION_STRING` as environment variables in this shell. The connection must target the in-cluster SQL Service and use the same SQL password. For example, its shape is `Server=sqlserver,1433;Database=InventarioDb;User Id=sa;Password=<local-password>;TrustServerCertificate=true;Encrypt=False` for local development. Compose's `.env` is not automatically imported into this shell.

```powershell
kubectl create secret generic sqlserver-secret --from-literal="MSSQL_SA_PASSWORD=$env:MSSQL_SA_PASSWORD" -n inventory
kubectl create secret generic api-secret --from-literal="JWT_KEY=$env:JWT_KEY" -n inventory
kubectl create secret generic api-db-secret --from-literal="ConnectionStrings__DefaultConnection=$env:DB_CONNECTION_STRING" -n inventory
kubectl apply -f artifacts/minikube/
kubectl rollout status deployment/sqlserver -n inventory --timeout=10m
kubectl rollout status deployment/inventory-api -n inventory --timeout=10m
kubectl get pods,svc,pvc -n inventory
kubectl port-forward -n inventory svc/inventory-api 8080:8080
```

These `create secret` commands assume the Secrets do not already exist. Manage updates separately when reusing a namespace. This adapted Minikube procedure has not been executed as part of this documentation update.

## Useful Commands

Run these from the repository root; the test command below explicitly excludes SQL integration:

```powershell
# Compile both projects.
dotnet build ApiInventario.slnx --configuration Release

# Run tests without creating SQL test databases.
dotnet test ApiInventario.Tests/ApiInventario.Tests.csproj --configuration Release --filter 'FullyQualifiedName!~ApiInventario.Tests.SqlServerConcurrencyTests'

# Build a local API image without publishing it.
docker build -t inventory-api:local .

# Inspect local Compose services and logs.
docker compose ps
docker compose logs -f apiinventario
```

For cloud or Kubernetes operations, use the optional procedures above after reviewing their prerequisites. A Git push runs CI validation; it is no longer a command for publishing an image or deploying to AWS.

## Possible Next Improvements

- Add coverage for authentication and role-based authorization.
- Add database-aware readiness checks separate from basic HTTP health.
- If cloud work resumes, use OIDC/IAM roles, immutable deployment image references, controlled migrations, and a reviewed backup strategy.
