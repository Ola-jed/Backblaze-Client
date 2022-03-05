using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Serilog;

namespace Backblaze_Client;

/// <summary>
/// BackblazeClient class
/// Used to handle requests with the Backblaze api
/// </summary>
public class BackblazeClient
{
    private readonly BackblazeConfig _config;
    private readonly HttpClient _httpClient = new();
    private readonly ILogger _logger;
    private string? _apiUrl;
    private string? _downloadUrl;
    private string? _accountAuthorizationToken;

    public BackblazeClient(BackblazeConfig config, ILogger logger)
    {
        _config = config;
        _logger = logger.ForContext(GetType());
        _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
    }

    /// <summary>
    /// Authorize the user account with the given credentials
    /// </summary>
    public async Task AuthorizeAccount()
    {
        var request = new HttpRequestMessage(HttpMethod.Get, $"{_config.ApiBase}b2_authorize_account");
        var authorization = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{_config.KeyId}:{_config.AppKey}"));
        request.Headers.Authorization = new AuthenticationHeaderValue("Basic",$"{authorization}");
        var responseMessage = await _httpClient.SendAsync(request);
        var content = await responseMessage.Content.ReadAsStringAsync();
        _logger.Debug("Account authorization response: {Response}", content);
        var doc = JsonDocument.Parse(content).RootElement;
        _accountAuthorizationToken = doc.GetProperty("authorizationToken").ToString();
        _apiUrl = doc.GetProperty("apiUrl").ToString();
        _downloadUrl = doc.GetProperty("downloadUrl").ToString();
    }

    /// <summary>
    /// Ask upload authorization
    /// </summary>
    /// <returns>Tuple containing the upload url and he upload authorization token</returns>
    private async Task<(string, string)> GetUploadAuthorization()
    {
        _logger.Debug("Trying to get upload authorization");
        var request = new HttpRequestMessage(HttpMethod.Post, $"{_apiUrl}/b2api/v2/b2_get_upload_url");
        request.Headers.TryAddWithoutValidation("Authorization", $"{_accountAuthorizationToken}");
        request.Content = new StringContent("{\"bucketId\":\""+_config.BucketId+"\"}", Encoding.UTF8, "application/json");
        var responseMessage = await _httpClient.SendAsync(request);
        var content = await responseMessage.Content.ReadAsStringAsync();
        var jsonElement = JsonDocument.Parse(content).RootElement;
        var uploadUrl = jsonElement.GetProperty("uploadUrl").ToString() ??
                        throw new ApplicationException("Failed to retrieve upload url");
        var authorizationToken = jsonElement.GetProperty("authorizationToken").ToString();
        return (uploadUrl,authorizationToken);
    }

    /// <summary>
    /// Upload a given file
    /// </summary>
    /// <param name="fileName">The file name that will be used</param>
    /// <param name="fileContent">The content of the file as byte array</param>
    /// <returns>The fileId on Backblaze</returns>
    public async Task<string> Upload(string fileName,byte[] fileContent)
    {
        _logger.Information("Uploading the file {Name}, {Size} bytes", fileName,fileContent.Length);
        var (uploadUrl,uploadAuthorization) = await GetUploadAuthorization();
        var request = (HttpWebRequest)WebRequest.Create(uploadUrl);
        request.Method = "POST";
        request.Headers.Add("Authorization", uploadAuthorization);
        request.Headers.Add("X-Bz-File-Name", fileName);
        request.Headers.Add("X-Bz-Content-Sha1", "do_not_verify");
        request.Headers.Add("X-Bz-Info-Author", "unknown");
        request.ContentType = $"image/{fileName.Split(".").Last()}";
        await using (var stream = request.GetRequestStream())
        {
            await stream.WriteAsync(fileContent.AsMemory(0, fileContent.Length));
            stream.Close();
        }
        WebResponse response = (HttpWebResponse)request.GetResponse();
        var responseString = await new StreamReader(response.GetResponseStream()).ReadToEndAsync();
        response.Close();
        var doc = JsonDocument.Parse(responseString).RootElement;
        _httpClient.DefaultRequestHeaders.Remove("Authorization");
        return doc.GetProperty("fileId").ToString() ?? throw new ApplicationException("File upload failed");
    }

    /// <summary>
    /// Download a file from Backblaze
    /// </summary>
    /// <param name="fileId">The fileId to download</param>
    /// <returns>A stream containing the file content</returns>
    public async Task<Stream> Download(string fileId)
    {
        _logger.Information("Downloading file with Id {Id}",fileId);
        var downloadUrl = $"{_downloadUrl}/b2api/v2/b2_download_file_by_id?fileId={fileId}";
        var request = new HttpRequestMessage(HttpMethod.Get, downloadUrl);
        request.Headers.TryAddWithoutValidation("Authorization", _accountAuthorizationToken);
        var responseMessage = await _httpClient.SendAsync(request);
        return await responseMessage.Content.ReadAsStreamAsync();
    }

    /// <summary>
    /// Get a file's filename
    /// </summary>
    /// <param name="fileId">The id of the file we want to get the name</param>
    /// <returns>The file name on Backblaze</returns>
    public async Task<string> GetFileName(string fileId)
    {
        _logger.Information("Get file name for file with id {Id}",fileId);
        var request = new HttpRequestMessage(HttpMethod.Get, $"{_apiUrl}/b2api/v2/b2_get_file_info?fileId={fileId}");
        request.Headers.TryAddWithoutValidation("Authorization", _accountAuthorizationToken);
        var responseMessage = await _httpClient.SendAsync(request);
        var doc = JsonDocument.Parse(await responseMessage.Content.ReadAsStringAsync()).RootElement;
        return doc.GetProperty("fileName").ToString() ?? throw new ApplicationException("Could not retrieve file name");
    }

    /// <summary>
    /// Delete a remote file on Backblaze
    /// </summary>
    /// <param name="fileId">The id of the file</param>
    /// <param name="fileName">The name of the file</param>
    public async Task Delete(string fileId, string fileName)
    {
        _logger.Information("Deleting file {Id} with name {Name}",fileId,fileName);
        var request = new HttpRequestMessage(HttpMethod.Post,$"{_apiUrl}/b2api/v2/b2_delete_file_version");
        request.Headers.TryAddWithoutValidation("Authorization", _accountAuthorizationToken);
        var json = JsonSerializer.Serialize(new
        {
            fileId, fileName
        });
        var content = new StringContent(json, Encoding.UTF8, "application/json");
        request.Content = content;
        var responseMessage = await _httpClient.SendAsync(request);
        responseMessage.EnsureSuccessStatusCode();
        _logger.Information("File {Name} deleted successfully with Http status {Status}", fileName,responseMessage.StatusCode);
    }
}