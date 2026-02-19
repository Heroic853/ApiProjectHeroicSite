namespace Client.Service
{
    public class ApplicationManager
    {
        // Le config le gestisce tutto Auth0 percio niente Token

        public string Username { get; set; }
        public string Account { get; set; }

        public bool IsLoggedIn()
        {
            return Username != null;
        }

        public bool IsAdmin()
        {
            return Username != null && Username.Equals("Admin");
        }
    }
}