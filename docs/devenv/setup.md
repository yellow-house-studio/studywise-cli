# Developer Environment Setup

## Status
**Datum:** 2026-05-10
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

#### macOS

```bash
brew tap dotnet/dev-proxy
brew install dev-proxy
```

When prompted to trust the certificate, press `y` to confirm.

#### Linux

```bash
bash -c "$(curl -sL https://aka.ms/devproxy/setup.sh)"
```

#### Windows

```bash
winget install DevProxy.DevProxy --silent
```

After installing on Windows, restart your terminal to refresh the PATH.

#### Verify Installation

```bash
devproxy --version
```

#### Usage for E2E Tests

Start Dev Proxy with mock files:

```bash
devproxy --mocks-urls "path/to/mocks.json"
```

Dev Proxy listens on `http://localhost:8000` by default.

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
devproxy --mocks-urls "test/Studywise.CLI.E2ETests/doctor-mocks.json"

# Then run E2E tests:
dotnet test test/Studywise.CLI.E2ETests/Studywise.CLI.E2ETests.csproj
```