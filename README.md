# release-readiness-api

A C# / .NET 8 minimal API that answers the question every change-advisory meeting asks: **is this release ready to go?**

Teams register a release, record readiness checks (security scan, UAT sign-off, rollback test) and risks, and the API returns a go/no-go decision with the exact blockers and warnings, so the meeting spends its time on decisions instead of status gathering.

> Portfolio project (2026) by Fleury-Bryan Havyarimana. Written from scratch; the go/no-go rules mirror how I ran release-readiness and change-advisory sessions for payment, insurance and SaaS platforms. No client code or data.

## Go / no-go rules

| Rule | Effect |
|---|---|
| A blocking check is Pending or Failed | No-go, listed as a blocker with its owner |
| An unmitigated High or Critical risk | No-go, listed as a blocker with its owner |
| An open Medium risk | Warning, and 10 points off the readiness score |
| A waiver without a written justification | Warning |
| Target date already passed | Warning |
| No checks recorded | Never go |

The score is the share of checks passed or waived, minus Medium-risk penalties.

## Endpoints

| Method | Route | Purpose |
|---|---|---|
| GET | `/health` | Liveness |
| GET | `/releases` | List releases by target date |
| POST | `/releases` | Create `{ "name": "...", "targetDate": "2026-03-05" }` |
| GET | `/releases/{id}` | Release with checks and risks |
| PUT | `/releases/{id}/checks` | Add or update a check by name |
| POST | `/releases/{id}/risks` | Add a risk |
| GET | `/releases/{id}/readiness` | Go/no-go report |

Example report:

```json
{
  "release": "Partner API 2.4",
  "go": false,
  "score": 90,
  "blockers": ["High risk open: Settlement window overlap (owner pm)"],
  "warnings": ["Medium risk open: Vendor SDK pinned"]
}
```

## Run

```bash
dotnet run --project src/ReleaseReadiness.Api
# or
docker build -t release-readiness-api . && docker run -p 8080:8080 release-readiness-api
```

## Test

```bash
dotnet test tests/ReleaseReadiness.Tests
```

Unit tests cover the evaluator rules; integration tests run the real HTTP pipeline with `WebApplicationFactory`. The GitHub Actions workflow in the source builds and tests the solution, then builds the Docker image and smoke-tests `/health` in a running container.

## Design notes

- The rules live in one pure function (`ReadinessEvaluator.Evaluate`) that takes the date as input, so they are easy to test and to explain to non-developers.
- `TimeProvider` is injected, so the API clock can be faked in tests.
- Storage sits behind `IReleaseStore`; the in-memory store keeps the sample self-contained and can be swapped for a database.
- The container runs as the non-root `app` user.
