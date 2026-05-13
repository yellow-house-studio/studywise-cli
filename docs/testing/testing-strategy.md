# Studywise CLI Teststrategi

## Status
**Datum:** 2026-05-09 (uppdaterad)
**Typ:** Testdokumentation
**Projekt:** Studywise CLI

---

## 1. Testprojekt — tre lager

```
test/
├── Studywise.CLI.UnitTests/           # Enhetstester
├── Studywise.CLI.IntegrationTests/     # Integrationstester
└── Studywise.CLI.E2ETests/           # E2E-tester
```

> **Namnkonvention:** `Studywise.CLI.{TestType}`

---

## 2. Testlager — översikt

| Lager | Process | HTTP | Vad det testar |
|-------|---------|------|----------------|
| UnitTests | Test-process | ❌ | Logik i isolation — inga dependencies |
| IntegrationTests | Test-process | ✅ WireMock.Net embedded | Handlers, response mapping, felhantering |
| E2ETests | **Separat CLI-process** | ✅ Dev Proxy | CLI som helhet, stdout, exit codes, output-format |

---

## 3. IntegrationTests

**Command handlers (eller motsvarande applikationslager) i samma process som testerna**, med HTTP mockad via WireMock.Net embedded.

För kommandon utan dedikerad handler-klass (t.ex. diagnosflöden) testas check-runner/formatter direkt i process, men fortfarande med riktig HTTP via embedded WireMock.

```csharp
public class IntegrationTests : IDisposable
{
    private readonly WireMockServer _server;

    public IntegrationTests() => _server = WireMockServer.Start();

    [Fact]
    public async Task ListEducationLevels_ReturnsFormattedTable()
    {
        _server.Given(Request.Create().WithPath("/api/v1/education-levels"))
            .RespondWith(Response.Create()
                .WithBody(JsonEducationLevels)
                .WithStatusCode(200));

        var handler = new ListEducationLevelsHandler(
            new HttpClient { BaseAddress = new Uri(_server.Url) });

        var result = await handler.HandleAsync(new ListEducationLevelsCommand());

        Assert.Contains("Sweden", result.Output);
    }

    public void Dispose() => _server.Dispose();
}
```

---

## 4. E2ETests

**CLI:n spawnas som separat process.** HTTP mockas via **Microsoft Dev Proxy** — en proxy på nätverksnivå som inte kräver några kodändringar i CLI:n.

### Varför Dev Proxy?

- **Ren .NET** — officiell Microsoft-produkt (`dotnet/dev-proxy`)
- **Inga kodändringar** — jobbar på nätverksnivå
- **Ingen Java** — allt är .NET
- **CI-vänligt** — kan installeras som global tool
- **Fungerar med alla HTTP-libs** — HttpClient, RestSharp, whatever

### Arkitektur

```
E2ETests
├── 1. Starta Dev Proxy (port 8000)
├── 2. Ladda mocks via mocks.json
└── 3. Process.Start() → CLI pekar mot localhost:8000
     ├── CLI skickar HTTP GET
     ├── Dev Proxy fångar → returnerar mock
     ├── CLI formaterar output till stdout
     └── Test verifierar stdout + exit code
```

### Setup

```bash
# Installera Dev Proxy (macOS)
brew tap dotnet/dev-proxy && brew install dev-proxy
```

Mock configuration files are in `test/.devproxy/`. The E2E test automatically starts Dev Proxy with the correct config, so no manual startup is needed.

```bash
# Kör alla tester (E2E starts Dev Proxy automatically)
dotnet test
```

---

## 5. Verktyg och dokumentation

| Resurs | Länk |
|--------|------|
| **Detaljerad guide** | `agent_work/deep-dive-2026-05-09-cli-e2e-testing-http-mock.md` |
| Dev Proxy (GitHub) | https://github.com/dotnet/dev-proxy |
| Dev Proxy (MS Learn) | https://learn.microsoft.com/en-us/microsoft-cloud/dev/dev-proxy |
| WireMock.Net | https://wiremock.org/dotnet/ |

---

## 6. Komma igång

```bash
# 1. Installera Dev Proxy (macOS)
brew tap dotnet/dev-proxy && brew install dev-proxy

# 2. Kör alla tester (E2E startar Dev Proxy automatiskt)
dotnet test
```

---

_Skapad 2026-05-09 | Uppdaterad 2026-05-09_
