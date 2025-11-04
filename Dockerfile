# ---- runtime ----
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS base
WORKDIR /app
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080

# ---- build/publish ----
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

# Copy everything (avoids per-csproj path mismatches)
COPY . .

# Restore & publish the API (adjust path if your API folder name differs)
RUN dotnet restore Donation/Donation.Api.csproj
WORKDIR /src/Donation
RUN dotnet publish Donation.Api.csproj -c Release -o /out /p:UseAppHost=false

# ---- final image ----
FROM base AS final
WORKDIR /app
COPY --from=build /out .
ENTRYPOINT ["dotnet","Donation.Api.dll"]
