using Azure;
using Azure.AI.OpenAI;
using chatsql.Models;
using Microsoft.Extensions.Options;
using MongoDB.Bson;
using MongoDB.Bson.IO;
using MongoDB.Bson.Serialization;
using MongoDB.Driver;
using Newtonsoft.Json;

namespace chatsql.Services
{
    public class MongoDBService
    {
        private readonly IMongoDatabase _database;
        private readonly IMongoCollection<Monster> _monsterCollection;
        private OpenAIClient openAIClient;
        private readonly int maxVectorSearchResults = 150;

        public MongoDBService(IOptions<MongoDBSettings> mongoDBSettings, OpenAIClient aiClient)
        {
            MongoClient client = new MongoClient(mongoDBSettings.Value.MongoVcoreConnection);
            _database = client.GetDatabase(mongoDBSettings.Value.MongoVcoreDatabase);
            _monsterCollection = _database.GetCollection<Monster>(mongoDBSettings.Value.MongoVcoreCollection);
            openAIClient = aiClient;
        }

        public async Task CreateAsync(Monster item)
        {
            // Generate embedding for item
            List<string> serializedMonsters = new() { Newtonsoft.Json.JsonConvert.SerializeObject(item) };
            EmbeddingsOptions options = new EmbeddingsOptions("wolfembedding", serializedMonsters);

            var response = await openAIClient.GetEmbeddingsAsync(options);

            // Add the generated embedding as a property on the original item
            Embeddings embeddings = response.Value;
            float[] embedding = embeddings.Data[0].Embedding.ToArray();
            item.embedding = embedding.ToList();

            // Save item and embedding (vectorized item) to database
            await _monsterCollection.InsertOneAsync((Monster)item);
        }

        public async Task<List<Monster>> Search(float[] embedding)
        {
            // Create Mongo pipeline search query
            BsonDocument[] pipeline = new BsonDocument[]
               {
                    BsonDocument.Parse($"{{$search: " +
                        $"{{cosmosSearch: " +
                            $"{{ vector: [{string.Join(',', embedding)}]," +
                                $"path: 'embedding'," +
                                $"k: { 10 }," +
                                $"efSearch: { 200 }}}," +
                                $"returnStoredSource:true}}}}"),
                    BsonDocument.Parse($"{{$project: {{embedding: 0}}}}"),
               };

            // Run search
            var bsonDocuments = await _monsterCollection.Aggregate<BsonDocument>(pipeline).ToListAsync();
            var relevantMonsters = bsonDocuments.ToList().ConvertAll(bsonDocument => BsonSerializer.Deserialize<Monster>(bsonDocument));

            // Clean up unnecessary data to send to completions AI models
            foreach (var monster in relevantMonsters)
            { 
                monster.embedding = null;
                monster.image = null;
            }

            return relevantMonsters;
        }

        public void CreateVectorIndexIfNotExists(string vectorIndexName)
        {
            try
            {
                // Find if vector index exists in vectors collection
                using (IAsyncCursor<BsonDocument> indexCursor = _monsterCollection.Indexes.List())
                {
                    bool vectorIndexExists = indexCursor.ToList().Any(x => x["name"] == vectorIndexName);
                    if (!vectorIndexExists)
                    {
                        BsonDocumentCommand<BsonDocument> command = new BsonDocumentCommand<BsonDocument>(
                        BsonDocument.Parse(@"
                            { createIndexes: 'monsters', 
                              indexes: [{ 
                                name: 'vectorSearchIndex', 
                                key: { embedding: 'cosmosSearch' }, 
                                cosmosSearchOptions: { 
                                    kind: 'vector-hnsw',
                                    m: 32,
                                    efConstruction: 128,
                                    similarity: 'COS',
                                    dimensions: 1536 } 
                              }] 
                            }"));

                        BsonDocument result = _database.RunCommand(command);
                        if (result["ok"] != 1)
                        {
                            Console.WriteLine("CreateIndex failed with response: " + result.ToJson());
                        }
                    }
                }

            }
            catch (MongoException ex)
            {
                Console.WriteLine("MongoDbService InitializeVectorIndex: " + ex.Message);
                throw;
            }
        }
    }
}
