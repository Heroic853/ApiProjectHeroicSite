using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.WebAssembly.Authentication;
using Microsoft.JSInterop;
using SharedLibrary.Dto;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;

namespace Client.Pages
{
    public partial class MhxrFeedback : IDisposable
    {
        [Inject] private IHttpClientFactory HttpClientFactory { get; set; } = default!;
        [Inject] private AuthenticationStateProvider AuthStateProvider { get; set; } = default!;
        [Inject] private HttpClient Http { get; set; } = default!;
        [Inject] private IJSRuntime JS { get; set; } = default!;

        // Chiavi in localStorage: e' il "biglietto" che permette a chi NON e'
        // loggato di ritrovare il proprio ticket dopo aver lasciato la pagina.
        // Non e' un cookie di sessione: sopravvive alla chiusura del browser,
        // ma resta legato a QUESTO browser/dispositivo (cambiando browser o
        // svuotando i dati del sito lo si perde, e non c'e' altro modo per
        // recuperarlo senza fare login).
        private const string ChiaveTicketId = "mhxr_ticket_id";
        private const string ChiaveTicketToken = "mhxr_ticket_token";

        /// <summary>Ogni quanto la pagina si aggiorna da sola mentre un ticket e' aperto.</summary>
        private static readonly TimeSpan IntervalloAggiornamento = TimeSpan.FromSeconds(6);

        private string categoria = "";
        private string messaggio = "";

        /// <summary>
        /// Facoltativo: solo per chi non e' loggato, cosi' so chi mi ha
        /// scritto senza fargli fare un login vero (che dentro la webview
        /// del gioco puo' non funzionare). Il server lo ignora comunque se
        /// chi manda ha un token valido: non e' un modo per spacciarsi per
        /// un altro account.
        /// </summary>
        private string nomeAnonimo = "";

        private bool invioInCorso;
        private string errore = "";

        /// <summary>
        /// L'ultimo ticket ancora aperto: di chi e' loggato (letto dal token)
        /// oppure, per chi non lo e', quello salvato in localStorage. Se non e'
        /// null il modulo di invio resta nascosto: un solo ticket alla volta,
        /// finche' l'ADMIN non lo chiude (l'utente non ha questo potere).
        /// </summary>
        private MhxrTicketDto? ticketAttivo;

        /// <summary>
        /// true se ticketAttivo viene dal percorso anonimo (localStorage):
        /// serve a InviaMessaggio per sapere quale endpoint chiamare, visto
        /// che per chi non e' loggato non c'e' un token da mandare, serve
        /// invece il "biglietto" salvato.
        /// </summary>
        private bool ticketAttivoEAnonimo;

        /// <summary>I TUOI ticket gia' chiusi, per lo storico in fondo alla pagina.</summary>
        private List<MhxrTicketDto> storico = new();

        /// <summary>Quali righe dello storico sono aperte per leggere la conversazione.</summary>
        private readonly HashSet<int> storicoEspanso = new();

        private void ToggleStorico(int idTicket)
        {
            if (!storicoEspanso.Add(idTicket))
                storicoEspanso.Remove(idTicket);
        }

        private bool caricamentoIniziale = true;

        private string nuovoMessaggio = "";
        private bool invioMessaggioInCorso;

        private bool MostraModulo => ticketAttivo is null;

        private int LunghezzaMessaggio => messaggio?.Length ?? 0;

        private PeriodicTimer? _timerAggiornamento;
        private CancellationTokenSource? _ctsAggiornamento;

        protected override async Task OnInitializedAsync()
        {
            var stato = await AuthStateProvider.GetAuthenticationStateAsync();
            var loggato = stato.User.Identity?.IsAuthenticated == true;

            if (loggato)
                await CaricaTicketAttivoAsync();
            else
                await CaricaTicketAnonimoAsync();

            await CaricaStoricoAsync(loggato);

            caricamentoIniziale = false;

            // Parte una volta e resta attivo per tutta la vita della pagina:
            // ogni giro controlla da solo se c'e' davvero un ticket da
            // aggiornare, quindi non serve fermarlo/riavviarlo quando si apre
            // o si chiude un ticket.
            _ctsAggiornamento = new CancellationTokenSource();
            _ = CicloAggiornamentoAsync(_ctsAggiornamento.Token);
        }

        /// <summary>
        /// Rilegge il ticket attivo a intervalli regolari, cosi' un nuovo
        /// messaggio (mio o dell'admin) compare senza dover ricaricare la
        /// pagina a mano.
        /// </summary>
        private async Task CicloAggiornamentoAsync(CancellationToken ct)
        {
            _timerAggiornamento = new PeriodicTimer(IntervalloAggiornamento);
            try
            {
                while (await _timerAggiornamento.WaitForNextTickAsync(ct))
                {
                    if (ticketAttivo is null)
                        continue;

                    var eraAnonimo = ticketAttivoEAnonimo;

                    if (eraAnonimo)
                        await CaricaTicketAnonimoAsync();
                    else
                        await CaricaTicketAttivoAsync();

                    // Se e' appena passato a null e' perche' l'admin lo ha
                    // chiuso proprio ora: rinfresca anche lo storico, cosi'
                    // il ticket ci compare subito invece che al prossimo
                    // caricamento della pagina.
                    if (ticketAttivo is null)
                        await CaricaStoricoAsync(!eraAnonimo);

                    await InvokeAsync(StateHasChanged);
                }
            }
            catch (OperationCanceledException)
            {
                // Normale quando si lascia la pagina, vedi Dispose
            }
        }

        public void Dispose()
        {
            _ctsAggiornamento?.Cancel();
            _ctsAggiornamento?.Dispose();
            _timerAggiornamento?.Dispose();
        }

        private async Task CaricaTicketAttivoAsync()
        {
            try
            {
                var ultimo = await Http.GetFromJsonAsync<MhxrTicketDto?>("api/mhxr/mio-ticket");
                ticketAttivo = ultimo is { Stato: not "Chiuso" } ? ultimo : null;
                ticketAttivoEAnonimo = false;
            }
            catch (Exception ex)
            {
                // Se questa chiamata fallisce si mostra comunque il modulo:
                // meglio lasciar scrivere un ticket che bloccare la pagina.
                Console.WriteLine($"[mhxr-ticket] impossibile leggere il ticket attivo: {ex.Message}");
            }
        }

        /// <summary>
        /// Legge id+token salvati in localStorage (se ci sono) e chiede al
        /// server lo stato di quel ticket. Se il ticket e' stato chiuso (solo
        /// l'admin puo' farlo), o il biglietto non e' piu' valido, lo si
        /// dimentica: cosi' la pagina torna a mostrare il modulo per un
        /// ticket nuovo.
        /// </summary>
        private async Task CaricaTicketAnonimoAsync()
        {
            try
            {
                var (id, token) = await LeggiBigliettoAnonimoAsync();
                if (id is null || string.IsNullOrEmpty(token))
                    return;

                var anonClient = HttpClientFactory.CreateClient("Anonymous");
                var risposta = await anonClient.GetAsync(
                    $"api/mhxr/ticket-anonimo/{id}?token={Uri.EscapeDataString(token)}");

                if (!risposta.IsSuccessStatusCode)
                {
                    await DimenticaBigliettoAnonimoAsync();
                    return;
                }

                var ticket = await risposta.Content.ReadFromJsonAsync<MhxrTicketDto>();
                if (ticket is null || ticket.Stato == "Chiuso")
                {
                    await DimenticaBigliettoAnonimoAsync();
                    ticketAttivo = null;
                    return;
                }

                ticketAttivo = ticket;
                ticketAttivoEAnonimo = true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[mhxr-ticket] impossibile leggere il ticket anonimo: {ex.Message}");
            }
        }

        /// <summary>
        /// I ticket gia' chiusi: di chi e' loggato (per Autore) oppure, per
        /// chi non lo e', quelli scritti dallo stesso indirizzo IP (nessun
        /// "biglietto" qui: dopo il ticket successivo il Client sovrascrive
        /// quello salvato, quelli vecchi non sarebbero piu' raggiungibili).
        /// </summary>
        private async Task CaricaStoricoAsync(bool loggato)
        {
            try
            {
                storico = loggato
                    ? await Http.GetFromJsonAsync<List<MhxrTicketDto>>("api/mhxr/mio-storico") ?? new()
                    : await HttpClientFactory.CreateClient("Anonymous")
                        .GetFromJsonAsync<List<MhxrTicketDto>>("api/mhxr/storico-anonimo") ?? new();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[mhxr-ticket] impossibile leggere lo storico: {ex.Message}");
            }
        }

        private async Task<(int? Id, string? Token)> LeggiBigliettoAnonimoAsync()
        {
            try
            {
                var idTesto = await JS.InvokeAsync<string?>("localStorage.getItem", ChiaveTicketId);
                var token = await JS.InvokeAsync<string?>("localStorage.getItem", ChiaveTicketToken);

                return int.TryParse(idTesto, out var id) && !string.IsNullOrEmpty(token)
                    ? (id, token)
                    : (null, null);
            }
            catch
            {
                // localStorage puo' non essere disponibile (es. navigazione
                // privata con dati bloccati): meglio un ticket non trovato
                // che una pagina rotta.
                return (null, null);
            }
        }

        private async Task SalvaBigliettoAnonimoAsync(int id, string token)
        {
            try
            {
                await JS.InvokeVoidAsync("localStorage.setItem", ChiaveTicketId, id.ToString());
                await JS.InvokeVoidAsync("localStorage.setItem", ChiaveTicketToken, token);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[mhxr-ticket] impossibile salvare il biglietto: {ex.Message}");
            }
        }

        private async Task DimenticaBigliettoAnonimoAsync()
        {
            try
            {
                await JS.InvokeVoidAsync("localStorage.removeItem", ChiaveTicketId);
                await JS.InvokeVoidAsync("localStorage.removeItem", ChiaveTicketToken);
            }
            catch
            {
                // niente da fare se anche questo fallisce
            }
        }

        private async Task InviaTicket()
        {
            errore = "";
            if (string.IsNullOrWhiteSpace(categoria))
            {
                errore = "Choose what kind of problem it is.";
                return;
            }

            if (string.IsNullOrWhiteSpace(messaggio))
            {
                errore = "Write a short description before sending.";
                return;
            }

            invioInCorso = true;
            try
            {
                var client = await ScegliClientAsync();

                var response = await client.PostAsJsonAsync(
                    "api/mhxr/ticket",
                    new MhxrTicketRequest
                    {
                        Categoria = categoria,
                        Messaggio = messaggio,
                        NomeAnonimo = nomeAnonimo
                    });

                if (response.IsSuccessStatusCode)
                {
                    var dati = await response.Content.ReadFromJsonAsync<JsonElement>();

                    var id = dati.TryGetProperty("id", out var idProp) ? idProp.GetInt32() : 0;
                    var anonimo = !dati.TryGetProperty("anonimo", out var anonProp) || anonProp.GetBoolean();

                    if (anonimo)
                    {
                        // Il server manda anche un "biglietto" solo per i ticket
                        // anonimi: senza account e' l'unico modo per ritrovare
                        // questo ticket dopo aver lasciato la pagina.
                        var token = dati.TryGetProperty("lookupToken", out var tokenProp)
                            ? tokenProp.GetString()
                            : null;

                        if (!string.IsNullOrEmpty(token))
                            await SalvaBigliettoAnonimoAsync(id, token);

                        ticketAttivo = new MhxrTicketDto
                        {
                            Id = id,
                            Categoria = categoria,
                            Anonimo = true,
                            Stato = "Aperto",
                            CreatedAt = DateTime.UtcNow,
                            Messaggi = new List<MhxrTicketMessaggioDto>
                            {
                                new()
                                {
                                    Mittente = MhxrMittente.Utente,
                                    Testo = messaggio,
                                    CreatedAt = DateTime.UtcNow
                                }
                            }
                        };
                        ticketAttivoEAnonimo = true;
                    }
                    else
                    {
                        // Chi e' loggato passa direttamente alla schermata di stato:
                        // e' anche il modo in cui la pagina applica "un ticket alla
                        // volta", perche' MostraModulo torna false finche' non e'
                        // chiuso dall'admin.
                        await CaricaTicketAttivoAsync();
                    }

                    return;
                }

                errore = response.StatusCode switch
                {
                    System.Net.HttpStatusCode.TooManyRequests =>
                        "You already sent several tickets. Please wait a bit before sending another one.",
                    System.Net.HttpStatusCode.Conflict =>
                        await LeggiMessaggio(response) ?? "You already have an open ticket.",
                    System.Net.HttpStatusCode.BadRequest =>
                        await LeggiMessaggio(response) ?? "Check the fields and try again.",
                    _ =>
                        $"Could not send the ticket (error {(int)response.StatusCode}). Please try again later."
                };

                // Se il server ha rifiutato perche' c'e' gia' un ticket aperto,
                // si allinea la pagina con quello vero invece di lasciarla
                // a mostrare un modulo che tanto verra' rifiutato di nuovo.
                if (response.StatusCode == System.Net.HttpStatusCode.Conflict)
                {
                    var stato = await AuthStateProvider.GetAuthenticationStateAsync();
                    if (stato.User.Identity?.IsAuthenticated == true)
                        await CaricaTicketAttivoAsync();
                    else
                        await CaricaTicketAnonimoAsync();
                }
            }
            catch (AccessTokenNotAvailableException)
            {
                // Non dovrebbe capitare: ScegliClientAsync usa il client anonimo
                // per chi non e' loggato. Resta come rete di sicurezza.
                errore = "Your session expired. Reload the page and try again.";
            }
            catch (Exception ex)
            {
                errore = "Network error. Check your connection and try again.";
                Console.WriteLine($"[mhxr-ticket] {ex.Message}");
            }
            finally
            {
                invioInCorso = false;
            }
        }

        /// <summary>
        /// Aggiunge un messaggio al ticket gia' aperto. Non chiude/riapre
        /// nulla lato Client: lo stato che torna dal server (che intanto ha
        /// rimesso il ticket ad "Aperto", il tuo turno) e' quello che conta.
        /// </summary>
        private async Task InviaMessaggio()
        {
            if (ticketAttivo is null || invioMessaggioInCorso)
                return;

            var testo = nuovoMessaggio.Trim();
            if (string.IsNullOrWhiteSpace(testo))
                return;

            invioMessaggioInCorso = true;
            try
            {
                HttpResponseMessage risposta;

                if (ticketAttivoEAnonimo)
                {
                    var (_, token) = await LeggiBigliettoAnonimoAsync();
                    var anonClient = HttpClientFactory.CreateClient("Anonymous");
                    risposta = await anonClient.PostAsJsonAsync(
                        $"api/mhxr/ticket-anonimo/{ticketAttivo.Id}/messaggi?token={Uri.EscapeDataString(token ?? "")}",
                        new MhxrTicketMessaggioRequest { Testo = testo });
                }
                else
                {
                    risposta = await Http.PostAsJsonAsync(
                        $"api/mhxr/ticket/{ticketAttivo.Id}/messaggi",
                        new MhxrTicketMessaggioRequest { Testo = testo });
                }

                if (risposta.IsSuccessStatusCode)
                {
                    var aggiornato = await risposta.Content.ReadFromJsonAsync<MhxrTicketDto>();
                    if (aggiornato is not null)
                        ticketAttivo = aggiornato;

                    nuovoMessaggio = "";
                }
                else
                {
                    Console.WriteLine($"[mhxr-ticket] invio messaggio fallito: {(int)risposta.StatusCode}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[mhxr-ticket] invio messaggio fallito: {ex.Message}");
            }
            finally
            {
                invioMessaggioInCorso = false;
            }
        }

        /// <summary>
        /// Sceglie con quale client mandare la richiesta.
        ///
        /// L'endpoint accetta tutti, ma il server deve poter capire CHI scrive
        /// quando c'e' un account. Il client "Anonymous" non manda mai il token,
        /// mentre quello autenticato lancia un'eccezione se il token non c'e':
        /// quindi si guarda prima lo stato di autenticazione e si sceglie.
        /// </summary>
        private async Task<HttpClient> ScegliClientAsync()
        {
            var stato = await AuthStateProvider.GetAuthenticationStateAsync();

            return stato.User.Identity?.IsAuthenticated == true
                ? Http
                : HttpClientFactory.CreateClient("Anonymous");
        }

        /// <summary>Legge il campo "message" dalla risposta di errore dell'API.</summary>
        private static async Task<string?> LeggiMessaggio(HttpResponseMessage response)
        {
            try
            {
                var json = await response.Content.ReadFromJsonAsync<JsonElement>();
                return json.TryGetProperty("message", out var m) ? m.GetString() : null;
            }
            catch
            {
                return null;
            }
        }

        /// <summary>Etichetta in inglese per il badge di stato del ticket.</summary>
        private static string EtichettaStato(string stato) => stato switch
        {
            "Risposto" => "Answered",
            "Chiuso" => "Closed",
            "Preso in carico" => "In progress",
            _ => "Pending"
        };

        /// <summary>Classe CSS del badge, coerente col colore dell'etichetta sopra.</summary>
        private static string ClasseStato(string stato) => stato switch
        {
            "Risposto" => "mhxr-badge-answered",
            "Chiuso" => "mhxr-badge-closed",
            "Preso in carico" => "mhxr-badge-progress",
            _ => "mhxr-badge-pending"
        };
    }
}
