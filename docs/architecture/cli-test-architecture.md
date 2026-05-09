# Studywise CLI Arkitektur

## Status
**Datum:** 2026-05-09
**Typ:** Arkitekturdokument
**Projekt:** Studywise CLI

---

## 1. Solution-struktur

```
studywise-cli/
├── Studywise.Cli.sln
├── src/
│   └── Studywise.CLI/                   # CLI-applikationen (System.CommandLine)
└── test/
    ├── Studywise.CLI.UnitTests/    # Enhetstester
    ├── Studywise.CLI.IntegrationTests/ # Integrationstester (mockad HTTP)
    └── Studywise.CLI.E2ETests/    # E2E: spawnar faktisk CLI-process
```

> **Namnkonvention:** `Studywise.CLI.{TestType}` — produktnamn + testtyp. Inga dubbla "Tests".

---

## 2. Testlager: Integration vs CLI E2E

De två sista testlagren (IntegrationTests och E2ETests) är relaterade men fundamentalt olika:

### IntegrationTests

**Vad det är:** Command handlers körs i processtestet med **mockad HTTP** (via `HttpClient`-mock eller wiremock).

**Vad det testar:**
- Handlern-parsing av arguments
- Korrekt mapping av HTTP response → CLI output
- Felhantering (404, 500, timeout)
- Att rätt kommando anropar rätt endpoint

**Körs:** Med `dotnet test` i samma process som testprojektet — **ingen separat process spawnas**.

**Exempel:**
```csharp
[Fact]
public async Task ListEducationLevels_ReturnsFormattedTable()
{
    // Arrange: mockad HTTP response
    var handler = new ListEducationLevelsHandler(
        new MockHttpClient(jsonEducationLevels));
    
    // Act
    var result = await handler.HandleAsync(new ListEducationLevelsCommand());
    
    // Assert
    Assert.NotNull(result);
    Assert.Contains("Name", result.Output);
    Assert.Contains("Sweden", result.Output);
}
```

### E2ETests (E2E — separat process)

**Vad det är:** Bygger CLI:n och **spawnar den som en separat process**. Verifierar stdin/stdout/exit code.

**Vad det testar:**
- CLI:n startar utan crash
- `--help` visar korrekt usage
- Korrekt output-format för list-kommandon
- Exit codes för felhantering
- Help-text och kommando-struktur
- Argument-parsing i "skarpt läge"

**Körs:** `Process.Start()` i test — CLI:n kompileras och körs som `studywise` eller `dotnet studywise`.

**Ingen HTTP** — detta är ren smoke/sanity för CLI:ns entry point.

**Exempel:**
```csharp
[Fact]
public void Help_Command_ExitsWithZero()
{
    // Arrange
    var cliPath = GetCliBinPath(); // bin/Debug/net9.0/studywise
    
    using var process = new Process { StartInfo = new ProcessStartInfo
    {
        FileName = cliPath,
        Arguments = "--help",
        RedirectStandardOutput = true,
        RedirectStandardError = true,
        UseShellExecute = false
    }};
    
    // Act
    process.Start();
    process.WaitForExit();
    var stdout = process.StandardOutput.ReadToEnd();
    var exitCode = process.ExitCode;
    
    // Assert
    Assert.Equal(0, exitCode);
    Assert.Contains("Usage:", stdout);
    Assert.Contains("list", stdout); // list-kommandot finns
}
```

### Jämförelse

| Aspekt | IntegrationTests | E2ETests |
|--------|-----------------|----------|
| Process | Samma process som test | **Separat CLI-process** |
| HTTP | ✅ mockad | ❌ (ingen HTTP) |
| Speed | Snabb | Något långsammare |
| Vad det verifierar | Logik, parsing, mapping | **Faktiskt CLI-beteende** |
| Kan krascha CLI:n | Nej | Ja (shell-verifiering) |

### Varför separata projekt?

- **Olika beroenden:** IntegrationTests behöver `HttpClient`-mocks. E2ETests behöver bara `dotnet build` + process-start.
- **Olika miljökrav:** E2ETests behöver CLI:n byggd först — IntegrationTests behöver inte det.
- **Olika stabilitetsprofil:** En kraschad CLI-process påverkar inte IntegrationTests.
- **Olika testtid:** E2ETests är långsammare (process-spawn + build).

**Beslutet är fattat:** E2ETests är ett **separat projekt** vid sidan av IntegrationTests.

---

## 3. Beslut som behövs

| Fråga | Status | Kommentar |
|------|--------|-----------|
| Separata projekt? | ✅ **Ja** | UnitTests, IntegrationTests, E2ETests — alla egna |
| Namnkonvention? | ✅ **`Studywise.CLI.{TestType}`** | Produktnamn + testtyp, inga dubbla Tests |
| E2ETests kräver API-mock? | **Nej** | Help/Exit-code tester behöver ingen HTTP |

---

_Skapad 2026-05-09_
