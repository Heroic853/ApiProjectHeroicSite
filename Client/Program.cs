using Client;
using Client.Service;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Microsoft.AspNetCore.Components.WebAssembly.Authentication;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

// HTTP client che aggiunge automaticamente il token Auth0 alle richieste
builder.Services.AddHttpClient("ServerAPI", client =>
    client.BaseAddress = new Uri("https://apiprojectheroicsite.onrender.com/"))
    .AddHttpMessageHandler(sp =>
    {
        var handler = sp.GetRequiredService<AuthorizationMessageHandler>();
        handler.ConfigureHandler(
            authorizedUrls: new[] { "https://apiprojectheroicsite.onrender.com" });
        return handler;
    });

builder.Services.AddScoped(sp => sp.GetRequiredService<IHttpClientFactory>()
    .CreateClient("ServerAPI"));
builder.Services.AddHttpClient("Anonymous", client =>
    client.BaseAddress = new Uri("https://apiprojectheroicsite.onrender.com/"));

//Auth0
builder.Services.AddOidcAuthentication(options =>
{
    builder.Configuration.Bind("Auth0", options.ProviderOptions);
    options.ProviderOptions.ResponseType = "code";
    options.ProviderOptions.AdditionalProviderParameters.Add(
        "audience", builder.Configuration["Auth0:Audience"]);
    options.ProviderOptions.DefaultScopes.Add("openid");
    options.ProviderOptions.DefaultScopes.Add("profile");
    options.ProviderOptions.DefaultScopes.Add("email");
    // Dove Auth0 rimanda dopo il logout. Il valore sta in appsettings.json
    // perche' lo usa anche Authentication.razor: prima era scritto a mano nei
    // due file e uno dei due era senza lo slash finale.
    //
    // Deve combaciare esattamente con un Allowed Logout URL su Auth0: senza lo
    // slash finale Auth0 risponde 400 e il logout non si completa.
    options.ProviderOptions.PostLogoutRedirectUri =
        builder.Configuration["Auth0:PostLogoutRedirectUri"]
        ?? "https://heroic853.github.io/Heroic853SiteV1/";
}).AddAccountClaimsPrincipalFactory<CustomUserFactory>();

builder.Services.AddSingleton(new ApplicationManager());
await builder.Build().RunAsync();