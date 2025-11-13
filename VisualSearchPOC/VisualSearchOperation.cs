using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace VisualSearchPOC
{
    public static class VisualSearchOperation
    {
        public const string ListId = "7a9c0110-e60c-4209-b68c-c2373d3224a0";
        public static async Task SaveData()
        {
            try
            {
                var bedrockClient = Providers.GetBedrockRuntimeClient();
                var path = "./Images/cd_2607541.jpeg";
                var imageBytes = File.ReadAllBytes(path);
                var base64Image = Convert.ToBase64String(imageBytes);
                var imageVector = await BedrockEmbeddings.GetImageEmbeddingAsync(bedrockClient, base64Image);
                //var result = await HuggingFaceGemma.GetResultAsync(bedrockClient, "Describe the image. What objects are visible, What is happening and what are the main objects in total 3 lines", base64Image);
                var result = await ImageRekognition.GetImageLabelsAsync(path);
                var imageDocument = new ImageDocument
                {
                    Id = Guid.NewGuid().ToString(),
                    ListId = "7a9c0110-e60c-4209-b68c-c2373d3224a0",
                    ListItemId = Guid.NewGuid().ToString(),
                    Embedding = imageVector,
                    FileId = Guid.NewGuid().ToString(),                    
                    TenantId = "tenant_123",
                    IsActive = true,
                    IsDefault = true,
                };
                List<string> labels = new List<string>();
                List<string> categoris = new List<string>();
                List<string> parents = new List<string>();
                foreach(var label in result)
                {
                    labels.Add(label.Name);
                    if(label.Categories != null && label.Categories.Count>0)
                        categoris.AddRange(label.Categories.Select(x => x.Name));
                    if(label.Parents != null && label.Parents.Count>0)
                        parents.AddRange(label.Parents.Select(x => x.Name));
                    Console.WriteLine($"Label: {label.Name}, Confidence: {label.Confidence}");
                }
                imageDocument.Labels = labels.Distinct().ToList();
                imageDocument.Categories = categoris.Distinct().ToList();
                imageDocument.Parents = parents.Distinct().ToList();
                var openSearch = new OpenSearchRepository<ImageDocument>(Providers.GetOpenSearchClient(), "image_vector_poc");
                await openSearch.IndexAsync(imageDocument, imageDocument.Id);

            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error: {ex.Message}");
            }
            
        }
    }
}
