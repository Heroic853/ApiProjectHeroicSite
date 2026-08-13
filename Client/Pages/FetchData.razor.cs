using SharedLibrary.Dto;
using System.Net.Http;
using System.Net.Http.Json;

namespace Client.Pages
{
    public partial class FetchData
    {
        private Dragon[]? dragonList;

        protected override async Task OnInitializedAsync() //una get
        {
            try
            {
                dragonList = await Http.GetFromJsonAsync<Dragon[]>("api/dragon");
            }
            catch (Exception ex)
            {
                // api/dragon ora richiede il token: se non sei loggato la
                // chiamata fallisce, e senza questo catch la pagina crashava.
                Console.WriteLine($"[fetchdata] {ex.Message}");
            }
        }
    }
}
