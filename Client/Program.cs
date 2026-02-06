using Client;
using Client.Service;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

//builder.Services.AddHttpClient();

builder.Services.AddScoped(sp => new HttpClient
{
    BaseAddress = new Uri("https://heroic853api-production.up.railway.app/")
});
builder.Services.AddSingleton(new ApplicationManager());

await builder.Build().RunAsync();
