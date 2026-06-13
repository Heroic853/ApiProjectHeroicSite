using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace Client.Pages
{
    public partial class HeroicReturn
    {
        [Inject] IJSRuntime JS { get; set; } = default!;

        private List<string> roarSounds = new()
        {
            "sounds/subsong_0115.wav",
            "sounds/subsong_0147.wav",
            "sounds/subsong_0241.wav",
            "sounds/subsong_0256.wav",
            "sounds/subsong_0405.wav",
            "sounds/subsong_0426.wav",
            "sounds/subsong_0492.wav"
        };
        
        private async Task PlayRandomRoar()
        {
            var random = new Random();
            var sound = roarSounds[random.Next(roarSounds.Count)];
            await JS.InvokeVoidAsync("playSound", sound);
        }
    }
}
