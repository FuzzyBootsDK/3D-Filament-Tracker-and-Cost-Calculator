# Docker configurations for Filament Tracker

This `docker` folder contains two docker-compose configurations and instructions for running the application in Development or Production mode.

Files

- `docker-compose.prod.yml` - Compose configuration for a production-like container. Builds the app using the `FilamentTracker/Dockerfile`, exposes the app on port 5000 by default, and stores runtime data in a named Docker volume `filament-tracker-data`.

- `docker-compose.dev.yml` - Development compose configuration that mounts the `FilamentTracker` project into a `mcr.microsoft.com/dotnet/sdk:10.0` container and runs `dotnet watch run` for live reload. Maps host port `5000` to the container. Use this when you want to iterate on code inside the container while seeing changes automatically.

Quickstart

Prerequisites:
- Docker Desktop (or Docker Engine + Compose) installed and running
- Ports required (default 5000) available on host

Production (recommended for deployment/testing):

1. From the repo root run (default port 5000):

   docker compose -f docker/docker-compose.prod.yml up -d --build

   Or if you have older docker-compose installed:

   docker-compose -f docker/docker-compose.prod.yml up -d --build

2. The app will be available at `http://localhost:5000` (or the port set in `$PORT` / environment variable).

3. To stop the service:

   docker compose -f docker/docker-compose.prod.yml down

Development (fast iteration with live reload):

1. From the `docker` folder or repo root run:

   docker compose -f docker/docker-compose.dev.yml up --build

   This starts a container based on the .NET SDK image and runs `dotnet watch run` so code changes inside `FilamentTracker/` are picked up automatically.

2. Visit `http://localhost:5000` to see the running app. Logs will show compilation and runtime messages.

Notes & tips

- Data persistence: both configs use a named volume `filament-tracker-data` that stores runtime files (SQLite DB, uploads, etc.). Removing volumes will delete data.

- Port override: you can override the host port by setting `PORT` environment variable before running compose (production compose uses `${PORT:-5000}`), e.g. `PORT=8080 docker compose -f docker/docker-compose.prod.yml up -d`.

- Healthchecks: the production compose file includes a simple HTTP healthcheck that hits `/` on the container. If you want a dedicated health endpoint, you can add one to the ASP.NET app and update the `HEALTHCHECK` in the Dockerfile and `test` in the compose file.

- Windows users: the `dev` compose mounts source using a relative path; on Windows ensure Docker Desktop uses WSL or has file sharing enabled for the project folder for live reload to work smoothly.

- If you prefer a single command wrapper, use `deploy.ps1` (PowerShell) or `deploy.bat` which will now prefer `docker compose` if available and falls back to building and running the image directly.

If you want, I can:
- Add an nginx reverse-proxy to the production compose
- Add a small `.env` template to manage PORT and other settings
- Add a health endpoint in the app and wire it into the healthcheck

