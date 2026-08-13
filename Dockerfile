# Build version 8
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Prima solo i .csproj: se non cambiano le dipendenze, Docker riusa la cache
# del restore e il deploy diventa molto piu' rapido.
COPY WebApi/WebApi.csproj WebApi/
COPY SharedLibrary/SharedLibrary.csproj SharedLibrary/
RUN dotnet restore WebApi/WebApi.csproj -r linux-x64

# Poi il codice (il progetto Client non serve all'API, non viene copiato)
COPY WebApi/ WebApi/
COPY SharedLibrary/ SharedLibrary/

# PublishReadyToRun precompila l'IL in codice nativo: all'avvio il runtime non
# deve piu' compilare tutto col JIT. E' la voce che pesa piu' di tutte sul
# cold start di un container che si e' appena svegliato.
#
# NOTA: richiede un'immagine linux-x64. Se il build su Render dovesse fallire,
# togli le tre opzioni "-r / --self-contained / PublishReadyToRun".
RUN dotnet publish WebApi/WebApi.csproj \
    -c Release \
    -o /app/out \
    -r linux-x64 \
    --self-contained false \
    --no-restore \
    -p:PublishReadyToRun=true

FROM base AS final
WORKDIR /app
COPY --from=build /app/out .

ENV ASPNETCORE_URLS=http://+:8080

# Spegne il ricaricamento a caldo di appsettings.json.
#
# Perche': CreateBuilder registra i file di configurazione con
# reloadOnChange:true, cioe' apre un FileSystemWatcher (inotify) per ognuno.
# Su Render l'host arriva al limite di 128 istanze inotify e l'app muore
# all'avvio con "The configured user limit (128) on the number of inotify
# instances has been reached", entrando in loop di riavvio.
#
# In un container i file di configurazione non cambiano mai a runtime, quindi
# quei watcher sono solo uno spreco che puo' impedire l'avvio.
ENV DOTNET_hostBuilder__reloadConfigOnChange=false

# Il container ha gia' un solo processo: il server non deve competere con
# la telemetria di primo avvio.
ENV DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=false
ENV DOTNET_CLI_TELEMETRY_OPTOUT=1

ENTRYPOINT ["dotnet", "WebApi.dll"]
