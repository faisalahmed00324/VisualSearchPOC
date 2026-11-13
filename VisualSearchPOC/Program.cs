using VisualSearchPOC;

if (args.Length == 0 || (args.Length == 1 && args[0] == "--help"))
{
    Console.WriteLine("Visual Search POC - Amazon Bedrock, Rekognition and OpenSearch");
    Console.WriteLine("Describe an image: dotnet run --project VisualSearchPOC -- describe <image-path>");
    Console.WriteLine("Index an image:    dotnet run --project VisualSearchPOC -- index <image-path> <tenant-id> <list-id>");
    Console.WriteLine("See README.md for AWS credentials, configuration and service costs.");
    return 0;
}

try
{
    if (args[0] == "describe" && args.Length == 2)
    {
        var imageFormat = Path.GetExtension(args[1]).ToLowerInvariant() switch
        {
            ".jpg" or ".jpeg" => "jpeg",
            ".png" => "png",
            ".gif" => "gif",
            ".webp" => "webp",
            _ => throw new ArgumentException("Use a JPEG, PNG, GIF or WebP image.")
        };
        var base64Image = Convert.ToBase64String(await File.ReadAllBytesAsync(args[1]));
        using var bedrockClient = Providers.GetBedrockRuntimeClient();
        Console.WriteLine(await ImageDesc.GetImageDescriptionAsync(bedrockClient, base64Image, imageFormat));
    }
    else if (args[0] == "index" && args.Length == 4)
    {
        await VisualSearchOperation.SaveData(args[1], args[2], args[3]);
    }
    else
    {
        throw new ArgumentException("Invalid arguments. Run with --help for usage.");
    }

    return 0;
}
catch (Exception ex)
{
    Console.Error.WriteLine($"Visual search failed: {ex.Message}");
    return 1;
}
