namespace chatsql.Models
{
    public class OpenAISettings
    {
        public string OpenAIEndpoint { get; set; } = null!;
        public string OpenAIEmbeddingDeployment { get; set; } = null!;
        public string OpenAICompletionsDeployment { get; set; } = null!;
        public string OpenAIKey { get; set; } = null!;
    }
}
