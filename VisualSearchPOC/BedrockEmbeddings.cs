using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Amazon.BedrockRuntime;
using Amazon.BedrockRuntime.Model;

namespace VisualSearchPOC
{
    public static class BedrockEmbeddings
    {
        /// <summary>
        /// Retrieves text embeddings from Amazon Bedrock using the Titan embedding model.
        /// </summary>
        /// <param name="client">The Bedrock Runtime client.</param>
        /// <param name="text">The text to generate embeddings for.</param>
        /// <param name="modelId">The Bedrock model ID (default: amazon.titan-embedding).</param>
        /// <returns>An array of embedding values.</returns>
        /// <exception cref="ArgumentNullException">Thrown when client or text is null.</exception>
        /// <exception cref="Exception">Thrown when unable to parse the embedding from the response.</exception>
        public static async Task<float[]> GetTextEmbeddingAsync(
            AmazonBedrockRuntimeClient client,
            string text,
            string modelId = "amazon.titan-embed-text-v2:0")
        {
            if (client == null)
                throw new ArgumentNullException(nameof(client));    
            if (text == null)
                throw new ArgumentNullException(nameof(text));

            var payload = JsonSerializer.Serialize(new { inputText = text });

            var request = new InvokeModelRequest
            {
                ModelId = modelId,
                ContentType = "application/json",
                Accept = "application/json",
                Body = new MemoryStream(Encoding.UTF8.GetBytes(payload))
            };

            var response = await client.InvokeModelAsync(request);

            using var sr = new StreamReader(response.Body, Encoding.UTF8);
            var body = await sr.ReadToEndAsync().ConfigureAwait(false);

            using var doc = JsonDocument.Parse(body);
            var root = doc.RootElement;

            // Try common response shape: { "embeddings": [ { "embedding": [...] } ] }
            if (TryExtractEmbeddingFromProperty(root, "embeddings", out var embedding))
                return embedding;

            // Try results array: { "results": [ { "embedding": [...] } ] }
            if (TryExtractEmbeddingFromProperty(root, "results", out embedding))
                return embedding;

            // Fallback: recursive search for first numeric array
            var found = FindFirstNumericArray(root);
            if (found != null)
                return found;

            throw new Exception($"Unable to parse embedding from Bedrock response: {body}");
        }

        /// <summary>
        /// Attempts to extract embedding from a specific property in the JSON response.
        /// </summary>
        private static bool TryExtractEmbeddingFromProperty(
            JsonElement root,
            string propertyName,
            out float[] embedding)
        {
            embedding = null;

            if (!root.TryGetProperty(propertyName, out var property) ||
                property.ValueKind != JsonValueKind.Array ||
                property.GetArrayLength() == 0)
            {
                return false;
            }

            var firstElement = property[0];

            // Try to find "embedding" property within the first element
            if (firstElement.TryGetProperty("embedding", out var embeddingProp) &&
                embeddingProp.ValueKind == JsonValueKind.Array)
            {
                embedding = embeddingProp.EnumerateArray().Select(e => e.GetSingle()).ToArray();
                return true;
            }

            // If first element is directly an array, use it
            if (firstElement.ValueKind == JsonValueKind.Array)
            {
                embedding = firstElement.EnumerateArray().Select(e => e.GetSingle()).ToArray();
                return true;
            }

            return false;
        }

        /// <summary>
        /// Recursively searches through a JSON element to find the first numeric array (embedding).
        /// </summary>
        private static float[] FindFirstNumericArray(JsonElement element)
        {
            return element.ValueKind switch
            {
                JsonValueKind.Array => SearchArray(element),
                JsonValueKind.Object => SearchObject(element),
                _ => null
            };
        }

        /// <summary>
        /// Searches within a JSON array for a numeric array.
        /// </summary>
        private static float[] SearchArray(JsonElement element)
        {
            var arrayElements = element.EnumerateArray().ToArray();

            // If this is an array of numbers, return it as the embedding
            if (arrayElements.Length > 0 && arrayElements.All(e => e.ValueKind == JsonValueKind.Number))
            {
                return arrayElements.Select(e => e.GetSingle()).ToArray();
            }

            // Otherwise, recursively search child elements
            foreach (var child in arrayElements)
            {
                var result = FindFirstNumericArray(child);
                if (result != null)
                    return result;
            }

            return null;
        }

        /// <summary>
        /// Searches within a JSON object for a numeric array.
        /// </summary>
        private static float[] SearchObject(JsonElement element)
        {
            foreach (var property in element.EnumerateObject())
            {
                var result = FindFirstNumericArray(property.Value);
                if (result != null)
                    return result;
            }

            return null;
        }
    }
}
