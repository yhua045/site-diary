# site-diary

Developer quickstart

Prerequisites
- .NET SDK (compatible with the solution)
- Node.js & npm (for the frontend)
- Docker & Docker Compose (for container runs)

Run the frontend (development)

```bash
cd frontend
npm install
npm run dev
```

Run the backend (development)

```bash
# from repo root
dotnet restore
dotnet build
dotnet run --project src/SiteDiary.Web
```

Seed the database

The project includes a data seeder that runs on application startup in development/local environments. To run the seeder when starting the backend locally set the environment to Development and run the Web project:

```bash
ASPNETCORE_ENVIRONMENT=Development dotnet run --project src/SiteDiary.Web
```

If you run the application inside Docker Compose the seeder will also run when the container starts (if enabled in the app configuration).

Build and deploy with Docker

Build and run using Docker Compose (recommended for local containerized development):

```bash
docker compose up -d --build
```

Build the Web image manually:

```bash
docker build -t your-repo/site-diary:latest -f src/SiteDiary.Web/Dockerfile .
# then tag & push to your registry
docker tag your-repo/site-diary:latest <registry>/your-repo/site-diary:latest
docker push <registry>/your-repo/site-diary:latest
```

Notes
- Host uploads folder is bind-mounted to the container at `/app/uploads` when using the provided `docker-compose.yml`.
- Adjust environment variables (connection strings, ports) as needed in `docker-compose.yml` or `appsettings.*.json`.
