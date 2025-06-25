namespace ElAgentApi.Bot.Models
{
    public class Citation(string id, string title, string url)
    {
        public string Id { get; set; } = id;
        public string Title { get; set; } = title;
        public string Url { get; set; } = url;
    }   
}
