# Inventory API DevOps

Inventory management API built with ASP.NET Core, Entity Framework Core, SQL Server, Docker, and GitHub Actions.

The project includes product, category, authentication, and inventory movement endpoints. Stock is controlled through inventory movements, so products are created with `StockActual = 0` and later updated through `Entrada`, `Salida`, or `Ajuste` movements.

## Tech Stack

- ASP.NET Core Web API
- Entity Framework Core
- SQL Server
- JWT authentication
- Swagger / OpenAPI
- xUnit tests
- Docker and Docker Compose
- GitHub Actions CI

## Project Structure

```text
.
+-- ApiInventario
|   +-- Controllers
|   +-- Data
|   +-- DTOs
|   +-- Migrations
|   +-- Models
|   +-- Services
|   +-- Program.cs
+-- ApiInventario.Tests
+-- .github/workflows
+-- k8s
+-- Dockerfile
+-- docker-compose.yml
+-- .env.example
```

## Environment Variables

Create a local `.env` file from the example:

```bash
cp .env.example .env
```

Then set local values:

```env
MSSQL_SA_PASSWORD=your_local_sql_password
JWT_KEY=your_long_local_jwt_key
```

The `.env` file is ignored by Git and should not be committed.

## Run With Docker Compose

Build and start the API with SQL Server:

```bash
docker compose up -d --build
```

Swagger:

```text
http://localhost:8080
```

Health check:

```bash
curl http://localhost:8080/health
```

Expected response:

```text
Healthy
```

Stop the containers:

```bash
docker compose down
```

Remove containers and the SQL Server volume:

```bash
docker compose down -v
```

Use `-v` only when local database data can be deleted.

## Database Migrations

The API applies Entity Framework Core migrations automatically when it starts.

This behavior can be disabled with:

```env
Database__AutoMigrate=false
```

The Docker CI pipeline uses this setting because it only needs to verify that the API image starts and `/health` responds. It does not start SQL Server.

## Run Tests

Run the test project:

```bash
dotnet test ApiInventario.Tests/ApiInventario.Tests.csproj
```

Current tests validate business rules such as:

- Products are created with `StockActual = 0`.
- Product price must be greater than zero.
- Inventory entry movements increase product stock.
- Inventory output movements fail when stock is insufficient.

## CI Pipelines

The repository includes two GitHub Actions workflows.

### Backend CI

File:

```text
.github/workflows/backend-ci.yml
```

Runs on every push or pull request to `main`.

Steps:

```text
checkout repository
setup .NET 10
dotnet restore
dotnet build
dotnet test
```

This validates that the backend compiles and that the automated tests pass.

### Docker CI

File:

```text
.github/workflows/docker-ci.yml
```

Runs on every push or pull request to `main`.

Steps:

```text
checkout repository
setup Docker Buildx
build Docker image
run API container
call /health
remove test container
```

This validates that the Docker image can be built, the container can start, and the API responds through the health endpoint.

## Kubernetes With Minikube

The project can also be deployed locally to Kubernetes using Minikube.

Kubernetes manifests are stored in:

```text
k8s/
```

Versioned manifests:

```text
namespace.yaml
api-configmap.yaml
sqlserver-pvc.yaml
sqlserver-deployment.yaml
sqlserver-service.yaml
api-deployment.yaml
api-service.yaml
```

Secrets with real values are created directly in the cluster with `kubectl create secret generic` and are not committed to the repository.

### Start Minikube

```bash
minikube start
kubectl get nodes
```

### Load the API Image Into Minikube

From the project root:

```bash
docker build -t inventory-api:k8s .
minikube image load inventory-api:k8s
minikube image ls | grep inventory-api
```

### Create Runtime Secrets

SQL Server password:

```bash
kubectl create secret generic sqlserver-secret \
  --from-literal=MSSQL_SA_PASSWORD='your_sql_password' \
  -n inventory
```

JWT key:

```bash
kubectl create secret generic api-secret \
  --from-literal=JWT_KEY='your_long_jwt_key' \
  -n inventory
```

Database connection string:

```bash
kubectl create secret generic api-db-secret \
  --from-literal=ConnectionStrings__DefaultConnection='Server=sqlserver,1433;Database=InventarioDb;User Id=sa;Password=your_sql_password;TrustServerCertificate=true;Encrypt=False' \
  -n inventory
```

### Deploy to Kubernetes

Apply all manifests:

```bash
kubectl apply -f k8s/
```

Check resources:

```bash
kubectl get pods -n inventory
kubectl get svc -n inventory
kubectl get pvc -n inventory
```

### Access the API

Expose the API through Minikube:

```bash
minikube service inventory-api -n inventory
```

This opens a local URL that forwards traffic to the `NodePort` service.

### Verify the Deployment

Useful commands for debugging:

```bash
kubectl logs -n inventory deployment/inventory-api
kubectl logs -n inventory deployment/sqlserver
kubectl describe pod -n inventory <pod-name>
kubectl describe svc inventory-api -n inventory
```

The current Kubernetes setup includes:

- `sqlserver` deployed as a `Deployment`
- `sqlserver` exposed internally as a `ClusterIP` service
- `inventory-api` deployed as a `Deployment`
- `inventory-api` exposed as a `NodePort` service
- SQL Server storage persisted with a `PersistentVolumeClaim`
- API health probes using `/health`

## Useful Commands

Build the API locally:

```bash
dotnet build ApiInventario/ApiInventario.csproj
```

Run tests:

```bash
dotnet test ApiInventario.Tests/ApiInventario.Tests.csproj
```

Build Docker image:

```bash
docker build -t inventory-api:local .
```

Run the full local stack:

```bash
docker compose up -d --build
```

Deploy to Kubernetes:

```bash
kubectl apply -f k8s/
```

Open the API from Minikube:

```bash
minikube service inventory-api -n inventory
```

View container logs:

```bash
docker compose logs -f apiinventario
```

## Next Steps

- Add a CD workflow that builds and pushes the Docker image.
- Add a Kubernetes deployment workflow after publishing the image.
- Add more tests for authentication and inventory movement rules.
