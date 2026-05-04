# HalfQR Quick Reference

**App Version**: 0.3.0.1  
**Date**: 2026-04-09  
**Repository**: `halfqr`

## Contents

- [HalfQR Quick Reference](#halfqr-quick-reference)
  - [Contents](#contents)
  - [Prerequisites](#prerequisites)
  - [Docker Bench Commands](#docker-bench-commands)
  - [Local Bench Endpoints](#local-bench-endpoints)
  - [Tunnel Targets](#tunnel-targets)
  - [Browser Request Policy](#browser-request-policy)
  - [Optional Host Builds](#optional-host-builds)
  - [Health Checks](#health-checks)
  - [Default Configuration Keys](#default-configuration-keys)
  - [Provider Switches](#provider-switches)
  - [Render Request Example](#render-request-example)
  - [WhatsApp Example](#whatsapp-example)
  - [Job Retrieval](#job-retrieval)

---

## Prerequisites

- Docker Desktop or Docker Engine with Compose
- Public tunnel configured outside this repository
- .NET SDK `10.0.x` if you want to build the backend outside Docker
- Node.js `20.x` or newer if you want to build the webapp outside Docker

## Docker Bench Commands

```bash
docker compose build webapp public-api worker
docker compose up -d webapp public-api worker postgres redis rabbitmq
docker compose ps
docker compose logs -f webapp public-api worker rabbitmq
docker compose down
```

## Local Bench Endpoints

```text
http://127.0.0.1:5173 -> webapp
http://127.0.0.1:8083 -> public API
http://127.0.0.1:5433 -> PostgreSQL
http://127.0.0.1:6380 -> Redis
http://127.0.0.1:5673 -> RabbitMQ
http://127.0.0.1:15673 -> RabbitMQ management
```

## Tunnel Targets

```text
www.halfqr.com -> localhost:5173
api.halfqr.com -> localhost:8083
```

## Browser Request Policy

- Build and run the webapp with `NEXT_PUBLIC_HALFQR_API_BASE_URL=https://api.halfqr.com`.
- The browser should not call `http://localhost:8083`; localhost is reserved for operator checks and CLI smoke tests on the devops bench.

## Optional Host Builds

```bash
dotnet build HalfQR.sln
npm install --prefix webapp
npm run build --prefix webapp
```

## Health Checks

```bash
curl http://127.0.0.1:8083/healthz
curl http://127.0.0.1:5173
curl https://api.halfqr.com/healthz
curl https://www.halfqr.com
```

## Default Configuration Keys

```text
NEXT_PUBLIC_HALFQR_API_BASE_URL=https://api.halfqr.com
HALFQR_WEBAPP_PUBLIC_ORIGIN=https://www.halfqr.com
HALFQR_WEBAPP_LOCAL_ORIGIN=http://localhost:5173
RabbitMq__HostName=rabbitmq
RabbitMq__Port=5672
RabbitMq__UserName=halfqr
RabbitMq__Password=halfqr_dev_password
RabbitMq__RenderQueueName=halfqr.render.jobs
RenderStorage__JobStateProvider=FileSystem
RenderStorage__ArtifactProvider=FileSystem
RenderStorage__RootPath=/var/lib/halfqr/render-jobs
PostgresRenderStore__ConnectionString=Host=127.0.0.1;Port=5433;Database=halfqr;Username=halfqr;Password=halfqr_dev_password
PostgresRenderStore__Schema=public
PostgresRenderStore__TableName=render_jobs
R2Storage__BucketName=
R2Storage__AccountId=
R2Storage__Endpoint=
R2Storage__AccessKeyId=
R2Storage__SecretAccessKey=
R2Storage__KeyPrefix=render-jobs
```

## Provider Switches

```bash
export RenderStorage__JobStateProvider=PostgreSql
export RenderStorage__ArtifactProvider=R2
export PostgresRenderStore__ConnectionString="Host=127.0.0.1;Port=5433;Database=halfqr;Username=halfqr;Password=halfqr_dev_password"
export R2Storage__BucketName="halfqr-render-artifacts"
export R2Storage__AccountId="<cloudflare-account-id>"
export R2Storage__AccessKeyId="<r2-access-key-id>"
export R2Storage__SecretAccessKey="<r2-secret-access-key>"
```

## Render Request Example

```bash
curl -X POST http://127.0.0.1:8083/api/v1/qr/render \
  -H "Content-Type: application/json" \
  -d '{
    "contentType": "Link",
    "targetUrl": "https://computemore.com/products/qr-launch",
    "payload": {},
    "mode": "Static",
    "errorCorrectionLevel": "H",
    "output": {
      "sizePx": 1024
    },
    "finder": {
      "borderShape": "Rounded",
      "centerShape": "Circle"
    },
    "colors": {
      "dark": "#0B1F3A",
      "light": "#FFFFFF"
    }
  }'
```

## WhatsApp Example

```bash
curl -X POST http://127.0.0.1:8083/api/v1/qr/render \
  -H "Content-Type: application/json" \
  -d '{
    "contentType": "WhatsApp",
    "payload": {
      "phone": "+260977000000",
      "message": "Hello from HalfQR"
    },
    "output": {
      "sizePx": 1024
    }
  }'
```

## Job Retrieval

```bash
curl http://127.0.0.1:8083/api/v1/qr/jobs/<job-id>
curl -L http://127.0.0.1:8083/api/v1/qr/jobs/<job-id>/artifacts/png --output halfqr.png
curl -L http://127.0.0.1:8083/api/v1/qr/jobs/<job-id>/artifacts/svg --output halfqr.svg
```