# Build stage
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy csproj and restore dependencies
COPY ["FilamentTracker.csproj", "./"]
RUN dotnet restore "FilamentTracker.csproj"

# Copy everything else and build
COPY . .
RUN dotnet build "FilamentTracker.csproj" -c Release -o /app/build

# Publish stage
FROM build AS publish
RUN dotnet publish "FilamentTracker.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app

# Install SQLite (if needed for runtime)
RUN apt-get update && apt-get install -y sqlite3 && rm -rf /var/lib/apt/lists/*

# Copy published app
COPY --from=publish /app/publish .

# Create directory for database with proper permissions
RUN mkdir -p /app/data && chmod 777 /app/data

# Set environment variables
ENV ASPNETCORE_URLS=http://+:5500
ENV ASPNETCORE_ENVIRONMENT=Production

# Expose port
EXPOSE 5500

# Run the application
ENTRYPOINT ["dotnet", "FilamentTracker.dll"]
