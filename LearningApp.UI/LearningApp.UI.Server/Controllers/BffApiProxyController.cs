using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Net.Http.Headers;

namespace LearningApp.UI.Server.Controllers;

[ApiController]
[Authorize]
[Route("bff/api/{**path}")]
public sealed class BffApiProxyController(IHttpClientFactory httpClientFactory) : ControllerBase
{
    private static readonly HashSet<string> HeadersToSkip = new(StringComparer.OrdinalIgnoreCase)
    {
        "Host",
        "Cookie",
        "Authorization",
        "Accept-Encoding",
        "Content-Length",
        "Transfer-Encoding",
        "Connection"
    };

    [HttpGet]
    [HttpPost]
    [HttpPut]
    [HttpDelete]
    public async Task<IActionResult> Proxy(string path, CancellationToken cancellationToken)
    {
        var accessToken = await HttpContext.GetTokenAsync("access_token");
        if (string.IsNullOrWhiteSpace(accessToken))
        {
            return Unauthorized();
        }

        using var request = CreateProxiedRequest(path, accessToken);
        var apiClient = httpClientFactory.CreateClient("LearningApi");
        using var response = await apiClient.SendAsync(
            request,
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken);

        var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
        var contentType = response.Content.Headers.ContentType?.ToString() ?? "application/json";
        return new ContentResult
        {
            Content = responseBody,
            ContentType = contentType,
            StatusCode = (int)response.StatusCode
        };
    }

    private HttpRequestMessage CreateProxiedRequest(string path, string accessToken)
    {
        var apiPath = $"api/{path}";
        var targetPath = string.IsNullOrWhiteSpace(Request.QueryString.Value)
            ? apiPath
            : $"{apiPath}{Request.QueryString.Value}";

        var request = new HttpRequestMessage(new HttpMethod(Request.Method), targetPath);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        foreach (var header in Request.Headers)
        {
            if (!HeadersToSkip.Contains(header.Key))
            {
                request.Headers.TryAddWithoutValidation(header.Key, header.Value.ToArray());
            }
        }

        if (Request.ContentLength > 0 || Request.Headers.ContainsKey("Content-Type"))
        {
            request.Content = new StreamContent(Request.Body);
            foreach (var header in Request.Headers)
            {
                if (!HeadersToSkip.Contains(header.Key))
                {
                    request.Content.Headers.TryAddWithoutValidation(header.Key, header.Value.ToArray());
                }
            }
        }

        return request;
    }
}
