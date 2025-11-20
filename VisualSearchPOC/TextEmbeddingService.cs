using Amazon.Runtime.Internal.Endpoints.StandardLibrary;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Intrinsics.Arm;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace VisualSearchPOC
{
    public class TextEmbeddingResult
    {
        public float[] embedding { get; set; }
    }
    public interface ITextEmbeddingService
    {
        float[] Get(string text);
    }

    public class ClipTextEmbeddingService : ITextEmbeddingService
    {
        public float[] Get(string text)
        {
            var payload = $"{{\"text\":\"{text}\"}}";
            var apiBase = "http://localhost:8081";
            var url = $"{apiBase.TrimEnd('/')}/text-embedding";
            using (var client = new HttpClient())
            {
                var content = new StringContent(payload, Encoding.UTF8, "application/json");
                HttpResponseMessage resp;
                try
                {
                    resp = client.PostAsync(url, content).Result;
                }
                catch (Exception ex)
                {
                    // Surface network/transport errors as a descriptive exception.
                    throw new InvalidOperationException($"Failed to call image description service at '{url}': {ex.Message}", ex);
                }
                var respBody = resp.Content.ReadAsStringAsync().Result;
                if (!resp.IsSuccessStatusCode)
                {
                    // Return body (or throw) — choose conservative behavior: throw so caller knows it failed.
                    throw new InvalidOperationException($"Image description API returned {(int)resp.StatusCode}: {respBody}");
                }
                var result = System.Text.Json.JsonSerializer.Deserialize<TextEmbeddingResult>(respBody);
                return result.embedding;
            }
        }
    }
    class ClipImageEmbeddingResult
    {
        public float[] embedding { get; set; }
    }
    public class ClipImageEmbedding
    {
        public float[] GetEmbedding(string base64Image)
        {
            var payload = $"{{\"image\":\"{base64Image}\"}}";
            var apiBase = "http://localhost:8080";
            var url = $"{apiBase.TrimEnd('/')}/invocations";
            using (var client = new HttpClient())
            {
                var content = new StringContent(payload, Encoding.UTF8, "application/json");
                HttpResponseMessage resp;
                try
                {
                    resp = client.PostAsync(url, content).Result;
                }
                catch (Exception ex)
                {
                    // Surface network/transport errors as a descriptive exception.
                    throw new InvalidOperationException($"Failed to call image description service at '{url}': {ex.Message}", ex);
                }
                var respBody = resp.Content.ReadAsStringAsync().Result;
                if (!resp.IsSuccessStatusCode)
                {
                    // Return body (or throw) — choose conservative behavior: throw so caller knows it failed.
                    throw new InvalidOperationException($"Image description API returned {(int)resp.StatusCode}: {respBody}");
                }
                var result = System.Text.Json.JsonSerializer.Deserialize<ClipImageEmbeddingResult>(respBody);
                return result.embedding;
            }
        }
    }

}
