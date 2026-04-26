using System.Net.Http;
using System.Net.Http.Headers;

namespace MusicSorter.Services;

internal static class SharedHttp
{
    public static readonly HttpClient Client = Build();

    private static HttpClient Build()
    {
        var c = new HttpClient(new SocketsHttpHandler
        {
            PooledConnectionLifetime = TimeSpan.FromMinutes(5),
            AutomaticDecompression = System.Net.DecompressionMethods.All
        })
        {
            Timeout = TimeSpan.FromSeconds(20)
        };
        c.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("MusicSorter", "1.0"));
        c.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("(github.com/stefs-time/music)"));
        c.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        return c;
    }
}
