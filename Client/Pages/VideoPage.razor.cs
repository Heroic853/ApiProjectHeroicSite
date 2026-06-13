namespace Client.Pages
{
    public partial class VideoPage
    {
        private bool modalOpen = false;
        private string activeVideoId = "";
        private string activeLabel = "";

        private string videoId1 = "FxDkoGhdFZc";
        private string videoId2 = "z1Dd3GO5CGc";
        private string label1 = "Special video of my best 20 mods!";
        private string label2 = "Special video of all my monsters!";

        private void OpenModal(string videoId, string label)
        {
            activeVideoId = videoId;
            activeLabel = label;
            modalOpen = true;
        }

        private void CloseModal()
        {
            modalOpen = false;
            activeVideoId = "";
        }
    }
}
