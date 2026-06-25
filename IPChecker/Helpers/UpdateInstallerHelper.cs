using System.Diagnostics;
using System.Net.Http.Headers;

namespace IPChecker.Helpers;

internal static class UpdateInstallerHelper
{
    private static readonly HttpClient HttpClient = CreateHttpClient();

    public static bool IsInstallerUrl(string? url) =>
        !string.IsNullOrWhiteSpace(url)
        && Uri.TryCreate(url, UriKind.Absolute, out var uri)
        && uri.Scheme is "http" or "https"
        && uri.AbsolutePath.EndsWith(".exe", StringComparison.OrdinalIgnoreCase);

    public static async Task<string> DownloadInstallerAsync(
        string downloadUrl,
        CancellationToken cancellationToken = default)
    {
        var fileName = Path.GetFileName(new Uri(downloadUrl).LocalPath);
        if (string.IsNullOrWhiteSpace(fileName))
        {
            fileName = "IPChecker-Setup-latest-x64.exe";
        }

        var directory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "IPChecker",
            "updates");
        Directory.CreateDirectory(directory);

        foreach (var oldFile in Directory.EnumerateFiles(directory, "IPChecker-Setup-*-x64.exe"))
        {
            if (!string.Equals(Path.GetFileName(oldFile), fileName, StringComparison.OrdinalIgnoreCase))
            {
                try
                {
                    File.Delete(oldFile);
                }
                catch
                {
                    // Best effort cleanup of stale installers.
                }
            }
        }

        var destinationPath = Path.Combine(directory, fileName);
        if (File.Exists(destinationPath))
        {
            try
            {
                File.Delete(destinationPath);
            }
            catch
            {
                destinationPath = Path.Combine(directory, $"{Path.GetFileNameWithoutExtension(fileName)}-{Guid.NewGuid():N}.exe");
            }
        }

        using var response = await HttpClient
            .GetAsync(downloadUrl, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
            .ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        await using var contentStream = await response.Content
            .ReadAsStreamAsync(cancellationToken)
            .ConfigureAwait(false);
        await using var fileStream = File.Create(destinationPath);
        await contentStream.CopyToAsync(fileStream, cancellationToken).ConfigureAwait(false);

        return destinationPath;
    }

    public static void LaunchInstaller(string installerPath)
    {
        Process.Start(new ProcessStartInfo(installerPath)
        {
            UseShellExecute = true,
        });
    }

    private static HttpClient CreateHttpClient()
    {
        var client = new HttpClient
        {
            Timeout = TimeSpan.FromMinutes(10),
        };
        client.DefaultRequestHeaders.UserAgent.ParseAdd("IPChecker-UpdateCheck/1.0");
        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/octet-stream"));
        return client;
    }
}
