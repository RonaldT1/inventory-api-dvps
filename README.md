# Inventory API DevOps

Inventory management API built with ASP.NET Core, Entity Framework Core, SQL Server, Docker, and GitHub Actions.

The project includes product, category, authentication, and inventory movement endpoints. Stock is controlled through inventory movements, so products are created with `StockActual = 0` and later updated through `Entrada`, `Salida`, or `Ajuste` movements.

The current cloud flow is:

- Terraform provisions the AWS infrastructure.
- GitHub Actions builds and publishes the API image to Amazon ECR.
- GitHub Actions deploys the Kubernetes manifests to Amazon EKS.
- Kubernetes runs the API and SQL Server inside the cluster.

## Tech Stack

- ASP.NET Core Web API
- Entity Framework Core
- SQL Server
- JWT authentication
- Swagger / OpenAPI
- xUnit tests
- Docker and Docker Compose
- GitHub Actions CI
- Amazon ECR
- Amazon EKS

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
+-- infra/terraform
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

The repository includes four active GitHub Actions workflows.

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

### CD to ECR

File:

```text
.github/workflows/cd-ecr.yml
```

Runs on every push to `main` and can also be triggered manually.

Steps:

```text
checkout repository
configure AWS credentials
login to Amazon ECR
extract Docker metadata
build Docker image
push image to ECR
```

Published image:

```text
178886967615.dkr.ecr.us-east-1.amazonaws.com/inventory-api
```

The workflow publishes at least these tags:

```text
latest
sha-<commit>
```

This gives the project a real CD step by automatically publishing a deployable image to Amazon ECR after changes reach `main`.

Required GitHub Secrets:

```text
AWS_ACCESS_KEY_ID
AWS_SECRET_ACCESS_KEY
```

### Deploy to EKS

File:

```text
.github/workflows/deploy-eks.yml
```

Runs:

- automatically after `CD to ECR` completes successfully on `main`
- manually through `workflow_dispatch`

Steps:

```text
checkout repository
configure AWS credentials
install kubectl
update kubeconfig for apiinventario-eks
verify cluster access
apply namespace
create or update Kubernetes secrets from GitHub Secrets
apply k8s manifests
wait for SQL Server rollout
wait for API rollout
show deployed pods and services
```

Required GitHub Secrets:

```text
AWS_ACCESS_KEY_ID
AWS_SECRET_ACCESS_KEY
MSSQL_SA_PASSWORD
JWT_KEY
DB_CONNECTION_STRING
```

## Kubernetes Deployment Targets

The repository includes Kubernetes manifests that can be applied to either:

- local Minikube for development and learning
- Amazon EKS for a managed cloud cluster

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

### Local Minikube

Start Minikube:

```bash
minikube start
kubectl get nodes
```

Load the API image into Minikube:

From the project root:

```bash
docker build -t inventory-api:k8s .
minikube image load inventory-api:k8s
minikube image ls | grep inventory-api
```

Create runtime secrets:

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

Deploy to Kubernetes:

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

Access the API:

Expose the API through Minikube:

```bash
minikube service inventory-api -n inventory
```

This opens a local URL that forwards traffic to the `NodePort` service.

Verify the deployment:

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
- `inventory-api` exposed as a `LoadBalancer` service on Amazon EKS
- SQL Server storage persisted with a `PersistentVolumeClaim`
- API health probes using `/health`

### Amazon EKS

The repository now uses Terraform in:

```text
infra/terraform/
```

Terraform currently manages:

- Amazon ECR repository
- IAM roles and policy attachments for EKS
- VPC, public subnets, private subnets, route tables, internet gateway, and NAT gateway
- Amazon EKS cluster
- EKS managed node group
- EKS addons:
  - `vpc-cni`
  - `kube-proxy`
  - `coredns`
  - `eks-pod-identity-agent`
  - `aws-ebs-csi-driver`

The current API Deployment manifest points to Amazon ECR:

```yaml
image: 178886967615.dkr.ecr.us-east-1.amazonaws.com/inventory-api:latest
```

That means the EKS cluster can pull the image published by the `CD to ECR` workflow.

Current cluster name:

```text
apiinventario-eks
```

Typical EKS workflow is:

- run `terraform apply` in `infra/terraform`
- let `CD to ECR` publish the Docker image
- let `Deploy to EKS` create or update cluster secrets and apply `k8s/`
- access the API through the AWS LoadBalancer created for `inventory-api`

### Terraform Commands

Initialize Terraform:

```bash
cd infra/terraform
terraform init
```

Preview infrastructure changes:

```bash
terraform plan
```

Apply infrastructure:

```bash
terraform apply
```

Destroy infrastructure when it is no longer needed:

```bash
terraform destroy
```

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

Build and publish image to Amazon ECR:

```bash
git push origin main
```

Access the API locally from EKS with port forwarding:

```bash
kubectl port-forward -n inventory svc/inventory-api 8080:8080
```

Health check:

```bash
curl http://localhost:8080/health
```

View container logs:

```bash
docker compose logs -f apiinventario
```

## Next Steps

- Move GitHub Actions AWS authentication from access keys to OIDC and IAM roles.
- Publish a stable image tag strategy for staging and production.
- Add more tests for authentication and inventory movement rules.
