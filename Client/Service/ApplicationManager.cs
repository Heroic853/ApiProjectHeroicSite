namespace Client.Service
{
    public class ApplicationManager
    {
        public string Username { get; set; }

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