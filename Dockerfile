# ---- runtime ----
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS base
WORKDIR /app
# Railway will route to this; Kestrel will bind to 0.0.0.0:8080
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080

# ---- build/publish ----
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

# Copy csproj files first for restore layer caching
COPY Donation.Core/Donation.Core.csproj Donation.Core/
COPY Donation.Application/Donation.Application.csproj Donation.Application/
COPY Donation.Infrastructure/Donation.Infrastructure.csproj Donation.Infrastructure/
COPY Donation.Api/Donation.Api.csproj Donation.Api/

# Restore
RUN dotnet restore Donation.Api/Donation.Api.csproj

# Copy the rest of the source
COPY . .

# Publish API
WORKDIR /src/Donation.Api
RUN dotnet publish -c Release -o /out /p:UseAppHost=false

# ---- final image ----
FROM base AS final
WORKDIR /app
COPY --from=build /out .
ENTRYPOINT ["dotnet","Donation.Api.dll"]
