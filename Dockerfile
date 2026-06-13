# Build version 7
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY . .
RUN dotnet restore WebApi/WebApi.csproj
RUN dotnet publish WebApi/WebApi.csproj -c Release -o /app/out

FROM base AS final
WORKDIR /app
COPY --from=build /app/out .
ENV ASPNETCORE_URLS=http://+:8080
ENTRYPOINT ["dotnet", "WebApi.dll"]