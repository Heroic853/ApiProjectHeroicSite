# Build version 6 - Debug
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY . .

# Verifica che il progetto esista
RUN ls -la WebApi/

# Prova a fare restore prima
RUN dotnet restore WebApi/WebApi.csproj

# Poi publish con output verboso
RUN dotnet publish WebApi/WebApi.csproj -c Release -o /app/out --verbosity detailed

# Vediamo cosa è stato generato
RUN ls -la /app/out/

FROM base AS final
WORKDIR /app
COPY --from=build /app/out .

# Vediamo cosa c'è nella directory finale
RUN ls -la

ENV ASPNETCORE_URLS=http://+:8080
ENTRYPOINT ["dotnet", "WebApi.dll"]