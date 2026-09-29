namespace Studywise.CLI.IntegrationTests;

/// <summary>
/// Marker base class for integration tests. The production code under
/// test reads its base URL from <c>STUDYWISE_API_BASE_URL</c> (set in
/// <c>Program.cs</c> from <c>ApplicationConfig.ApiBaseUrl</c>), but
/// the existing integration tests build their own
/// <see cref="System.Net.Http.IHttpClientFactory"/> via
/// <c>WireMockServer.Start()</c> and set <c>STUDYWISE_API_BASE_URL</c>
/// to the WireMock URL inside the test body. Because the URL is
/// assigned per-test, no env-var guard runs in <c>[OneTimeSetUp]</c>.
///
/// If a future integration test depends on a URL provided by the
/// surrounding CI config (rather than constructing its own), add a
/// loopback-address assertion here so the test fails fast in CI
/// rather than silently dialing the real Studywise API.
/// </summary>
public abstract class BaseIntegrationTest
{
}
