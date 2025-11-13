// See https://aka.ms/new-console-template for more information
using System.IO;
using VisualSearchPOC;

Console.WriteLine("Hello, World!");



var bedrockClient = Providers.GetBedrockRuntimeClient();

try
{
    // var text = "Two cat sleeping on a red cloth on a bed";
    //var embedding = await BedrockEmbeddings.GetTextEmbeddingAsync(bedrockClient, text);
    //var openSearch = new OpenSearchRepository<ImageDocument>(Providers.GetOpenSearchClient(), "image_vector_poc");
    //var data = await openSearch.GetAllAsync();
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

    //    Console.WriteLine($"Document ID: {item.Id}, Item ID: {item.ListItemId}, Labels: {labelsOutput}");
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
    var result = await ImageRekognition.GetImageLabelsAsync("./Images/cd_2607541.jpeg");
}
catch (Exception ex)
{
    Console.WriteLine("Error getting embedding: " + ex.Message);
}



Console.WriteLine("Press any key to exit...");
Console.Read();