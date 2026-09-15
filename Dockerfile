# -------------------------------
# BUILD STAGE
# -------------------------------
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build

WORKDIR /src

# Copy project files first
COPY ["DigiLine.API/DigiLine.API.csproj", "DigiLine.API/"]
COPY ["DigiLine.Application/DigiLine.Application.csproj", "DigiLine.Application/"]
COPY ["DigiLine.Domain/DigiLine.Domain.csproj", "DigiLine.Domain/"]
COPY ["DigiLine.Infrastructure/DigiLine.Infrastructure.csproj", "DigiLine.Infrastructure/"]

# Restore NuGet packages
RUN dotnet restore "DigiLine.API/DigiLine.API.csproj"

# Copy remaining source code
COPY . .

WORKDIR "/src/DigiLine.API"

# Publish the API
RUN dotnet publish "DigiLine.API.csproj" \
    -c Release \
    -o /app/publish \
    /p:UseAppHost=false


# -------------------------------
# RUNTIME STAGE
# -------------------------------
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS final

WORKDIR /app

COPY --from=build /app/publish .

EXPOSE 8080

ENV ASPNETCORE_URLS=http://+:8080

ENTRYPOINT ["dotnet", "DigiLine.API.dll"]