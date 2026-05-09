# Deep-Dive: CLI E2E Testing — Process-Spawn med HTTP-Mock

**Datum:** 2026-05-09
**Scope:** Hur testar man en .NET CLI-applikation end-to-end där CLI:n körs i separat process, HTTP-anrop fångas upp och returnerar mockad data — utan riktiga nätverksanrop.
**Credibility-skala:** 1=Obey (officiell), 2=Trust (vetenskaplig), 3=Consider (blogg), 4=Evaluate (social media)

---

## Sammanfattning

Att testa en CLI E2E med HTTP-mock kräver två separata ting:
1. **Process-spawn** — CLI:n körs som en riktig process utanför testprocessen
2. **HTTP-interception** — HTTP-anropen från CLI-processen fångas och besvaras med mock-data

Det finns flera etablerade mönster för detta i .NET-ekosystemet. Det vanligaste för ren HTTP-mock är **WireMock.NET** som embedded server. Det vanligaste för process-baserade CLI-tester är **`Process.Start()` med `RedirectStandardOutput`**.

---

## 1. Tvådimensionell teststrategi

Vi måste lösa två oberoende problem:

| Problem | Lösning |
|---------|---------|
| CLI:n körs i separat process | `Process.Start()` + `ProcessStartInfo` med redirects |
| HTTP-anrop mockas | WireMock.NET (embedded server) eller `HttpClient`-mock |

---

## 2. Process-Spawn Mönstret (CLI i egen process)

### Grunden: ProcessStartInfo

```csharp
[Fact]
public void ListEducationLevels_OutputsTable()
{
    // Arrange
    var cliPath = Path.GetFullPath("../../../src/Studywise.CLI/bin/Debug/net9.0/studywise");
    var baseUrl = "http://localhost:9876";
    
    using var process = new Process { StartInfo = new ProcessStartInfo
    {
        FileName = cliPath,
        Arguments = $"list education-levels --base-url {baseUrl}",
        RedirectStandardOutput = true,
        RedirectStandardError = true,
        UseShellExecute = false
    }};
    
    // Act
    process.Start();
    var stdout = process.StandardOutput.ReadToEndAsync().GetAwaiter().GetResult();
    process.WaitForExit();
    
    // Assert
    Assert.Equal(0, process.ExitCode);
    Assert.Contains("Education Levels", stdout);
    Assert.Contains("Sweden", stdout);
}
```

**Nyckelattribut:**
- `RedirectStandardOutput = true` — fånga stdout
- `RedirectStandardError = true` — fånga stderr
- `UseShellExecute = false` — krävs för redirects
- `CreateNoWindow = true` — undvik konsollfönster

### Två varianter av process-spawn

**Variant A: Kompilerad binär**
```csharp
var cliPath = Path.Combine(repoRoot, "src", "Studywise.CLI", "bin", "Debug", "net9.0", "studywise");
```
- CLI:n måste byggas före testerna
- Kan läggas som MSBuild-target som kör före testerna

**Variant B: `dotnet run` i processen**
```csharp
var dotnetRunInfo = new ProcessStartInfo
{
    FileName = "dotnet",
    Arguments = $"run --project {cliProjectPath}",
    ...
};
```
- Enklare — ingen pre-build behövs
- Något långsammare (dotnet run overhead)

---

## 3. HTTP-Mock: WireMock.NET (Embedded)

WireMock.NET kan köras som **embedded server i samma testprocess** — men det löser bara problemet för testprocessen, inte för en separat CLI-process.

**För att mocka HTTP för en separat CLI-process behöver WireMock köras som en _separat process_ eller på en fast port som CLI:n pekar mot.**

### Tvåarkitektur för WireMock + CLI-process

```
┌─────────────────────────────────────────────────────────┐
│  Test Process                                         │
│  ├── Start WireMock.Server (port 9876)                │
│  ├── Konfigurera stubs (GET /education-levels → ...) │
│  └── Process.Start() → CLI som kör mot localhost:9876 │
│       ├── CLI skickar HTTP GET                        │
│       ├── WireMock fångar → returnerar mock            │
│       ├── CLI formaterar output                       │
│       ├── CLI skriver till stdout                     │
│       └── Test verifierar stdout                      │
└─────────────────────────────────────────────────────────┘
```

**Alternativ A: WireMock som separat process**
```csharp
var wireMockPath = FindWireMockStandalone();
using var wiremock = new Process { StartInfo = new ProcessStartInfo
{
    FileName = wireMockPath,
    Arguments = "--port 9876",
    UseShellExecute = false
}};
wiremock.Start();
Thread.Sleep(1000); // vänta på startup
```

**Alternativ B: WireMock.Net embedded som TestServer**
- WireMock.Net kan köras embedded — men CLI-processen kan inte nå den
- Därför: WireMock som standalone JAR med Java, eller dotnet-körbar

### WireMock Standalone (enklast)

```csharp
[Fact]
public async Task ListEducationLevels_WithMockApi_ReturnsTable()
{
    // 1. Starta WireMock på port
    var wireMockPort = 9876;
    var wireMockDir = FindWireMockStandalone();
    
    using var wiremock = new Process { StartInfo = new ProcessStartInfo
    {
        FileName = "java", // WireMock kräver Java
        Arguments = $"-jar {wireMockDir}/wiremock-standalone.jar --port {wireMockPort}",
        UseShellExecute = false
    }};
    wiremock.Start();
    
    // Vänta på att WireMock ska starta
    await WaitForUrl($"http://localhost:{wireMockPort}/__admin");
    
    // 2. Konfigurera stubs via REST API
    await ConfigureStub(wireMockPort, "/api/v1/education-levels", JsonEducationLevels);
    
    // 3. Starta CLI:n
    var cliPath = GetCliPath();
    using var cli = new Process { StartInfo = new ProcessStartInfo
    {
        FileName = "dotnet", Arguments = $"run --project {cliPath}",
        Environment = { ["API_BASE_URL"] = $"http://localhost:{wireMockPort}" },
        RedirectStandardOutput = true, UseShellExecute = false
    }};
    cli.Start();
    var output = await cli.StandardOutput.ReadToEndAsync();
    cli.WaitForExit();
    
    // 4. Verifiera
    Assert.Contains("Master", output); // Swedish "master degree"
    Assert.Equal(0, cli.ExitCode);
}
```

---

## 4. WireMock i samma process (IntegrationTests, ej E2E)

För **IntegrationTests** (samma process) är WireMock.Net enkelt:

```csharp
public class IntegrationTests : IDisposable
{
    private readonly WireMockServer _server;
    
    public IntegrationTests()
    {
        _server = WireMockServer.Start();
    }
    
    [Fact]
    public async Task ListEducationLevels_ReturnsFormattedOutput()
    {
        // Mock inom samma process
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

**Källa:** Code Maze — "Seamless Integration Testing With WireMock.NET" (Credibility: 3 — blogg)
**Källa:** WireMock.NET docs — https://wiremock.org/dotnet/ (Credibility: 1 — officiell)

---

## 5. TestServer / WebApplicationFactory för API-test

Ett annat etablerat mönster i .NET är **TestServer** eller **WebApplicationFactory** — för att skapa en minimal ASP.NET Core-app i testprocessen. Men detta gäller API-test, inte CLI-test.

- **CLI IntegrationTests** (samma process) → WireMock.Net embedded
- **CLI E2ETests** (separat process) → WireMock standalone + process-spawn
- **API IntegrationTests** → `WebApplicationFactory<T>` med inbäddad test-server

---

## 6. Två testlager för CLI:n — kombinerat

### IntegrationTests (samma process)
- Testar **handlers** med WireMock.Net embedded
- `HttpClient` pekar mot `wiremockserver.Url`
- Snabba, stabila
- Testar: logik, parsing, felhantering, response mapping

### E2ETests (separat process)
- Testar **CLI-appen som helhet**
- CLI-processen startas via `Process.Start()`
- WireMock standalone körs på fast port
- CLI:n konfigureras (via env var eller CLI-argument) att peka mot WireMock
- Testar: help, exit codes, output-format, argument-parsing i "skarpt läge"

**Det kritiska beslutet:** Vilken HTTP-mock-arkitektur?

| Metod | CLI-process | IntegrationTests | Komplexitet |
|------|-----------|-----------------|-------------|
| WireMock.Net embedded | ❌ (når inte processen) | ✅ | Låg |
| WireMock standalone (Java) | ✅ | ✅ (via URL) | Medium |
| **Dev Proxy (dotnet)** | ✅ | ✅ | **Låg** |
| MockServer (dotnet) | ✅ | ✅ | Medium |
| Testserver (WebApplicationFactory) | ❌ (CLI når den inte) | ✅ (mockad HttpClient) | Medium |

**Rekommendation:** Dev Proxy för E2ETests — ren .NET, officiell Microsoft, inga kodändringar.

---

## 7. Referenser

1. [WireMock.NET — API Mocking for .NET](https://wiremock.org/dotnet/) — Officiell dokumentation (Credibility: 1)
2. [Seamless Integration Testing With WireMock.NET — Code Maze](https://code-maze.com/integration-testing-wiremock-dotnet/) — Blogg, steg-för-steg guide (Credibility: 3)
3. [WireMock.Net GitHub](https://github.com/wiremock/WireMock.Net) — Officiellt bibliotek med Testcontainers-stöd (Credibility: 1)
4. [Real world mocking! HTTP Services testing in C# using Wiremock.NET — Xebia](https://xebia.com/blog/real-world-mocking-http-services-testing-in-c-using-wiremock-net/) — Produktionserfarenhet (Credibility: 3)
5. [dotnet/command-line-api Issue #1670 — Dry run mode](https://github.com/dotnet/command-line-api/issues/1670) — CLI-testdiskussion, officiell källa (Credibility: 1)

---

## 8. Öppna frågor / Osäkra områden

1. **WireMock standalone kräver Java** — finns det en ren .NET-alternativ? `MockServer` (NUnit.MockFramework) eller en enkel ASP.NET Core minimal app som mock-server?
2. **CLI:n konfiguration** — hur konfigurerar CLI:n sin API-base-url? Miljövariabel, CLI-argument, eller config-fil?
3. **Dockercontainers för WireMock** — WireMock.Net har Testcontainers-stöd. Är det relevant för studywise-CLI?

---

## 9. Rekommendation

För Studywise CLI E2E-tests:

1. **IntegrationTests (UnitTests + IntegrationTests):** WireMock.Net embedded i samma process — för handler-logik och response mapping
2. **E2ETests:** WireMock standalone (Java) ELLER en enkel ASP.NET Core minimal mock-server + `Process.Start()` för CLI-processen

Valet beror på hur enkelt det är att sätta upp WireMock standalone i CI-miljön. Om Java är på plats → WireMock standalone. Om inte → minimal dotnet-baserad mock-server.

**Nästa steg:** undersök om det finns en ren .NET-alternativ till WireMock standalone (t.ex. en enkel ASP.NET Core app med bara ett par endpoints som kan startas i bakgrunden).

---

## 10. Microsoft Dev Proxy — DEN LÖSNINGEN

**Detta är den etablerade Microsoft-lösningen för exakt detta problem.**

[Dev Proxy](https://github.com/dotnet/dev-proxy) är en **proxy på nätverksnivå** som:
- Intercepterar ALL HTTP/HTTPS-trafik transparent — utan kodändringar
- Fungerar med **vilken HTTP-library som helst** — HttpClient, RestSharp, whatever
- Kan definiera **mock responses** via en JSON-fil
- Kan simulera throttling, errors, latency, chaos
- Är en ren **.NET-applikation** — ingen Java
- Körs som en lokal proxy på t.ex. `localhost:8000`

### Arkitekturen med Dev Proxy + CLI-process

```
Test Process (E2ETests)
├── 1. Starta Dev Proxy (dotnet devproxy)
├── 2. Ladda mocks via mocks.json
└── 3. Process.Start() → CLI pekar mot localhost:8000
     ├── CLI skickar HTTP GET
     ├── Dev Proxy fångar → returnerar mock
     ├── CLI formaterar output
     └── Test verifierar stdout
```

**Inga kodändringar i CLI:n!** Dev Proxy jobbar på nätverksnivå. CLI:n behöver inte veta om att den pratar med en proxy.


### Fördelar över WireMock

| Aspekt | WireMock | Dev Proxy |
|--------|----------|----------|
| Java krävs | ✅ (standalone) | ❌ ren .NET |
| Kodändring i app | ❌ | ❌ |
| Fungerar med alla HTTP-libs | ❌ | ✅ |
| Officiell Microsoft | ❌ | ✅ |
| CI-vänlig | Medium | ✅ |
| Record & playback | Via WireMock.Net | ✅ inbyggt |

### Setup-exempel

```bash
# Installera Dev Proxy
dotnet tool install -g Microsoft.devproxy

# Starta med mock-filer:
devproxy --mocks-urls mocks.json

# Eller spela in för record/playback:
devproxy --record --output recordings/
```

**Källa:** [dotnet/dev-proxy på GitHub](https://github.com/dotnet/dev-proxy) (Credibility: 1)
**Källa:** [Dev Proxy vs unit tests — Microsoft Learn](https://learn.microsoft.com/en-us/microsoft-cloud/dev/dev-proxy/concepts/dev-proxy-vs-unit-tests) (Credibility: 1)

---

## 11. Slutsatser och uppdaterad rekommendation


**Nu har vi en tydlig standard: Microsoft Dev Proxy.**


| Lager | Process | HTTP-mock | Verktyg |
|-------|---------|-----------|---------|
| UnitTests | Samma | ❌ | vanilla xUnit + mocked deps |
| IntegrationTests | Samma | WireMock.Net embedded | `WireMockServer.Start()` |
| E2ETests | **Separat** | Dev Proxy (proxy-nivå) | `Process.Start()` + `mocks.json` |


**Johanna hade rätt:** "Detta måste vara ett löst problem." — **Det är det.** Dev Proxy är Microsofts egen lösning, ren .NET, nätverksbaserad, CI-vänlig.


---

## 12. Referenser (uppdaterade)


1. [dotnet/dev-proxy — GitHub](https://github.com/dotnet/dev-proxy) — Officiell Microsoft (Credibility: 1)
2. [Dev Proxy vs unit tests — Microsoft Learn](https://learn.microsoft.com/en-us/microsoft-cloud/dev/dev-proxy/concepts/dev-proxy-vs-unit-tests) (Credibility: 1)
3. [Mock responses - Dev Proxy | Microsoft Learn](https://learn.microsoft.com/en-us/microsoft-cloud/dev/dev-proxy/how-to/mock-responses) (Credibility: 1)
4. [Build & test resilient apps in .NET with Dev Proxy — DevBlogs](https://devblogs.microsoft.com/dotnet/build-test-resilient-apps-dotnet-dev-proxy/) (Credibility: 1)
5. [Integration tests for .NET CLI tools — Anton Sizikov](https://blog.cloud-eng.nl/2023/01/22/dotnet-cli-integration-tests/) — CliRunner-mönstret (Credibility: 3)
6. [Azure-Samples/record-playback-test-proxy-demo-csharp](https://github.com/Azure-Samples/record-playback-test-proxy-demo-csharp) (Credibility: 1)
7. [WireMock.NET — API Mocking for .NET](https://wiremock.org/dotnet/) (Credibility: 1)


---

_Modell: MiniMax-M2.7 | Uppskattad kostnad: ~0.15 USD_
