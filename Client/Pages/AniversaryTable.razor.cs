using Client.Service;
using Microsoft.AspNetCore.Components;
using SharedLibrary.Dto;
using System.Net.Http;
using System.Net.Http.Json;

namespace Client.Pages
{
    public partial class AniversaryTable
    {
        [Inject] private ApplicationManager ApplicationManager { get; set; }
       
        private HttpClient httpClient;
        private Clasification[]? ClasificationList;
        private Dragon[]? dragonList;

        protected override async Task OnInitializedAsync() //una get
        {
            dragonList = await Http.GetFromJsonAsync<Dragon[]>("api/dragon");
            ClasificationList = await Http.GetFromJsonAsync<Clasification[]>("api/dragon/Clasification");
        }
    }
}