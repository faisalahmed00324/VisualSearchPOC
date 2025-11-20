// See https://aka.ms/new-console-template for more information
using System.IO;
using VisualSearchPOC;

Console.WriteLine("Hello, World!");



var bedrockClient = Providers.GetBedrockRuntimeClient();

try
{
    // var text = "Two cat sleeping on a red cloth on a bed";
    //var embedding = await BedrockEmbeddings.GetTextEmbeddingAsync(bedrockClient, text);
   // var textEmbeddion = await BedrockEmbeddings.GetTextEmbeddingAsync(bedrockClient);
    var openSearch = new OpenSearchRepository<ImageDocument>(Providers.GetOpenSearchClient(), "image_vector_poc");
   //var result = openSearch.Search( page: 1, pageSize: 1000);

    // var textEmbeddingService = new ClipTextEmbeddingService();
    //var embedding = textEmbeddingService.Get("A scenic view of mountains during sunrise");
    //var clipImageEmbeddingService = new ClipImageEmbedding();
    //var base64Image = Convert.ToBase64String(File.ReadAllBytes("./Images/image_10.jpg"));
    //var embedding = clipImageEmbeddingService.GetEmbedding(base64Image);
    int a = 1;
    //await openSearch.DeleteIndex();
    //var data = await openSearch.GetAllAsync();
    int i = 1;
    //foreach (var item in data)
    //{
    //    // Build a readable labels string
    //    string labelsOutput;
    //    if (item.Labels == null)
    //    {
    //        labelsOutput = "(none)";
    //    }
    //    else if (item.Labels is IEnumerable<string> labelEnumerable)
    //    {
    //        labelsOutput = string.Join(", ", labelEnumerable);
    //    }
    //    else
    //    {
    //        labelsOutput = item.Labels.ToString();
    //    }

    //    Console.WriteLine($"No: {i} Document ID: {item.Id}, Item ID: {item.ListItemId}, Labels: {labelsOutput}"); i++;
    //}
    //              await openSearch.DeleteIndex();
    //var searchResults =  openSearch.Search(q => q
    //            .Bool(b => b
    //                .Filter(
    //                        f => f.Term(t => t.Field(fd => fd.ListId).Value("6901d174cc7ecfdf1d772987"))
    //                    )
    //                .Must(m => m.Knn(t => t
    //                    .Field(fd => fd.Embedding)
    //                    .Vector(embedding)
    //                    .K(50)
    //                )
    //            )
    //        ), page: 1, pageSize: 50);
    //Console.WriteLine($"Embedding length: {embedding.Length}");
    //Console.WriteLine("First10 values: " + string.Join(", ", embedding.Take(Math.Min(10, embedding.Length))));
    // await VisualSearchOperation.SaveData();
    //var result = await ImageRekognition.GetImageLabelsAsync("./Images/fruits_68525.jpeg");
    var base64Image = Convert.ToBase64String(File.ReadAllBytes("./Images/image_10.jpg"));
    var desc = await ImageDesc.GetImageDescriptionAsync(bedrockClient, base64Image);
}
catch (Exception ex)
{
    Console.WriteLine("Error getting embedding: " + ex.Message);
}



Console.WriteLine("Press any key to exit...");
Console.Read();