# Stage 1: Build .NET 10 Web API
FROM mcr.microsoft.com/dotnet/sdk:10.0.402 AS build
WORKDIR /src

# Copy csproj and restore dependencies
COPY ["backend/ShoeExportInvoice.Api/ShoeExportInvoice.Api.csproj", "backend/ShoeExportInvoice.Api/"]
RUN dotnet restore "backend/ShoeExportInvoice.Api/ShoeExportInvoice.Api.csproj"

# Copy source code and publish
COPY backend/ShoeExportInvoice.Api/ backend/ShoeExportInvoice.Api/
WORKDIR "/src/backend/ShoeExportInvoice.Api"
RUN dotnet publish "ShoeExportInvoice.Api.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Stage 2: Final ASP.NET Core Runtime
FROM mcr.microsoft.com/dotnet/aspnet:10.0.13 AS final
WORKDIR /app

# Writable application directories, owned by the built-in unprivileged .NET user
RUN mkdir -p /app/data /app/Templates && chown -R app:app /app/data /app/Templates

COPY --from=build /app/publish .

# Environment variables
ENV ASPNETCORE_URLS=http://+:5270
ENV ASPNETCORE_ENVIRONMENT=Production
USER app
# SQL Server connection and keys must be injected by the operator. No database defaults.

EXPOSE 5270

ENTRYPOINT ["dotnet", "ShoeExportInvoice.Api.dll"]
