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

**Command handlers i samma process som testerna**, med HTTP mockad via WireMock.Net embedded.

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
# Installera Dev Proxy
dotnet tool install -g Microsoft.devproxy
```

```csharp
[Fact]
public void ListEducationLevels_WithMockApi_ReturnsTable()
{
    // Starta Dev Proxy som bakgrundsprocess
    using var devProxy = StartDevProxy(mocksFile: "mocks.json");

    // Spawna CLI:n
    var cliPath = GetCliPath();
    using var cli = new Process { StartInfo = new ProcessStartInfo
    {
        FileName = "dotnet",
        Arguments = $"run --project {cliPath}",
        Environment = { ["API_BASE_URL"] = devProxy.Url },
        RedirectStandardOutput = true,
        UseShellExecute = false
    }};

    cli.Start();
    var output = cli.StandardOutput.ReadToEnd();
    cli.WaitForExit();

    Assert.Equal(0, cli.ExitCode);
    Assert.Contains("Education Levels", output);
    Assert.Contains("Master", output); // Svenska "master degree"
}
```

### Mocks.json-exempel

```json
[
  {
    "request": {
      "method": "GET",
      "path": "/api/v1/education-levels"
    },
    "response": {
      "status": 200,
      "body": "[{\"id\":\"1\",\"name\":\"Master\",\"country\":\"Sweden\"}]"
    }
  }
]
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
# 1. Installera verktyg
dotnet tool install -g Microsoft.devproxy
dotnet tool install -g WireMock.Net

# 2. Kör tester
dotnet test                              # UnitTests + IntegrationTests
dotnet test --project test/E2ETests      # E2ETests (kräver Dev Proxy)
```

---

_Skapad 2026-05-09 | Uppdaterad 2026-05-09_
