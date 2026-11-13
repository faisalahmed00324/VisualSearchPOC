using Amazon.BedrockRuntime;
using Amazon.Runtime;
using Amazon.Runtime.Credentials;
using OpenSearch.Client;
using OpenSearch.Net;
using OpenSearch.Net.Auth.AwsSigV4;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace VisualSearchPOC
{
    public static class Providers
    {
        public static Amazon.RegionEndpoint GetRegion()
        {
            var region = Environment.GetEnvironmentVariable("AWS_REGION");
            return Amazon.RegionEndpoint.GetBySystemName(
                string.IsNullOrWhiteSpace(region) ? "us-west-2" : region);
        }

        public static  OpenSearchClient GetOpenSearchClient()
        {
            var endpoint = Environment.GetEnvironmentVariable("OPENSEARCH_ENDPOINT");
            if (!Uri.TryCreate(endpoint, UriKind.Absolute, out var uri) ||
                uri.Scheme != Uri.UriSchemeHttps ||
                !string.IsNullOrEmpty(uri.UserInfo))
            {
                throw new InvalidOperationException(
                    "Set OPENSEARCH_ENDPOINT to your HTTPS OpenSearch endpoint without embedded credentials.");
            }

            var credentials = DefaultAWSCredentialsIdentityResolver.GetCredentials();
            var connection = new AwsSigV4HttpConnection(credentials, GetRegion());
            var pool = new SingleNodeConnectionPool(uri);
            var settings = new ConnectionSettings(pool, connection);

            var client = new OpenSearchClient(settings);
            return client;
        }
        public static AmazonBedrockRuntimeClient GetBedrockRuntimeClient()
        {
            
            var config = new AmazonBedrockRuntimeConfig
            {
                RegionEndpoint = GetRegion()
            };
            var client = new AmazonBedrockRuntimeClient( config);
            return client;
        }
    }
    public interface IOpenSearchRepository<T> where T : class
    {
        void EnsureIndex();
        Task IndexAsync(T document, string id);
        Task DeleteIndex();
        Task<T> GetAsync(string id);
        Task<IList<T>> GetAllAsync();
        Task DeleteAsync(string id);
        Task UpdateAsync(string id, T document);
        void UpdateByQuery(Func<QueryContainerDescriptor<T>, QueryContainer> query, Func<ScriptDescriptor, IScript> script);
        Task<ISearchResponse<T>> SearchByVectorAsync(
            float[] vector,
            int pageNumber,
            int pageSize,
            Dictionary<string, object> filters = null);
        ISearchResponse<T> Search(
                        int page = 1,
                        int pageSize = 10,
                        Dictionary<string, object> filters = null);
        ISearchResponse<T> Search(
               Func<QueryContainerDescriptor<T>, QueryContainer> query,
               int page = 1,
               int pageSize = 10
               );
        void Commit();
    }

    public class OpenSearchRepository<T> : IOpenSearchRepository<T> where T : ImageDocument
    {
        private readonly IOpenSearchClient _client;
        private readonly string _indexName;
        private readonly string _vectorFieldName;
        private readonly IOpenSearchUnitOfWork<T> _unitOfWork;

        public OpenSearchRepository(IOpenSearchClient client, string indexName, string vectorFieldName = "embedding")
        {
            _client = client;
            _indexName = indexName;
            _vectorFieldName = vectorFieldName;
            _unitOfWork = new OpenSearchUnitOfWork<T>(client, indexName);
        }

        public void EnsureIndex()
        {
            var exists = _client.Indices.ExistsAsync(_indexName).Result;
            if (!exists.Exists)
            {
                var response = _client.Indices.CreateAsync(_indexName, c => c
                    .Settings(s => s
                        .Setting("index.knn", true)
                    )
                    .Map<T>(m => m
                        .AutoMap()
                        .Properties(ps => ps
                            .KnnVector(kv => kv
                                .Name(n => n.Embedding)
                                .Dimension(1024)
                                .Method(me => me
                                    .Name("hnsw")
                                    .Engine("faiss")
                                    .SpaceType("cosinesimil")
                                )
                            )
                        )
                    )
                ).Result;
                if (!response.IsValid)
                    throw new Exception($"Failed to create index {_indexName}: {response.OriginalException?.Message}", response.OriginalException);
            }
        }

        public async Task IndexAsync(T document, string id)
        {
            var response = await _client.IndexAsync(document, i => i.Index(_indexName).Id(id));
            if (!response.IsValid)
                throw new Exception($"Index failed: {response.OriginalException?.Message}", response.OriginalException);
        }

        public void Commit()
        {
            _unitOfWork.Commit();
        }



        public async Task<IList<T>> GetAllAsync()
        {
            var response = await _client.SearchAsync<T>(s => s
                .Index(_indexName)
                .Query(q => q.MatchAll())
                .Size(1000) // Adjust size as needed
            );
            if (!response.IsValid)
                throw new Exception($"Search failed: {response.OriginalException?.Message}", response.OriginalException);
            return response.Documents.ToList();
        }
        public async Task<T> GetAsync(string id)
        {
            var response = await _client.GetAsync<T>(id, g => g.Index(_indexName));
            if (!response.Found)
                return null;
            return response.Source;
        }

        public async Task DeleteAsync(string id)
        {
            var response = await _client.DeleteAsync<T>(id, d => d.Index(_indexName));
            if (!response.IsValid)
                throw new Exception($"Delete failed: {response.OriginalException?.Message}", response.OriginalException);
        }

        public async Task UpdateAsync(string id, T document)
        {
            var response = await _client.UpdateAsync<T>(id, u => u
                .Index(_indexName)
                .Doc(document)
                .DocAsUpsert(true)); // if not exists, insert
            if (!response.IsValid)
                throw new Exception($"Update failed: {response.OriginalException?.Message}", response.OriginalException);
        }

        public async Task<ISearchResponse<T>> SearchByVectorAsync(
            float[] vector,
            int pageNumber,
            int pageSize,
            Dictionary<string, object> filters = null)
        {
            if (pageNumber <= 0) throw new ArgumentException("PageNumber must be >= 1");
            if (pageSize <= 0) throw new ArgumentException("PageSize must be >= 1");

            var from = (pageNumber - 1) * pageSize;

            var response = await _client.SearchAsync<T>(s => s
                .Index(_indexName)
                .From(from)
                .Size(pageSize)
                .Query(q =>
                    q.ScriptScore(ss => ss
                        .Query(innerQ => BuildFilterQuery(innerQ, filters))
                        .Script(script => script
                            .Source($"cosineSimilarity(params.query_vector, '{_vectorFieldName}') + 1.0")
                            .Params(p => p.Add("query_vector", vector))
                        )
                    )
                )
            );

            if (!response.IsValid)
                throw new Exception($"Search failed: {response.OriginalException?.Message}", response.OriginalException);

            return response;
        }
        public void UpdateByQuery(Func<QueryContainerDescriptor<T>, QueryContainer> query, Func<ScriptDescriptor, IScript> script)
        {
            if (_unitOfWork != null)
            {
                _unitOfWork.EnqueueUpdateByQuery(query, script);
                return;
            }

            var response = _client.UpdateByQuery<T>(u => u
                .Index(_indexName)
                .Query(query)
                .Script(script)
                .Refresh(true)
            );
            if (!response.IsValid)
                throw new Exception($"Search failed: {response.OriginalException?.Message}", response.OriginalException);
        }
        public ISearchResponse<T> Search(
                int page = 1,
                int pageSize = 10,
                Dictionary<string, object> filters = null)
        {
            int from = (page - 1) * pageSize;

            var response = _client.SearchAsync<T>(s => s
                .Index(_indexName)
                .From(from)
                .Size(pageSize)
                .Query(q => BuildFilterQuery(q, filters))
            ).Result;

            if (!response.IsValid)
                throw new Exception($"Search failed: {response.OriginalException?.Message}", response.OriginalException);

            return response;
        }
        public ISearchResponse<T> Search(
               Func<QueryContainerDescriptor<T>, QueryContainer> query,
               int page = 1,
               int pageSize = 10
               )
        {
            int from = (page - 1) * pageSize;

            var response = _client.SearchAsync<T>(s => s
                .Index(_indexName)
                .From(from)
                .Size(pageSize)
                .Query(query)
            ).Result;


            if (!response.IsValid)
                throw new Exception($"Search failed: {response.OriginalException?.Message}", response.OriginalException);

            return response;
        }

        private QueryContainer BuildFilterQuery(QueryContainerDescriptor<T> q, Dictionary<string, object> filters)
        {
            if (filters == null || filters.Count == 0)
                return q.MatchAll();

            QueryContainer query = null;

            foreach (var filter in filters)
            {
                QueryContainer clause = null;

                switch (filter.Value)
                {
                    case string strVal:
                        clause = q.Term(t => t.Field(filter.Key).Value(strVal));
                        break;

                    case bool boolVal:
                        clause = q.Term(t => t.Field(filter.Key).Value(boolVal));
                        break;

                    case int intVal:
                        clause = q.Term(t => t.Field(filter.Key).Value(intVal));
                        break;

                    case IEnumerable<string> strList:
                        clause = q.Terms(t => t.Field(filter.Key).Terms(strList));
                        break;

                    case Tuple<DateTime, DateTime> dateRange:
                        clause = q.DateRange(r => r
                            .Field(filter.Key)
                            .GreaterThanOrEquals(dateRange.Item1)
                            .LessThanOrEquals(dateRange.Item2));
                        break;

                    default:
                        throw new NotSupportedException($"Unsupported filter type: {filter.Value.GetType()}");
                }

                query &= clause; // AND filters together
            }

            return query;
        }

        public async Task DeleteIndex()
        {
            var deleteResponse = await _client.Indices.DeleteAsync(_indexName);
        }
    }


    public class ImageDocument
    {
        public string Id { get; set; }
        public string ListItemId { get; set; }
        public string ListId { get; set; }
        public string FileId { get; set; }
        public FolderDetail FolderDetail { get; set; }

         
        public float[] TextEmbedding { get; set; }
        public string TenantId { get; set; }
        public bool IsDefault { get; set; }
        public bool IsActive { get; set; }
        public float[] Embedding { get; set; }
        public List<string> Categories { get; set; }
        public List<string> Labels { get; set; }
        public List<string> Parents { get; set; }
    }
    public class FolderDetail
    {
        public string FolderId { get; set; }
        public List<string> FolderPathIds { get; set; }

    }
}
