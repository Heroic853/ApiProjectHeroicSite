using Microsoft.AspNetCore.Components;
using SharedLibrary.Dto;
using System.Net.Http.Json;

namespace Client.Pages
{
    public partial class AniversaryTable
    {
        private Clasification[]? ClasificationList;
        private Dragon[]? dragonList;

        protected override async Task OnInitializedAsync()
        {
            dragonList = await Http.GetFromJsonAsync<Dragon[]>("api/dragon");
            ClasificationList = await Http.GetFromJsonAsync<Clasification[]>("api/dragon/Clasification");
        }
    }
}