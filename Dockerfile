# Stage 1: build frontend
FROM node:22-alpine AS frontend
WORKDIR /src/web
COPY src/Redhead.SitesCatalog.Web/package.json src/Redhead.SitesCatalog.Web/package-lock.json ./
RUN npm ci
COPY src/Redhead.SitesCatalog.Web/ .
RUN npm run build

# Stage 2: build backend and copy frontend into wwwroot
FROM mcr.microsoft.com/dotnet/sdk:10.0-alpine AS backend
WORKDIR /src
COPY . .
COPY --from=frontend /src/web/dist ./src/Redhead.SitesCatalog.Api/wwwroot/app
RUN dotnet publish src/Redhead.SitesCatalog.Api/Redhead.SitesCatalog.Api.csproj -c Release -o /app/publish

# Stage 3: runtime
FROM mcr.microsoft.com/dotnet/aspnet:10.0-alpine AS runtime
RUN apk add --no-cache \
    icu-libs \
    krb5-libs
ENV DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=false
WORKDIR /app
COPY --from=backend /app/publish .
ENV ASPNETCORE_ENVIRONMENT=Production
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
ENTRYPOINT ["dotnet", "Redhead.SitesCatalog.Api.dll"]
