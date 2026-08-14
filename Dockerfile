# Build version 9
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY . .
RUN dotnet restore WebApi/WebApi.csproj
RUN dotnet publish WebApi/WebApi.csproj -c Release -o /app/out

# NOTA: qui c'era anche PublishReadyToRun con -r linux-x64, che precompila
# l'IL in codice nativo per ridurre il tempo di avvio. In locale funziona,
# ma sul builder di Render il build usciva con "Exited with status 1":
# crossgen2 e' molto esigente in memoria e il piano gratuito non ce la fa.
# Rimosso: il guadagno era marginale, il vero taglio al cold start lo fa
# DatabaseWarmupService. Non rimetterlo senza poter testare il build.

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
ENV DOTNET_CLI_TELEMETRY_OPTOUT=1

# Colori nei log di Render.
#
# Non basta ColorBehavior.Enabled in Program.cs: quando l'output non e' un
# terminale vero (ed e' il caso di un container), .NET intercetta le sequenze
# ANSI e le RIMUOVE, provando a usare Console.ForegroundColor, che su un
# output rediretto non fa niente. Risultato: log tutti grigi.
#
# Questa variabile gli dice di lasciare passare l'ANSI. Verificato in locale:
# senza -> 0 righe colorate, con -> 27.
ENV DOTNET_SYSTEM_CONSOLE_ALLOW_ANSI_COLOR_REDIRECTION=1

ENTRYPOINT ["dotnet", "WebApi.dll"]
