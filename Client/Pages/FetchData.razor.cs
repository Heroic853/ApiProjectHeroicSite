using Client.Service;
using Microsoft.AspNetCore.Components;
using SharedLibrary.Dto;
using System.Net.Http;
using System.Net.Http.Json;

namespace Client.Pages
{
    public partial class FetchData
    {
        private HttpClient httpClient;
        private Dragon[]? dragonList;

        [Inject]
        public IHttpClientFactory HttpClientFactory { get; set; }

        protected override async Task OnInitializedAsync() //una get
        {
            httpClient = HttpClientFactory.CreateClient("API");
            dragonList = await Http.GetFromJsonAsync<Dragon[]>("api/dragon");
        }
        [Inject] private ApplicationManager ApplicationManager { get; set; }
    }
}
