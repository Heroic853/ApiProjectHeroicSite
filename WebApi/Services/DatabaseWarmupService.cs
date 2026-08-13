using System.Diagnostics;
using Microsoft.EntityFrameworkCore;
using WebApi.Data;

namespace WebApi.Services
{
    /// <summary>
    /// Scalda il DbContext appena l'app parte, in background.
    ///
    /// Perche' serve: la prima chiamata che tocca il database paga tutto in una volta —
    /// EF costruisce il modello, Npgsql apre la connessione e fa il TLS handshake, e se il
    /// Postgres e' sospeso deve svegliarsi. Misurato in produzione: ~4.6s la prima, ~0.17s le dopo.
    ///
    /// Facendo il warm-up qui, quel costo lo paga il server all'avvio e non il primo visitatore.
    /// Gira in background (non blocca lo start) cosi' la porta si apre subito e Render
    /// non fa fallire l'health check.
    /// </summary>
    public class DatabaseWarmupService : BackgroundService
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<DatabaseWarmupService> _logger;

        public DatabaseWarmupService(IServiceScopeFactory scopeFactory, ILogger<DatabaseWarmupService> logger)
        {
            _scopeFactory = scopeFactory;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            // Lascia finire l'avvio del server prima di occupare il thread pool
            await Task.Yield();

            var total = Stopwatch.StartNew();
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<DragonListDbContext>();

                // 1. Costruisce il modello EF (la parte piu' lenta del primo giro)
                var modelWatch = Stopwatch.StartNew();
                _ = db.Model;
                modelWatch.Stop();
                _logger.LogInformation("WARMUP: modello EF costruito in {Elapsed}ms", modelWatch.ElapsedMilliseconds);

                // 2. Apre davvero la connessione e popola il pool
                var connectWatch = Stopwatch.StartNew();
                await db.Database.OpenConnectionAsync(stoppingToken);
                connectWatch.Stop();
                _logger.LogInformation("WARMUP: connessione al database aperta in {Elapsed}ms", connectWatch.ElapsedMilliseconds);

                // 3. Esegue una query reale su ogni tabella letta dal sito, cosi' EF
                //    compila i query plan e non li ricompila alla prima richiesta vera
                var queryWatch = Stopwatch.StartNew();
                // L'OrderBy serve solo a rendere deterministico il Take(1):
                // senza, EF logga un warning a ogni avvio.
                _ = await db.DragonSet.AsNoTracking().OrderBy(d => d.Id).Take(1).ToListAsync(stoppingToken);
                _ = await db.Clasification.AsNoTracking().OrderBy(c => c.Id).Take(1).ToListAsync(stoppingToken);
                _ = await db.User.AsNoTracking().OrderBy(u => u.Id).Take(1).ToListAsync(stoppingToken);

                // Stessa forma di query di /daily-stats (filtro per data + GroupBy):
                // scalda il piano di quella query, che e' la piu' pesante del sito.
                // Il DateTime va marcato UTC perche' visited_at e' timestamptz e
                // Npgsql rifiuta un parametro con Kind=Unspecified.
                var since = DateTime.SpecifyKind(DateTime.UtcNow.Date, DateTimeKind.Utc).AddDays(-29);
                var sample = await db.PageVisits.AsNoTracking()
                    .Where(v => v.VisitedAt >= since)
                    .GroupBy(v => v.VisitedAt.Date)
                    .Select(g => new { Date = g.Key, Count = g.Count() })
                    .ToListAsync(stoppingToken);

                queryWatch.Stop();
                _logger.LogInformation(
                    "WARMUP: query eseguite in {Elapsed}ms (statistiche visite: {Days} giorni con traffico negli ultimi 30, {Total} visite)",
                    queryWatch.ElapsedMilliseconds,
                    sample.Count,
                    sample.Sum(s => s.Count));

                await db.Database.CloseConnectionAsync();

                total.Stop();
                _logger.LogInformation(
                    "WARMUP COMPLETATO in {Elapsed}ms — le prossime chiamate partono calde",
                    total.ElapsedMilliseconds);
            }
            catch (OperationCanceledException)
            {
                // L'app si sta spegnendo, normale
            }
            catch (Exception ex)
            {
                // Il warm-up e' un'ottimizzazione: se fallisce l'app deve restare in piedi
                _logger.LogError(ex,
                    "WARMUP FALLITO dopo {Elapsed}ms — l'app continua, ma la prima chiamata sara' lenta",
                    total.ElapsedMilliseconds);
            }
        }
    }
}
