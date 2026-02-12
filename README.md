# Redhead Sites Catalog

Production-ready internal web application for browsing and managing a sites catalog.

## Architecture

- **Backend**: ASP.NET Core (.NET 10) solution with clean architecture
  - `Redhead.SitesCatalog.Api` - Web API layer
  - `Redhead.SitesCatalog.Application` - Business logic layer
  - `Redhead.SitesCatalog.Domain` - Domain entities and interfaces
  - `Redhead.SitesCatalog.Infrastructure` - Data access and external services

- **Frontend**: Vite + React + TypeScript
  - `Redhead.SitesCatalog.Web` - Single Page Application

## Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0) (or latest LTS)
- [Node.js 22.22.0+](https://nodejs.org/) (LTS) and npm
  - **Note:** Vite 7.3.1 requires Node.js 20.19+ or 22.12+. We recommend using [nvm-windows](https://github.com/coreybutler/nvm-windows) to manage Node.js versions.
- [PostgreSQL 15+](https://www.postgresql.org/download/)

### Node.js Version Management (Optional but Recommended)

If you have an older Node.js version, use nvm to upgrade:

```powershell
# Install Node.js 22.22.0
nvm install 22.22.0

# Switch to Node.js 22.22.0
nvm use 22.22.0

# Verify version
node --version
# Should show: v22.22.0
```

### PostgreSQL Database Setup

1. **Install PostgreSQL** if you haven't already (PostgreSQL 15+ recommended)

2. **Create the database:**
   ```sql
   CREATE DATABASE redhead_sites_catalog;
   ```

3. **Update connection string** (if needed):
   - Edit `src/Redhead.SitesCatalog.Api/appsettings.json`
   - Default: `Host=localhost;Database=redhead_sites_catalog;Username=postgres;Password=postgres`

4. **Apply migrations:**
   ```bash
   # From the repository root
   dotnet ef database update --project src/Redhead.SitesCatalog.Infrastructure --startup-project src/Redhead.SitesCatalog.Api
   ```

   This will create all tables and seed initial RoleSettings data.

## Getting Started

### 1. Clone and navigate to the repository

```bash
cd c:\work\redhead-catalog-app
```

### 2. Backend Setup and Run

From the repository root:

```bash
# Build the solution
dotnet build redhead-catalog-app.sln

# Run the API (from the Api project directory)
cd src\Redhead.SitesCatalog.Api
dotnet run --launch-profile http
```

The API will be available at: **http://localhost:5000**

Health endpoint: **http://localhost:5000/api/health**

### 3. Frontend Setup and Run

Open a new terminal from the repository root:

```bash
# Navigate to the Web project
cd src\Redhead.SitesCatalog.Web

# Install dependencies (first time only)
npm install

# Run the development server
npm run dev
```

The frontend will be available at: **http://localhost:5173**

The React app will automatically fetch and display the health status from the backend API.

## Available Commands

### Backend

```bash
# Build the entire solution
dotnet build redhead-catalog-app.sln

# Build and run the API
cd src\Redhead.SitesCatalog.Api
dotnet run

# Clean build artifacts
dotnet clean redhead-catalog-app.sln
```

### Frontend

```bash
cd src\Redhead.SitesCatalog.Web

# Development server
npm run dev

# Build for production
npm run build

# Lint code
npm run lint

# Format code
npm run format

# Preview production build
npm run preview
```

## Quality Gates

### Backend
- ✅ Code analyzers enabled (Microsoft.CodeAnalysis.NetAnalyzers)
- ✅ EnforceCodeStyleInBuild enabled
- ✅ .editorconfig with C# formatting rules
- ✅ Nullable reference types enabled
- ✅ Compatible with `dotnet format`

### Frontend
- ✅ TypeScript strict mode enabled
- ✅ ESLint configured with TypeScript support
- ✅ Prettier for code formatting
- ✅ React hooks and React Refresh plugins
- ✅ No `any` usage warnings

## Project Structure

```
redhead-catalog-app/
├── src/
│   ├── Redhead.SitesCatalog.Api/          # ASP.NET Core Web API
│   │   ├── Controllers/
│   │   │   └── HealthController.cs         # Health check endpoint
│   │   ├── Properties/
│   │   │   └── launchSettings.json         # Launch configuration
│   │   └── Program.cs                      # Application entry point
│   │
│   ├── Redhead.SitesCatalog.Application/  # Business logic layer
│   │
│   ├── Redhead.SitesCatalog.Domain/       # Domain entities
│   │
│   ├── Redhead.SitesCatalog.Infrastructure/ # Data access layer
│   │
│   ├── Redhead.SitesCatalog.Web/          # React + TypeScript SPA
│   │   ├── src/
│   │   │   ├── App.tsx                     # Main app component
│   │   │   └── main.tsx                    # Entry point
│   │   ├── eslint.config.js                # ESLint configuration
│   │   ├── .prettierrc                     # Prettier configuration
│   │   ├── tsconfig.json                   # TypeScript configuration
│   │   └── package.json                    # NPM dependencies
│   │
│   └── Directory.Build.props               # Shared MSBuild properties
│
├── .editorconfig                           # Code style configuration
├── .gitignore                              # Git ignore rules
├── global.json                             # .NET SDK version pinning
├── nuget.config                            # NuGet package sources
└── redhead-catalog-app.sln                 # Solution file

```

## Development Notes

- The backend uses CORS to allow requests from the frontend (ports 5173-5174)
- The frontend is configured to call the API at `http://localhost:5000`
- Both applications support hot reload during development

## Commit 1: Bootstrap + Linting ✅

This is the initial commit that sets up:
- Complete .NET solution structure with clean architecture
- Vite + React + TypeScript frontend
- Quality gates (linting, formatting, analyzers)
- Minimal health endpoint with UI integration
- All acceptance criteria met:
  - ✅ `dotnet build` succeeds
  - ✅ `npm run lint` succeeds
  - ✅ Running both apps shows health response in UI
