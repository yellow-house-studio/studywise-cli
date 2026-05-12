# Developer Environment Setup

## Status
**Datum:** 2026-05-12 (uppdaterad)
**Typ:** Developer Guide
**Projekt:** Studywise CLI

---

## Prerequisites

- .NET 9 SDK
- Git

---

## Tools

### Dev Proxy

Dev Proxy is used for E2E testing to mock HTTP responses at the network level.

#### Installation

**Linux:**
```bash
bash -c "$(curl -sL https://aka.ms/devproxy/setup.sh)"
```

**macOS:**
```bash
brew tap dotnet/dev-proxy
brew install dev-proxy
```
When prompted to trust the certificate, press `y` to confirm.

**Windows:**
```bash
winget install DevProxy.DevProxy --silent
```
After installing on Windows, restart your terminal to refresh the PATH.

#### Verify Installation

```bash
devproxy --version
```

#### Usage for E2E Tests

Dev Proxy listens on port **8000** by default. Start with mock files:

```bash
devproxy --mocks-urls "path/to/mocks.json"
devproxy --detach  # Run in background
devproxy status    # Check running status
devproxy stop      # Stop background instance
devproxy logs     # View logs
```

**In E2E tests:** Set `STUDYWISE_API_BASE_URL=http://127.0.0.1:8000` so CLI routes requests through Dev Proxy.

---

### WireMock.Net

WireMock.Net is used for integration tests to mock HTTP within the test process.

This is included as a package dependency in `Studywise.CLI.IntegrationTests`.

---

## Running Tests

```bash
# Unit + Integration tests (no external dependencies)
dotnet test

# E2E tests (requires Dev Proxy running)
# Start Dev Proxy first:
devproxy --mocks-urls "test/Studywise.CLI.E2ETests/doctor-mocks.json" --detach

# Then run E2E tests:
dotnet test test/Studywise.CLI.E2ETests/Studywise.CLI.E2ETests.csproj

# Or run all tests (Dev Proxy must be running in background):
devproxy --detach && dotnet test && devproxy stop
```

---

_Skapad 2026-05-10 | Uppdaterad 2026-05-12_
