using Microsoft.Extensions.DependencyInjection;
using Studywise.Cli.Configuration;

namespace Studywise.Cli.Http;

/// <summary>
/// Applies the CLI's shared <see cref="HttpClient"/> defaults to an SDK typed client.
/// </summary>
/// <remarks>
/// The SDK's <c>AddStudywiseClient</c> registers each typed client itself and offers no
/// hook for the primary message handler, so the CLI re-registers the clients it uses
/// on top of the SDK's registration. Both registrations configure the same named
/// <see cref="HttpClient"/>, so the SDK's base address, timeout and API-key handler
/// all still apply.
/// </remarks>
public static class StudywiseHttpClientExtensions
{
    /// <summary>
    /// Adds the CLI's User-Agent and disables automatic redirect following.
    /// </summary>
    /// <param name="builder">The client builder to configure.</param>
    /// <param name="config">Resolved CLI configuration (the User-Agent comes from here).</param>
    /// <returns>The same <paramref name="builder"/> for chaining.</returns>
    public static IHttpClientBuilder UseStudywiseDefaults(this IHttpClientBuilder builder, ApplicationConfig config)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(config);

        return builder
            .ConfigureHttpClient(client =>
            {
                if (!client.DefaultRequestHeaders.Contains("User-Agent"))
                {
                    client.DefaultRequestHeaders.Add("User-Agent", config.UserAgent);
                }
            })
            .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
            {
                // Disabled so X-Studywise-Api-Key cannot leak across a 3xx to a
                // different host. The SDK attaches the API key to every request,
                // so following a redirect would forward the credential.
                // Tracked upstream as studywise-api#890.
                AllowAutoRedirect = false
            });
    }
}
