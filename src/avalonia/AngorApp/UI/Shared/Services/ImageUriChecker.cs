namespace AngorApp.UI.Shared.Services;

using System.Net.Http;
using CSharpFunctionalExtensions;

public interface IImageValidationService
{
    Task<Result<bool>> IsImage(string url);
}

public sealed class ImageValidationService(IHttpClientFactory factory) : IImageValidationService
{
    public async Task<Result<bool>> IsImage(string url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
        {
            return Result.Failure<bool>("Malformed URL");
        }

        var client = factory.CreateClient("image-validation");

        var head = await TryHead(client, uri);
        if (head.IsSuccess)
        {
            return Result.Success(IsImageMime(head.Value));
        }

        var get = await TryGetHeaders(client, uri);
        if (get.IsSuccess)
        {
            return Result.Success(IsImageMime(get.Value));
        }

        return Result.Failure<bool>("No valid headers retrieved");
    }

    private async Task<Result<string>> TryHead(HttpClient client, Uri uri)
    {
        try
        {
            using var req = new HttpRequestMessage(HttpMethod.Head, uri);
            using var resp = await client.SendAsync(req, HttpCompletionOption.ResponseHeadersRead);

            if (!resp.IsSuccessStatusCode)
            {
                return Result.Failure<string>("HEAD unsuccessful");
            }

            var mime = resp.Content.Headers.ContentType?.MediaType;
            return mime != null
                ? Result.Success(mime)
                : Result.Failure<string>("Missing Content-Type");
        }
        catch
        {
            return Result.Failure<string>("HEAD error");
        }
    }

    private async Task<Result<string>> TryGetHeaders(HttpClient client, Uri uri)
    {
        try
        {
            using var req = new HttpRequestMessage(HttpMethod.Get, uri);
            using var resp = await client.SendAsync(req, HttpCompletionOption.ResponseHeadersRead);

            if (!resp.IsSuccessStatusCode)
            {
                return Result.Failure<string>("GET unsuccessful");
            }

            var mime = resp.Content.Headers.ContentType?.MediaType;
            return mime != null
                ? Result.Success(mime)
                : Result.Failure<string>("Missing Content-Type");
        }
        catch
        {
            return Result.Failure<string>("GET error");
        }
    }

    private static bool IsImageMime(string mime)
    {
        return mime.StartsWith("image/", StringComparison.OrdinalIgnoreCase);
    }
}