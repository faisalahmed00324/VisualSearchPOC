using OpenSearch.Client;
using OpenSearch.Net;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace VisualSearchPOC
{
    public interface IOpenSearchUnitOfWork<T> where T : class
    {
        void EnqueueIndex(string id, T document);
        void EnqueueUpdate(string id, T document);
        void EnqueueUpdateByQuery(Func<QueryContainerDescriptor<T>, QueryContainer> queryBuilder,
            Func<ScriptDescriptor, IScript> script);
        void EnqueueDelete(string id);
        void EnqueueSoftDelete(string id, Action<T> applyChange);
        void Commit();
        void Rollback();
    }

    public class OpenSearchUnitOfWork<T> : IOpenSearchUnitOfWork<T> where T : class
    {
        private readonly IOpenSearchClient _client;
        private readonly string _indexName;
        private readonly List<Action<BulkDescriptor>> _operations = new List<Action<BulkDescriptor>>();
        private readonly List<Action> _scriptOperations = new List<Action>();
        private bool _committedOrRolledBack;

        public OpenSearchUnitOfWork(IOpenSearchClient client, string indexName)
        {
            _client = client;
            _indexName = indexName ?? throw new ArgumentNullException(nameof(indexName));
        }

        public void EnqueueIndex(string id, T document)
        {
            if (document == null) throw new ArgumentNullException(nameof(document));

            _operations.Add(b => b.Index<T>(op => op
                .Index(_indexName)
                .Id(id)
                .Document(document)
            ));
        }

        public void EnqueueUpdate(string id, T document)
        {
            if (document == null) throw new ArgumentNullException(nameof(document));

            _operations.Add(b => b.Update<T>(op => op
                .Index(_indexName)
                .Id(id)
                .Doc(document)
                .DocAsUpsert(true)
            ));
        }
        public void EnqueueUpdateByQuery(Func<QueryContainerDescriptor<T>, QueryContainer> queryBuilder,
            Func<ScriptDescriptor, IScript> script)
        {
            if (queryBuilder == null)
                throw new ArgumentNullException(nameof(queryBuilder));
            if (script == null)
                throw new ArgumentNullException(nameof(script));

            _scriptOperations.Add(() =>
            {
                var response = _client.UpdateByQuery<T>(u => u
                    .Index(_indexName)
                    .Query(queryBuilder)
                    .Script(script)
                    .Refresh(true)
                );

                if (!response.IsValid)
                {
                    throw new Exception(
                        $"UpdateByQuery failed: {response.ServerError?.Error?.Reason ?? response.OriginalException?.Message}",
                        response.OriginalException
                    );
                }
            });
        }

        public void EnqueueDelete(string id)
        {
            _operations.Add(b => b.Delete<T>(op => op
                .Index(_indexName)
                .Id(id)
            ));
        }

        public void EnqueueSoftDelete(string id, Action<T> applyChange)
        {
            // Create a partial document of type T and apply the change
            var partialDoc = Activator.CreateInstance<T>();
            applyChange?.Invoke(partialDoc);

            _operations.Add(b => b.Update<T>(op => op
                .Index(_indexName)
                .Id(id)
                .Doc(partialDoc)
            ));
        }

        public void Commit()
        {
            if (_committedOrRolledBack) return;
            if (_operations.Count == 0 && _scriptOperations.Count == 0)
            {
                _committedOrRolledBack = true;
                return;
            }
            if (_operations.Count > 0)
            {
                var bulk = new BulkDescriptor().Refresh(Refresh.True);

                foreach (var op in _operations)
                    op(bulk);

                var response = _client.BulkAsync(bulk).Result;
                if (!response.IsValid)
                {
                    // consider logging response.ServerError/ItemsWithErrors for diagnosis
                    throw new Exception($"Bulk commit failed: {response.OriginalException?.Message ?? response.ServerError?.ToString()}",
                        response.OriginalException);
                }
                if (response.Errors)
                {
                    var errors = string.Join("; ", response.ItemsWithErrors.Select(i => $"id:{i.Id} err:{i.Error?.Reason}"));
                    throw new Exception("Bulk commit completed with item errors: " + errors);
                }
            }

            if (_scriptOperations.Count > 0)
            {
                foreach (var op in _scriptOperations)
                    op();

            }


            _operations.Clear();
            _committedOrRolledBack = true;
        }

        public void Rollback()
        {
            _operations.Clear();
            _committedOrRolledBack = true;
        }

        public void Dispose()
        {
            if (!_committedOrRolledBack)
            {
                Rollback();
            }
        }
    }
}
