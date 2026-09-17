using SharedLibrary.Dto;
using System.Net.Http;
using System.Net.Http.Json;

namespace Client.Pages
{
    public partial class MhxrTicketsAdmin
    {
        private List<MhxrTicketDto>? Tickets;
        private string? loadError;

        private string filtroStato = "";
        private List<MhxrTicketDto> TicketsFiltrati =>
            Tickets is null
                ? new List<MhxrTicketDto>()
                : string.IsNullOrEmpty(filtroStato)
                    ? Tickets
                    : Tickets.Where(t => t.Stato == filtroStato).ToList();

        /// <summary>Righe con il box di risposta aperto.</summary>
        private readonly HashSet<int> rigaAperta = new();

        /// <summary>Testo scritto per ogni ticket, cosi' non si perde riaprendo il box.</summary>
        private readonly Dictionary<int, string> bozzeRisposta = new();

        /// <summary>Righe con un invio in corso: evita il doppio click.</summary>
        private readonly HashSet<int> operazioneInCorso = new();

        private readonly Dictionary<int, string> erroreRiga = new();

        protected override async Task OnInitializedAsync() => await CaricaTicketsAsync();

        private async Task CaricaTicketsAsync()
        {
            try
            {
                Tickets = await Http.GetFromJsonAsync<List<MhxrTicketDto>>("api/mhxr/tickets?limit=200");
            }
            catch (Exception ex)
            {
                loadError = "Could not load the tickets.";
                Console.WriteLine($"[mhxr-admin] {ex.Message}");
            }
        }

        private void ApplicaFiltro()
        {
            // Il filtro e' gia' applicato via TicketsFiltrati: questo esiste solo
            // come "hook" @bind:after, non serve altro codice qui.
        }

        private void ToggleRisposta(int id)
        {
            if (!rigaAperta.Add(id))
                rigaAperta.Remove(id);

            bozzeRisposta.TryAdd(id, "");
        }

        private async Task InviaRisposta(int id)
        {
            erroreRiga.Remove(id);

            var testo = bozzeRisposta.TryGetValue(id, out var t) ? t?.Trim() : null;
            if (string.IsNullOrWhiteSpace(testo))
            {
                erroreRiga[id] = "Write something before sending.";
                return;
            }

            operazioneInCorso.Add(id);
            try
            {
                var risposta = await Http.PostAsJsonAsync(
                    $"api/mhxr/tickets/{id}/rispondi",
                    new MhxrTicketRispostaRequest { Risposta = testo });

                if (risposta.IsSuccessStatusCode)
                {
                    rigaAperta.Remove(id);
                    bozzeRisposta.Remove(id);
                    await CaricaTicketsAsync();
                }
                else
                {
                    erroreRiga[id] = $"Could not send the reply (error {(int)risposta.StatusCode}).";
                }
            }
            catch (Exception ex)
            {
                erroreRiga[id] = "Network error, try again.";
                Console.WriteLine($"[mhxr-admin] {ex.Message}");
            }
            finally
            {
                operazioneInCorso.Remove(id);
            }
        }

        private async Task ChiudiTicket(int id)
        {
            operazioneInCorso.Add(id);
            try
            {
                var risposta = await Http.PostAsJsonAsync($"api/mhxr/tickets/{id}/stato", "Chiuso");
                if (risposta.IsSuccessStatusCode)
                    await CaricaTicketsAsync();
                else
                    erroreRiga[id] = $"Could not close the ticket (error {(int)risposta.StatusCode}).";
            }
            catch (Exception ex)
            {
                erroreRiga[id] = "Network error, try again.";
                Console.WriteLine($"[mhxr-admin] {ex.Message}");
            }
            finally
            {
                operazioneInCorso.Remove(id);
            }
        }

        private static string EtichettaStato(string stato) => stato switch
        {
            "Risposto" => "Answered",
            "Chiuso" => "Closed",
            "Preso in carico" => "In progress",
            _ => "Pending"
        };

        private static string ClasseStato(string stato) => stato switch
        {
            "Risposto" => "ticket-badge-answered",
            "Chiuso" => "ticket-badge-closed",
            "Preso in carico" => "ticket-badge-progress",
            _ => "ticket-badge-pending"
        };
    }
}
