using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using Application.Contracts.Interfaces;
using Application.DTOs.Updates;
using Microsoft.Extensions.Configuration;

namespace Infrastructure.Updates
{
    /// <summary>
    /// Читает манифест выпуска по ссылке из appsettings.json.
    ///
    /// Формат манифеста наш собственный и нарочно простой: его одинаково
    /// отдаёт и GitHub Pages, и raw.githubusercontent, и файл, приложенный
    /// к релизу. Привязки к конкретному хостингу нет.
    /// </summary>
    public class HttpUpdateFeed : IUpdateFeed
    {
        private static readonly JsonSerializerOptions Json = new()
        {
            PropertyNameCaseInsensitive = true,
        };

        private readonly IHttpClientFactory _factory;
        private readonly IConfiguration _configuration;

        public HttpUpdateFeed(IHttpClientFactory factory, IConfiguration configuration)
        {
            _factory = factory;
            _configuration = configuration;
        }

        public async Task<UpdateManifest> GetLatestAsync(CancellationToken ct)
        {
            var url = (_configuration["Updates:ManifestUrl"] ?? "").Trim();

            if (url.Length == 0)
                throw new InvalidOperationException("В appsettings.json не задан Updates:ManifestUrl");

            // Обновление приносит исполняемый код: по открытому http его
            // подменит кто угодно в той же сети
            if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps)
                throw new InvalidOperationException("Ссылка на манифест должна быть https");

            var client = _factory.CreateClient(UpdateHttp.ClientName);

            // GitHub Pages и raw отдают закешированное: просим свежее
            var request = new HttpRequestMessage(HttpMethod.Get, uri);
            request.Headers.CacheControl = new System.Net.Http.Headers.CacheControlHeaderValue
            {
                NoCache = true,
            };

            using var response = await client.SendAsync(request, ct);

            response.EnsureSuccessStatusCode();

            var manifest = await response.Content.ReadFromJsonAsync<UpdateManifest>(Json, ct)
                ?? throw new InvalidOperationException("Манифест пуст");

            if (string.IsNullOrWhiteSpace(manifest.Version))
                throw new InvalidOperationException("В манифесте не указана версия");

            return manifest;
        }
    }

    /// <summary>Общее имя клиента: таймаут и заголовки настраиваются один раз.</summary>
    public static class UpdateHttp
    {
        public const string ClientName = "updates";
    }
}
