using System.Net;
using System.Text;
using Dograc.Core.Models;
using Dograc.Core.Theme;
using Dograc.Infrastructure.Services;
using Dograc.Infrastructure.Settings;
using Xunit;

namespace Dograc.Core.Tests;

public sealed class Phase6ServicesTests
{
    [Fact]
    public void JsonSettingsStore_PersistsPreferencesAndDocumentLocalPaths()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"dograc-settings-{Guid.NewGuid():N}");
        var settingsPath = Path.Combine(tempDir, "settings.json");
        var samplePdf = Path.Combine(tempDir, "guide.pdf");
        Directory.CreateDirectory(tempDir);
        File.WriteAllText(samplePdf, "%PDF-1.4");

        try
        {
            var store = new JsonSettingsStore(settingsPath);
            store.Save(new AppSettings
            {
                ThemeMode = AppThemeMode.Dark,
                OllamaEndpointUrl = "http://127.0.0.1:11434",
                SelectedModelName = "llama3.1:8b",
                StopManagedServicesOnExit = false
            });
            store.RegisterDocumentLocalPath("guide.pdf", samplePdf);

            var reloaded = new JsonSettingsStore(settingsPath);
            Assert.Equal(AppThemeMode.Dark, reloaded.Current.ThemeMode);
            Assert.Equal("llama3.1:8b", reloaded.Current.SelectedModelName);
            Assert.False(reloaded.Current.StopManagedServicesOnExit);
            Assert.Equal(samplePdf, reloaded.ResolveDocumentLocalPath("guide.pdf"));
            Assert.Equal("http://127.0.0.1:11434/v1", reloaded.Current.NormalizedOllamaV1Endpoint);
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public async Task OllamaService_ParsesInstalledModelsFromApiTags()
    {
        var json = """
        {
          "models": [
            {
              "name": "qwen2.5:7b",
              "size": 4700000000,
              "details": { "parameter_size": "7.6B", "family": "qwen2" }
            },
            {
              "name": "gemma2:9b",
              "size": 5400000000,
              "details": { "parameter_size": "9B", "family": "gemma2" }
            }
          ]
        }
        """;

        using var client = new HttpClient(new InlineHandler((_, _) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            })));

        var service = new OllamaService(client);
        var result = await service.CheckAndListModelsAsync("http://localhost:11434/v1");

        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.Models.Count);
        Assert.Equal("qwen2.5:7b", result.Models[0].Name);
        Assert.Equal("7.6B", result.Models[0].ParameterSize);
    }

    [Fact]
    public async Task GitHubUpdateService_DetectsNewerVersionAndHandlesOffline()
    {
        var releaseJson = """
        {
          "tag_name": "v0.2.0",
          "body": "WPF 통합 데스크톱 릴리스",
          "html_url": "https://github.com/minwoo1119/dograc/releases/tag/v0.2.0",
          "assets": [
            {
              "name": "dograc-win-Setup.exe",
              "browser_download_url": "https://github.com/minwoo1119/dograc/releases/download/v0.2.0/dograc-win-Setup.exe"
            }
          ]
        }
        """;

        using var onlineClient = new HttpClient(new InlineHandler((_, _) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(releaseJson, Encoding.UTF8, "application/json")
            })));

        var updater = new GitHubUpdateService(onlineClient, "0.1.0");
        var check = await updater.CheckForUpdatesAsync();

        Assert.True(check.HasUpdate);
        Assert.Equal("v0.2.0", check.LatestVersion);
        Assert.Contains("dograc-win-Setup.exe", check.InstallerUrl);

        using var offlineClient = new HttpClient(new InlineHandler((_, _) =>
            throw new HttpRequestException("offline")));
        var offlineUpdater = new GitHubUpdateService(offlineClient, "0.1.0");
        var offlineResult = await offlineUpdater.CheckForUpdatesAsync();

        Assert.False(offlineResult.HasUpdate);
        Assert.Contains("오프라인", offlineResult.Message);
    }

    private sealed class InlineHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> _handler;

        public InlineHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> handler)
        {
            _handler = handler;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => _handler(request, cancellationToken);
    }
}
