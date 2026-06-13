namespace Client.Pages
{
    public partial class MusicProject
    {
        private bool modalOpen = false;
        private string activeVideoId = "";
        private string activeLabel = "";

        private string videoId1 = "fR7jvDpSFDs";
        private string videoId2 = "FM5pDoJsYyE";
        private string videoId3 = "YB3dK2PPhGY";
        private string videoId4 = "YaVzACrOuMY";
        private string videoId5 = "bmJNaXvjhjM";
        private string videoId6 = "HZMGvLC3nEU";
        private string videoId7 = "WoF7y8jSUWA";

        private string label1 = "";
        private string label2 = "";

        private void OpenModal(string videoId)
        {
            activeVideoId = videoId;
            modalOpen = true;
        }

        private void CloseModal()
        {
            modalOpen = false;
            activeVideoId = "";
        }
    }
}
