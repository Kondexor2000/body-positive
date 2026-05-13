# Bias Audit Tool for Models

Body-positive WebAPI for auditing image moderation. The project accepts uploads, saves files to S3/MinIO, queues background tasks, queries NudeNet for detections, saves results to PostgreSQL, and generates HTML reports secured with an API key.

## Composition

- ASP.NET Core 8 Minimal API
- API Key in `X-Api-Key` header
- Multipart upload to `/api/audits/upload`
- Background queue based on `Channel<Guid>` and `BackgroundService`
- PostgreSQL via EF Core
- S3 via `AWSSDK.S3`, locally compatible with MinIO
- NudeNet integration via HTTP endpoint `/detect`
- HTML report: `/api/audits/{id}/report`
- Static test client: `/api-test.html`

## Local Setup

Make sure Docker Desktop is running before starting the containers.

2. Run the API:

```powershell
dotnet run --project .\BiasAudit.Api\BiasAudit.Api.csproj
```

3. Open the browser to the address shown in the logs, typically: `https://localhost:7072`

4. Swagger is available at `https://localhost:7072/swagger`.

PostgreSQL is exposed on host port `5433` to avoid conflicts with local
PostgreSQL installations on `5432`.

## API Test Client

A static HTML client is served from `wwwroot/api-test.html` and is available at:

```text
https://localhost:7072/api-test.html
```

Use it to test:

- `GET /health`
- `POST /api/audits/upload`
- `GET /api/audits/{id}`
- `GET /api/audits/{id}/report`

The test client also supports setting the `X-Api-Key` header.

## Running Tests

The project includes unit and integration tests for API controllers.

To run tests:

```powershell
dotnet test
```

Unit tests use Moq to mock dependencies such as database and external services. Integration tests use WebApplicationFactory to verify endpoints.

Current status: **27/27 tests passing**.

## Example Upload

```powershell
curl.exe -k -X POST "https://localhost:7072/api/audits/upload" `
  -H "X-Api-Key: dev-api-key-change-me" `
  -F "file=@C:\path\image.jpg;type=image/jpeg" `
  -F "modelDecision=rejected" `
  -F "cohort=voluntarily provided test cohort" `
  -F "notes=over-moderation regression test"
```

Status:

```powershell
curl.exe -k "https://localhost:7072/api/audits/{id}" -H "X-Api-Key: dev-api-key-change-me"
```

HTML report:

```powershell
curl.exe -k "https://localhost:7072/api/audits/{id}/report" -H "X-Api-Key: dev-api-key-change-me"
```

## NudeNet

The API expects a NudeNet service compatible with the contract:

```http
POST /detect
Content-Type: multipart/form-data
file=<image>
```

Response:

```json
[
  {
    "label": "BELLY_EXPOSED",
    "confidence": 0.72,
    "box": { "x": 10, "y": 20, "width": 200, "height": 180 }
  }
]
```

By default, `NudeNet:UseMockWhenUnavailable=true`, so the app works without NudeNet service and returns an empty detections list. In production, set it to `false`.

## Ethical Assumptions

The tool does not classify attractiveness, body type, or a person's identity. Cohort is an optional field for aggregated tests. The report indicates the risk of over-moderation and sexualization of neutral body representations, not judging the person in the photo.
