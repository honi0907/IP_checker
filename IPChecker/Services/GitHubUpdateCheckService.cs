using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization;
using IPChecker.Helpers;
using IPChecker.Models;

namespace IPChecker.Services;

public sealed class GitHubUpdateCheckService : IUpdateCheckService
{
    private const string Repository = "honi0907/IP_checker";
    private const string LatestReleaseUrl = $"https://api.github.com/repos/{Repository}/releases/latest";

    private static readonly HttpClient HttpClient = CreateHttpClient();

    public async Task<AppUpdateInfo?> CheckLatestAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            using var response = await HttpClient
                .GetAsync(LatestReleaseUrl, cancellationToken)
                .ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
            {
                App.WriteStartupLog($"Update check failed: HTTP {(int)response.StatusCode}");
                return null;
            }

            await using var stream = await response.Content
                .ReadAsStreamAsync(cancellationToken)
                .ConfigureAwait(false);

            var release = await JsonSerializer
                .DeserializeAsync<GitHubReleaseResponse>(stream, JsonOptions, cancellationToken)
                .ConfigureAwait(false);

            if (release?.TagName is null || release.HtmlUrl is null)
            {
                return null;
            }

            var latestVersion = AppVersionHelper.NormalizeReleaseTag(release.TagName);
            if (latestVersion is null)
            {
                return null;
            }

            var installerUrl = release.Assets?
                .FirstOrDefault(a => a.Name?.StartsWith("IPChecker-Setup-", StringComparison.OrdinalIgnoreCase) == true
                    && a.Name.EndsWith("-x64.exe", StringComparison.OrdinalIgnoreCase))
                ?.BrowserDownloadUrl;

            return new AppUpdateInfo
            {
                LatestVersion = latestVersion,
                ReleasePageUrl = release.HtmlUrl,
                InstallerDownloadUrl = installerUrl,
                IsNewerThanCurrent = AppVersionHelper.IsRemoteNewer(latestVersion),
            };
        }
        catch (Exception ex)
        {
            App.WriteStartupLog($"Update check failed: {ex.Message}");
            return null;
        }
    }

    private static HttpClient CreateHttpClient()
    {
        var client = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(15),
        };
        client.DefaultRequestHeaders.UserAgent.ParseAdd("IPChecker-UpdateCheck/1.0");
        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        return client;
    }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    private sealed class GitHubReleaseResponse
    {
        [JsonPropertyName("tag_name")]
        public string? TagName { get; set; }

        [JsonPropertyName("html_url")]
        public string? HtmlUrl { get; set; }

        [JsonPropertyName("assets")]
        public List<GitHubReleaseAsset>? Assets { get; set; }
    }

    private sealed class GitHubReleaseAsset
    {
        [JsonPropertyName("name")]
        public string? Name { get; set; }

        [JsonPropertyName("browser_download_url")]
        public string? BrowserDownloadUrl { get; set; }
    }
}
