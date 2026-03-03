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
        [Inject] private IHttpClientFactory HttpClientFactory { get; set; }

        // --- LOGICA PER IL GRAFICO ---
        private List<VisitStat> VisitStats = new();

        private ApexChartOptions<VisitStat> ChartOptions = new()
        {
            Theme = new Theme { Mode = Mode.Dark },
            Chart = new Chart
            {
                Background = "transparent",
                ForeColor = "#d4af37"
            },
            Colors = new List<string> { "#8b0000" },
            Xaxis = new XAxis
            {
                Labels = new XAxisLabels { Style = new AxisLabelStyle { Colors = "#c0c0c0" } }
            }
        };

        protected override async Task OnInitializedAsync()
        {
            var anonClient = HttpClientFactory.CreateClient("Anonymous");


            //dati esistenti 
            dragonList = await Http.GetFromJsonAsync<Dragon[]>("api/dragon");
            ClasificationList = await Http.GetFromJsonAsync<Clasification[]>("api/dragon/Clasification");

            var stats = await anonClient.GetFromJsonAsync<List<VisitStat>>(
                "https://apiprojectheroicsite.onrender.com/api/dragon/daily-stats");
            if (stats != null)
                VisitStats = stats;
        }
    }
}