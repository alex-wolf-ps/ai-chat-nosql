using MongoDB.Bson.Serialization.Attributes;
using Newtonsoft.Json;

namespace chatsql.Models
{
    public class Monster : Compendium
    {
        [BsonId]
        [JsonProperty("id")]
        public string? id { get; set; }
        public string? name { get; set; }
        public string? image { get; set; }
        public List<string>? drops { get; set; }
        public bool dlc { get; set; }
        public string? description { get; set; }
        public string? category { get; set; }
        public List<string>? common_locations { get; set; }
    }
}
