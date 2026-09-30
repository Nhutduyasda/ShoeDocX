# Stage 1: Build .NET 8 Web API
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy csproj and restore dependencies
COPY ["backend/ShoeExportInvoice.Api/ShoeExportInvoice.Api.csproj", "backend/ShoeExportInvoice.Api/"]
RUN dotnet restore "backend/ShoeExportInvoice.Api/ShoeExportInvoice.Api.csproj"

# Copy source code and publish
COPY backend/ShoeExportInvoice.Api/ backend/ShoeExportInvoice.Api/
WORKDIR "/src/backend/ShoeExportInvoice.Api"
RUN dotnet publish "ShoeExportInvoice.Api.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Stage 2: Final ASP.NET Core Runtime
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app

# Local development volume paths; database is always SQL Server
RUN mkdir -p /app/data /app/Templates

COPY --from=build /app/publish .

# Environment variables
ENV ASPNETCORE_URLS=http://+:5270
ENV ASPNETCORE_ENVIRONMENT=Production

EXPOSE 5270

ENTRYPOINT ["dotnet", "ShoeExportInvoice.Api.dll"]

