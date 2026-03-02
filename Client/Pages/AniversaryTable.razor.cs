using Microsoft.AspNetCore.Components;
using SharedLibrary.Dto;
using System.Net.Http.Json;
using ApexCharts;

namespace Client.Pages
{
    public partial class AniversaryTable
    {
        // Liste per le tabelle esistenti
        private Clasification[]? ClasificationList;
        private Dragon[]? dragonList;

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
            // Caricamento dati esistenti
            dragonList = await Http.GetFromJsonAsync<Dragon[]>("api/dragon");
            ClasificationList = await Http.GetFromJsonAsync<Clasification[]>("api/dragon/Clasification");

            VisitStats = new List<VisitStat>
            {
                new VisitStat { Date = DateTime.Now.AddDays(-3), Count = 45 },
                new VisitStat { Date = DateTime.Now.AddDays(-2), Count = 82 },
                new VisitStat { Date = DateTime.Now.AddDays(-1), Count = 63 },
                new VisitStat { Date = DateTime.Now, Count = 95 }
            };
        }
    }
}