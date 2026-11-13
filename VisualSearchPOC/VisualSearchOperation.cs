namespace VisualSearchPOC
{
    public static class VisualSearchOperation
    {
        public static async Task SaveData(string imagePath, string tenantId, string listId)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(imagePath);
            ArgumentException.ThrowIfNullOrWhiteSpace(tenantId);
            ArgumentException.ThrowIfNullOrWhiteSpace(listId);

            var indexName = Environment.GetEnvironmentVariable("OPENSEARCH_INDEX") ?? "visual-search-demo";
            var openSearch = new OpenSearchRepository<ImageDocument>(Providers.GetOpenSearchClient(), indexName);
            using var bedrockClient = Providers.GetBedrockRuntimeClient();
            var imageBytes = await File.ReadAllBytesAsync(imagePath);
            var base64Image = Convert.ToBase64String(imageBytes);
            var imageVector = await BedrockEmbeddings.GetImageEmbeddingAsync(bedrockClient, base64Image);
            var labels = await ImageRekognition.GetImageLabelsAsync(imagePath);
            var imageDocument = new ImageDocument
            {
                Id = Guid.NewGuid().ToString(),
                ListId = listId,
                ListItemId = Guid.NewGuid().ToString(),
                Embedding = imageVector,
                FileId = Guid.NewGuid().ToString(),
                TenantId = tenantId,
                IsActive = true,
                IsDefault = true,
                Labels = labels.Select(label => label.Name).Distinct().ToList(),
                Categories = labels
                    .SelectMany(label => label.Categories ?? [])
                    .Select(category => category.Name).Distinct().ToList(),
                Parents = labels
                    .SelectMany(label => label.Parents ?? [])
                    .Select(parent => parent.Name).Distinct().ToList()
            };

            openSearch.EnsureIndex();
            await openSearch.IndexAsync(imageDocument, imageDocument.Id);
            Console.WriteLine($"Indexed document {imageDocument.Id} in {indexName}.");
        }
    }
}
