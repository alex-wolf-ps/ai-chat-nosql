namespace chatsql.Models
{
    public class MongoDBSettings
    {
        public string MongoVcoreConnection { get; set; } = null!;
        public string MongoVcoreDatabase { get; set; } = null!;
        public string MongoVcoreCollection { get; set; } = null!;
    }
}
