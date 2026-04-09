# HaveQR Quick Reference

## Prerequisites

- .NET SDK `10.0.x` for the current scaffold
- Node.js `20.x` or newer for the webapp
- A second machine or hosted environment for Docker-backed RabbitMQ, PostgreSQL, and Redis if you do not want to run containers on the dev machine

## Dev Machine Commands

```bash
dotnet build HaveQR.sln
npm install --prefix webapp
ASPNETCORE_URLS=http://127.0.0.1:5080 dotnet run --project microservices/HaveQR.PublicApi/HaveQR.PublicApi.csproj
dotnet run --project microservices/HaveQR.Worker/HaveQR.Worker.csproj
HAVEQR_PUBLIC_API_BASE_URL=http://127.0.0.1:5080 npm run dev --prefix webapp
HAVEQR_PUBLIC_API_BASE_URL=http://127.0.0.1:5080 npm run build --prefix webapp
```

## Power Machine Infrastructure Commands

```bash
docker compose up -d postgres redis rabbitmq
docker compose logs -f rabbitmq
docker compose logs -f postgres
docker compose logs -f redis
docker compose down
```

## Tunnel Commands

```bash
ssh -L 5432:127.0.0.1:5432 -L 5672:127.0.0.1:5672 <user>@<power-machine-host>
ssh -L 15672:127.0.0.1:15672 <user>@<power-machine-host>
ssh -L 5080:127.0.0.1:5080 <user>@<power-machine-host>
```

If the webapp is running on the dev machine while the API runs remotely through a tunnel, keep `HAVEQR_PUBLIC_API_BASE_URL=http://127.0.0.1:5080`.

## Default Configuration Keys

```text
RabbitMq__HostName=localhost
RabbitMq__Port=5672
RabbitMq__UserName=haveqr
RabbitMq__Password=haveqr_dev_password
RabbitMq__RenderQueueName=haveqr.render.jobs
RenderStorage__JobStateProvider=FileSystem
RenderStorage__ArtifactProvider=FileSystem
RenderStorage__RootPath=.data/render-jobs
PostgresRenderStore__ConnectionString=Host=127.0.0.1;Port=5432;Database=haveqr;Username=haveqr;Password=haveqr_dev_password
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
export PostgresRenderStore__ConnectionString="Host=127.0.0.1;Port=5432;Database=haveqr;Username=haveqr;Password=haveqr_dev_password"
export R2Storage__BucketName="haveqr-render-artifacts"
export R2Storage__AccountId="<cloudflare-account-id>"
export R2Storage__AccessKeyId="<r2-access-key-id>"
export R2Storage__SecretAccessKey="<r2-secret-access-key>"
```

## Render Request Example

```bash
curl -X POST http://127.0.0.1:5080/api/v1/qr/render \
  -H "Content-Type: application/json" \
  -d '{
    "contentType": "Link",
    "targetUrl": "https://computemore.com/products/qr-launch",
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
curl -X POST http://127.0.0.1:5080/api/v1/qr/render \
  -H "Content-Type: application/json" \
  -d '{
    "contentType": "WhatsApp",
    "payload": {
      "phone": "+260977000000",
      "message": "Hello from HaveQR"
    },
    "output": {
      "sizePx": 1024
    }
  }'
```

## Job Retrieval

```bash
curl http://127.0.0.1:5080/api/v1/qr/jobs/<job-id>
curl -L http://127.0.0.1:5080/api/v1/qr/jobs/<job-id>/artifacts/png --output haveqr.png
curl -L http://127.0.0.1:5080/api/v1/qr/jobs/<job-id>/artifacts/svg --output haveqr.svg
```