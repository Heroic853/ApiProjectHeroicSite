using ApexCharts;
using Microsoft.AspNetCore.Components;
using SharedLibrary.Dto;
using System.Net.Http;
using System.Net.Http.Json;

namespace Client.Pages
{
    public partial class AniversaryTable
    {
        // Liste per le tabelle esistenti
        private Clasification[]? ClasificationList;
        private Dragon[]? dragonList;

        // --- IMPAGINAZIONE (tutta lato Client: le liste sono gia' intere in memoria) ---
        private const int PageSize = 10;
        private int feedbackPage = 1;
        private int dragonPage = 1;

        private IEnumerable<Clasification> ClasificationPagina =>
            ClasificationList?.Skip((feedbackPage - 1) * PageSize).Take(PageSize) ?? Enumerable.Empty<Clasification>();

        private int FeedbackTotalPages =>
            ClasificationList is null ? 1 : Math.Max(1, (int)Math.Ceiling(ClasificationList.Length / (double)PageSize));

        private IEnumerable<Dragon> DragonPagina =>
            dragonList?.Skip((dragonPage - 1) * PageSize).Take(PageSize) ?? Enumerable.Empty<Dragon>();

        private int DragonTotalPages =>
            dragonList is null ? 1 : Math.Max(1, (int)Math.Ceiling(dragonList.Length / (double)PageSize));

        private void CambiaPaginaFeedback(int delta) =>
            feedbackPage = Math.Clamp(feedbackPage + delta, 1, FeedbackTotalPages);

        private void CambiaPaginaDragon(int delta) =>
            dragonPage = Math.Clamp(dragonPage + delta, 1, DragonTotalPages);

        // --- LOGICA PER IL GRAFICO ---
        private List<VisitStat> VisitStats = new();
        private ApexChart<VisitStat>? visitsChart;

        private bool chartLoading = true;
        private string? chartError;

        // Diventa true quando arrivano dati nuovi, così il grafico si ridisegna
        // una volta sola e non a ogni render
        private bool chartNeedsRefresh;

        // Quanti giorni mostrare nel grafico
        private int chartDays = 30;

        private int TotalVisits => VisitStats.Sum(v => v.Count);
        private int PeakVisits => VisitStats.Count == 0 ? 0 : VisitStats.Max(v => v.Count);

        private ApexChartOptions<VisitStat> ChartOptions = new()
        {
            Theme = new Theme { Mode = Mode.Dark },
            Chart = new Chart
            {
                Background = "transparent",
                ForeColor = "#d4af37",
                Toolbar = new Toolbar { Show = false },
                Animations = new Animations { Enabled = true }
            },
            Colors = new List<string> { "#ffd700" },
            Stroke = new Stroke
            {
                Curve = Curve.Smooth,
                Width = 3
            },
            Grid = new Grid
            {
                BorderColor = "rgba(212, 175, 55, 0.15)"
            },
            Markers = new Markers
            {
                Size = 4,
                Colors = new List<string> { "#8b0000" },
                StrokeColors = "#ffd700",
                StrokeWidth = 2
            },
            Xaxis = new XAxis
            {
                Labels = new XAxisLabels
                {
                    Style = new AxisLabelStyle { Colors = "#c0c0c0" },
                    // Con 30 giorni le etichette si sovrappongono: mostrane una parte
                    Rotate = -45,
                    HideOverlappingLabels = true
                },
                AxisBorder = new AxisBorder { Color = "rgba(212, 175, 55, 0.3)" }
            },
            Yaxis = new List<YAxis>
            {
                new YAxis
                {
                    Labels = new YAxisLabels
                    {
                        Style = new AxisLabelStyle { Colors = new List<string> { "#c0c0c0" } },
                        // Le visite sono numeri interi: senza questo l'asse
                        // mostra 0.5, 1.5, 2.5 e sembra rotto
                        Formatter = "function (val) { return val.toFixed(0); }"
                    },
                    Min = 0,
                    ForceNiceScale = true
                }
            },
            NoData = new NoData
            {
                Text = "Nessuna visita registrata in questo periodo",
                Style = new NoDataStyle { Color = "#c0c0c0" }
            }
        };

        protected override async Task OnInitializedAsync()
        {
            // Le tre chiamate sono indipendenti: lanciarle in parallelo invece
            // che in fila taglia il tempo di caricamento della pagina admin.
            var dragonsTask = Http.GetFromJsonAsync<Dragon[]>("api/dragon");
            var clasificationTask = Http.GetFromJsonAsync<Clasification[]>("api/dragon/Clasification");
            var statsTask = LoadVisitStatsAsync();

            try
            {
                await Task.WhenAll(dragonsTask, clasificationTask, statsTask);
            }
            catch
            {
                // I singoli errori li gestiscono i blocchi sotto / LoadVisitStatsAsync
            }

            if (dragonsTask.IsCompletedSuccessfully) dragonList = dragonsTask.Result;
            if (clasificationTask.IsCompletedSuccessfully) ClasificationList = clasificationTask.Result;
        }

        private async Task LoadVisitStatsAsync()
        {
            chartLoading = true;
            chartError = null;

            try
            {
                // Usa "Http" (il client con il token Auth0), non quello anonimo:
                // daily-stats ora richiede il ruolo Admin. Questa pagina è già
                // dentro <AuthorizeView Roles="Admin">, quindi il token c'è.
                var stats = await Http.GetFromJsonAsync<List<VisitStat>>(
                    $"api/dragon/daily-stats?days={chartDays}");

                VisitStats = stats ?? new List<VisitStat>();
            }
            catch (Exception ex)
            {
                chartError = ex.Message;
                VisitStats = new List<VisitStat>();
                Console.WriteLine($"[daily-stats] errore: {ex.Message}");
            }
            finally
            {
                chartLoading = false;
                chartNeedsRefresh = true;
            }
        }

        /// <summary>
        /// ApexCharts disegna il grafico in JavaScript al primo render.
        /// I dati arrivano dopo (la chiamata HTTP è asincrona), quindi bisogna
        /// dirgli esplicitamente di ridisegnarsi: prima non lo si faceva e il
        /// grafico restava vuoto anche quando l'API rispondeva correttamente.
        /// </summary>
        protected override async Task OnAfterRenderAsync(bool firstRender)
        {
            if (chartNeedsRefresh && visitsChart is not null)
            {
                chartNeedsRefresh = false;
                await visitsChart.UpdateSeriesAsync(animate: true);
            }
        }

        private async Task ChangeRange(int days)
        {
            if (days == chartDays) return;

            chartDays = days;
            await LoadVisitStatsAsync();
            StateHasChanged();
        }
    }
}
