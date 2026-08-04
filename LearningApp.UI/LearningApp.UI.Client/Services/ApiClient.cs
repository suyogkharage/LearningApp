using LearningApp.UI.Shared.Common;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace LearningApp.UI.Client.Services;

public sealed class ApiClient(HttpClient httpClient)
{
    public async Task<ApiResponse<T>> GetAsync<T>(string path, CancellationToken cancellationToken = default)
    {
        return await SendAsync<T>(() => httpClient.GetAsync(ToBffPath(path), cancellationToken), cancellationToken);
    }

    public async Task<ApiResponse<T>> PostAsync<T>(string path, object request, CancellationToken cancellationToken = default)
    {
        return await SendAsync<T>(() => httpClient.PostAsJsonAsync(ToBffPath(path), request, cancellationToken), cancellationToken);
    }

    public async Task<ApiResponse<T>> PutAsync<T>(string path, object request, CancellationToken cancellationToken = default)
    {
        return await SendAsync<T>(() => httpClient.PutAsJsonAsync(ToBffPath(path), request, cancellationToken), cancellationToken);
    }

    public async Task<ApiResponse<object>> DeleteAsync(string path, CancellationToken cancellationToken = default)
    {
        return await SendAsync<object>(() => httpClient.DeleteAsync(ToBffPath(path), cancellationToken), cancellationToken);
    }

    private static async Task<ApiResponse<T>> SendAsync<T>(
        Func<Task<HttpResponseMessage>> send,
        CancellationToken cancellationToken)
    {
        var response = await send();
        return await ReadAsync<T>(response, cancellationToken);
    }

    private static async Task<ApiResponse<T>> ReadAsync<T>(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        if (response.StatusCode == HttpStatusCode.Unauthorized)
        {
            return ApiResponse<T>.Fail("The API rejected the access token. Check the API app registration audience, scope, and supported account type.");
        }

        if (response.StatusCode == HttpStatusCode.Forbidden)
        {
            return ApiResponse<T>.Fail("You are signed in, but you do not have permission to access this resource.");
        }

        if (response.StatusCode == HttpStatusCode.TooManyRequests)
        {
            var error = await TryReadErrorAsync(response, cancellationToken);
            return ApiResponse<T>.Fail(error?.Message ?? "Too many requests. Please wait a moment and try again.");
        }

        if (!response.IsSuccessStatusCode)
        {
            var error = await TryReadErrorAsync(response, cancellationToken);
            var message = !string.IsNullOrWhiteSpace(error?.Message)
                ? error.Message
                : string.IsNullOrWhiteSpace(response.ReasonPhrase)
                    ? $"The API returned HTTP {(int)response.StatusCode}."
                    : $"The API returned HTTP {(int)response.StatusCode}: {response.ReasonPhrase}.";

            return ApiResponse<T>.Fail(message, error?.Errors);
        }

        var body = await response.Content.ReadFromJsonAsync<ApiResponse<T>>(cancellationToken: cancellationToken);
        return body ?? ApiResponse<T>.Fail("The server returned an empty response.");
    }

    private static async Task<ApiErrorResponse?> TryReadErrorAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        try
        {
            return await response.Content.ReadFromJsonAsync<ApiErrorResponse>(cancellationToken: cancellationToken);
        }
        catch (JsonException)
        {
            return null;
        }
        catch (NotSupportedException)
        {
            return null;
        }
    }

    private static string ToBffPath(string path)
    {
        var normalizedPath = path.TrimStart('/');
        return normalizedPath.StartsWith("bff/", StringComparison.OrdinalIgnoreCase)
            ? normalizedPath
            : $"bff/{normalizedPath}";
    }
}
