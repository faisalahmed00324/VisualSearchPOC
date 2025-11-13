using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Amazon.BedrockRuntime;
using Amazon.BedrockRuntime.Model;
using Amazon.Rekognition;
using Amazon.Rekognition.Model;


namespace VisualSearchPOC
{
    public static class BedrockEmbeddings
    {
        public static async Task<float[]> GetImageEmbeddingAsync(AmazonBedrockRuntimeClient client, string base64Image)
        {
            // Prepare request
            var requestBody = JsonSerializer.Serialize(new
            {
                inputImage = base64Image
            });

            var request = new InvokeModelRequest
            {
                ModelId = "amazon.titan-embed-image-v1",
                ContentType = "application/json",
                Body = new MemoryStream(Encoding.UTF8.GetBytes(requestBody))
            };

            // Invoke model
            var response = await client.InvokeModelAsync(request);
            response.Body.Position = 0;

            string responseString;
            using (var reader = new StreamReader(response.Body))
            {
                responseString = reader.ReadToEndAsync().Result;
            }

            // Parse embeddings
            float[] embeddings;
            using (var doc = System.Text.Json.JsonDocument.Parse(responseString))
            {
                var embeddingArray = doc.RootElement.GetProperty("embedding");

                embeddings = new float[embeddingArray.GetArrayLength()];
                int i = 0;
                foreach (var val in embeddingArray.EnumerateArray())
                {
                    embeddings[i++] = val.GetSingle();
                }
            }
            double sumSq = 0.0;
            for (int i = 0; i < embeddings.Length; i++)
            {
                sumSq += (double)embeddings[i] * embeddings[i];
            }
            var norm = Math.Sqrt(sumSq);
            if (norm > 0.0)
            {
                for (int i = 0; i < embeddings.Length; i++)
                    embeddings[i] = (float)(embeddings[i] / norm);
            }

            return embeddings;
        }
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

   

    public static class HuggingFaceGemma
    {
        public static async Task<string> GetResultAsync(
            AmazonBedrockRuntimeClient client,
            string text, string imageBase64)
        {
            var requestBody = new
            {
                anthropic_version = "bedrock-2023-05-31",
                messages = new[]
             {
                new {
                    role = "user",
                    content = new object[] {
                        new {
                            type = "text",
                            text = text
                        },
                        new {
                            type = "image",
                            source = new {
                                type = "base64",
                                media_type = "image/jpeg",
                                data = imageBase64
                            }
                        }
                    }
                }
            },
                max_tokens = 150
            };


            var bodyJson = JsonSerializer.Serialize(requestBody);
            var request = new InvokeModelRequest
            {
                ModelId = "arn:aws:bedrock:us-west-2:591165668488:inference-profile/global.anthropic.claude-sonnet-4-20250514-v1:0",
                Body = new MemoryStream(Encoding.UTF8.GetBytes(bodyJson)),
                ContentType = "application/json",
                Accept = "application/json"
            };

            var response = await client.InvokeModelAsync(request);

            using var reader = new StreamReader(response.Body);
            var output = await reader.ReadToEndAsync();

            Console.WriteLine(output);
            return output;
        }
    }
    public static class ImageRekognition
    {
        public static async Task<List<Label>> GetImageLabelsAsync(string imagePath)
        {
            var rekognitionClient = new AmazonRekognitionClient(Amazon.RegionEndpoint.USEast1);

            // Path to your image
            
            List<Label> labels = new List<Label>();
            // Read image bytes
            byte[] imageBytes = File.ReadAllBytes(imagePath);

            var detectLabelsRequest = new DetectLabelsRequest
            {
                Image = new Image
                {
                    Bytes = new MemoryStream(imageBytes)
                },
                MaxLabels = 10,
                MinConfidence = 90F
            };

            try
            {
                var detectLabelsResponse = await rekognitionClient.DetectLabelsAsync(detectLabelsRequest);
                labels = detectLabelsResponse.Labels;
                Console.WriteLine("Detected labels in the image:");
                foreach (var label in detectLabelsResponse.Labels)
                {
                    Console.WriteLine($"{label.Name} - Confidence: {label.Confidence}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error: {ex.Message}");
            }
            return labels;
        }

    }
        
}
