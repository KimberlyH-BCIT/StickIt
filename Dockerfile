# ELKH Sticker Store - Production Container
# Multi-stage build for optimized production deployment
# Features: Security hardening, minimal runtime, optimized image size

# ====================================
# Stage 1: Build Environment
# ====================================
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy project files and restore dependencies
# Copy in dependency order for better caching
COPY ["ELKH/ELKH.csproj", "ELKH/"]
COPY ["ELKH.Tests/ELKH.Tests.csproj", "ELKH.Tests/"]
RUN dotnet restore "ELKH/ELKH.csproj"

# Copy source code and build application
COPY . .
WORKDIR "/src/ELKH"

# Build in Release mode with optimizations
RUN dotnet build "ELKH.csproj" -c Release -o /app/build --no-restore

# ====================================
# Stage 2: Publish Optimized Build
# ====================================
FROM build AS publish
RUN dotnet publish "ELKH.csproj" \
    -c Release \
    -o /app/publish \
    --no-restore \
    --no-build \
    /p:PublishTrimmed=false \
    /p:PublishSingleFile=false

# ====================================
# Stage 3: Production Runtime
# ====================================
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime

# Create non-root user for security
RUN groupadd -r elkh && useradd -r -g elkh elkh

# Set working directory
WORKDIR /app

# Install necessary packages for production
RUN apt-get update \
    && apt-get install -y --no-install-recommends \
        curl \
        ca-certificates \
    && rm -rf /var/lib/apt/lists/*

# Copy published application
COPY --from=publish /app/publish .

# Create necessary directories with proper permissions
RUN mkdir -p /app/wwwroot/uploads \
    && mkdir -p /app/Data \
    && mkdir -p /app/logs \
    && chown -R elkh:elkh /app

# ====================================
# Production Configuration
# ====================================

# Environment variables for production
ENV ASPNETCORE_ENVIRONMENT=Production
ENV ASPNETCORE_URLS=http://+:80
ENV ASPNETCORE_HTTP_PORTS=80
ENV DOTNET_RUNNING_IN_CONTAINER=true
ENV DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=false

# Security: Run as non-root user
USER elkh

# Health check endpoint
HEALTHCHECK --interval=30s --timeout=10s --start-period=60s --retries=3 \
    CMD curl -f http://localhost/health || exit 1

# Expose port
EXPOSE 80

# Set entrypoint
ENTRYPOINT ["dotnet", "ELKH.dll"]

# ====================================
# Usage Examples
# ====================================
# 
# Development build:
# docker build -t elkh-stickers:dev .
# docker run -p 8080:80 elkh-stickers:dev
#
# Production build with environment variables:
# docker run -p 80:80 \
#   -e ConnectionStrings__DefaultConnection="your-connection-string" \
#   -e Authentication__Google__ClientId="your-client-id" \
#   elkh-stickers:latest
#
# With volume mounts for data persistence:
# docker run -p 80:80 \
#   -v elkh-data:/app/Data \
#   -v elkh-uploads:/app/wwwroot/uploads \
#   elkh-stickers:latest