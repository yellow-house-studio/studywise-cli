# Studywise CLI

A command-line interface for Studywise, built for agents (Robert, Lilly) and end users.

## Installation

### For Agents (Automated)

No GitHub release has been published yet. The release workflow
(`.github/workflows/ci-production.yml`) builds a self-contained binary
(`studywise-linux-x64.zip`) and a NuGet package, and attaches both to a
GitHub Release — but the first release has not run.

Once one exists:

```bash
curl -L https://github.com/yellow-house-studio/studywise-cli/releases/latest/download/studywise-linux-x64.zip -o studywise.zip
unzip studywise.zip
./studywise --help
```

Installing as a .NET tool (`dotnet tool install --global studywise`) is not
available yet: the package is not currently built as a .NET tool package.

### From Source

```bash
git clone https://github.com/yellow-house-studio/studywise-cli.git
cd studywise-cli
dotnet build
dotnet run --project src/Studywise.Cli/Studywise.Cli.csproj -- --help
```

## Authentication

### For Agents

Set the `STUDYWISE_API_KEY` environment variable:

```bash
export STUDYWISE_API_KEY=sk_live_xxx
studywise doctor --check auth-verify
```

`studywise doctor --check auth-verify` confirms that a key is configured and
that the API accepts it, and reports which user the key authenticates as. The
key can also be set in `~/.config/studywise/config.json` — see
[docs/auth.md](docs/auth.md).

### For Humans (Not Yet Implemented)

Interactive login via Auth0 device flow (`studywise auth login`) is planned
but does not exist in any build today.

## Commands

The CLI currently ships one command:

- `studywise doctor` — run the diagnostic checks (`config`, `api-key`,
  `connection`, `auth-verify`)

Planned, and tracked in the [issue tracker](https://github.com/yellow-house-studio/studywise-cli/issues):

- `studywise auth verify` — confirm the configured API key authenticates
- `studywise education-level list` — list education levels for a country

Run `studywise --help` for the commands available in your build.

## Development

### Prerequisites

- .NET 10.0 SDK
- Git

### Building

```bash
dotnet build Studywise.Cli.sln
```

### Testing

```bash
dotnet test Studywise.Cli.sln
```

The local gate that CI mirrors is `./scripts/verify.sh --scope=fast` — see
[docs/testing/testing-strategy.md](docs/testing/testing-strategy.md).

## API Models

The CLI depends on `YellowHouseStudio.Studywise.Sdk`, the typed HTTP client
published from the [studywise-api repo](https://github.com/yellow-house-studio/studywise-api).
It is pinned in `Directory.Packages.props`.

The SDK owns the API wire types, so the CLI declares no API DTOs of its own.
See [docs/design/cli-design.md](docs/design/cli-design.md) § 7 (SDK adoption)
and [docs/architecture/sdk-consumption.md](docs/architecture/sdk-consumption.md)
for how to restore it and how to authenticate against the private feed.

## Project Structure

```
studywise-cli/
├── src/Studywise.Cli/     # Main application (commands, configuration, diagnostics)
├── test/                  # Test projects (unit + integration)
└── .github/workflows/     # CI/CD
```

## License

MIT
