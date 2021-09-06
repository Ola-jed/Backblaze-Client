# Backblaze-Client
Api client for [Backblaze](https://www.backblaze.com/) written in C#

## Usage

```c#
private static async Task Main(string[] args)
{
    var config = new BackblazeConfig()
    {
        KeyId = "yourkeyid",
        AppKey = "yourappkey",
        ApiBase = "https://api.backblazeb2.com/b2api/v2/",
        BucketId = "yourbucketid"
    };

    const string filePath = "image.jpg";
    var bytes = await File.ReadAllBytesAsync(filePath);

    var client = new BackblazeClient(config,new Logger(LogLevel.High));
    await client.AuthorizeAccount();

    var id = await client.Upload(DateTime.Now.Ticks.ToString()+".jpg", bytes);
    var stream = await client.Download(id);
    
    const string path = "download.jpeg";
    await using var outputFileStream = new FileStream(path, FileMode.CreateNew);
    await stream.CopyToAsync(outputFileStream);

    var fileName = await client.GetFileName(id);
    await client.Delete(id, fileName);
}
```

## Download
[Download here](https://www.nuget.org/api/v2/package/Backblaze_Client/1.0.0)