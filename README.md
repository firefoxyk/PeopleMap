# PeopleMap landing prototype

A responsive demand-validation landing page for PeopleMap, served by an ASP.NET Core 8 Minimal API. It includes interactive product mockups, anonymous first-party analytics, and an idempotent early-access form.

## Run locally

```powershell
dotnet run --project src/PeopleMap.Api
```

Open `http://localhost:5186`.

## Static demo

Build the same static artifact that GitHub Pages publishes:

```powershell
powershell -ExecutionPolicy Bypass -File ./scripts/build-pages.ps1
powershell -ExecutionPolicy Bypass -File ./scripts/serve-static.ps1 -Root .pages-dist
```

Open `http://localhost:4173/`. To reproduce the repository subpath locally, build to `.pages-preview/PeopleMap`, serve `.pages-preview`, and open `http://localhost:4173/PeopleMap/`:

```powershell
powershell -ExecutionPolicy Bypass -File ./scripts/build-pages.ps1 -OutputPath .pages-preview/PeopleMap
powershell -ExecutionPolicy Bypass -File ./scripts/serve-static.ps1 -Root .pages-preview
```

The build script copies only `src/PeopleMap.Api/wwwroot`, injects the demo marker into the generated copy, and never includes the API, tests, or local data. In demo mode analytics and API submission are disabled. Pushing `main` runs `.github/workflows/pages.yml` and updates GitHub Pages.

Run the static desktop/mobile browser checks on Windows with Edge installed:

```powershell
powershell -ExecutionPolicy Bypass -File ./scripts/test-pages.ps1
```

## Architecture

- `wwwroot/` — dependency-free, progressively enhanced landing page.
- `POST /api/analytics/events` — accepts event batches (maximum 100).
- `POST /api/early-access` — validates email and returns an idempotent result.
- `Services/FilePrototypeStore.cs` — local prototype persistence in ignored `App_Data/*.ndjson` files.
- `database/schema.sql` — PostgreSQL production schema. `IPrototypeStore` is the seam for swapping in a PostgreSQL implementation when deployment credentials and a driver are added.

Analytics never includes the submitted email. Anonymous visitor IDs live in local storage, session IDs live in session storage, and event delivery is batched.

## Checks

```powershell
dotnet build PeopleMap.sln -c Release
dotnet run --project tests/PeopleMap.SmokeTests
```
