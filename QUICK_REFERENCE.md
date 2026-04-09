# HaveQR Quick Reference

## Prerequisites

- .NET SDK `10.0.x` for the current scaffold
- Node.js `20.x` or newer for the webapp
- Docker Desktop or compatible Docker engine if you want local RabbitMQ, PostgreSQL, and Redis

## Build And Run

```bash
dotnet build HaveQR.sln
dotnet run --project microservices/HaveQR.PublicApi/HaveQR.PublicApi.csproj
dotnet run --project microservices/HaveQR.Worker/HaveQR.Worker.csproj
npm install --prefix webapp
npm run dev --prefix webapp
```

## Infrastructure

```bash
docker compose up -d postgres redis rabbitmq
docker compose logs -f rabbitmq
docker compose down
```

## Default Configuration Keys

```text
RabbitMq__HostName=localhost
RabbitMq__Port=5672
RabbitMq__UserName=haveqr
RabbitMq__Password=haveqr_dev_password
RabbitMq__RenderQueueName=haveqr.render.jobs
RenderStorage__RootPath=.data/render-jobs
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