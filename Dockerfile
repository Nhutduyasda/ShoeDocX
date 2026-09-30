# Build the SPA and API into one HTTPS origin behind Container Apps ingress.
FROM node:22-alpine AS frontend
WORKDIR /src/frontend
COPY frontend/package*.json ./
RUN npm ci
COPY frontend/ ./
RUN npm run build

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS backend
WORKDIR /src
COPY backend/ShoeExportInvoice.Api/ShoeExportInvoice.Api.csproj backend/ShoeExportInvoice.Api/
RUN dotnet restore backend/ShoeExportInvoice.Api/ShoeExportInvoice.Api.csproj
COPY backend/ShoeExportInvoice.Api/ backend/ShoeExportInvoice.Api/
RUN dotnet publish backend/ShoeExportInvoice.Api/ShoeExportInvoice.Api.csproj -c Release -o /out --no-restore /p:UseAppHost=false
COPY --from=frontend /src/frontend/dist /out/wwwroot

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app
COPY --from=backend --chown=app:app /out/ ./
ENV ASPNETCORE_ENVIRONMENT=Production ASPNETCORE_URLS=http://+:8080
USER app
EXPOSE 8080
ENTRYPOINT ["dotnet", "ShoeExportInvoice.Api.dll"]
