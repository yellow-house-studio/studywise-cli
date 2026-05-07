# Studywise CLI

A command-line interface for Studywise, built for agents (Robert, Lilly) and end users.

## Installation

### For Agents (Automated)

Download the latest release from GitHub:

```bash
curl -L https://github.com/yellow-house-studio/studywise-cli/releases/latest/download/studywise-linux-x64.zip -o studywise.zip
unzip studywise.zip
./studywise --help
```

Or install as a .NET tool:

```bash
dotnet tool install --global studywise
```

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
studywise words list
```

### For Humans (Future)

Interactive login via Auth0 device flow:

```bash
studywise auth login
```

## Commands

- `studywise words` - Manage word lists
- `studywise progress` - View study progress  
- `studywise practice` - Practice with flashcards
- `studywise auth` - Authentication commands

Run `studywise --help` for all available commands.

## Development

### Prerequisites

- .NET 9.0 SDK
- Git

### Building

```bash
dotnet build Studywise.Cli.sln
```

### Testing

```bash
dotnet test Studywise.Cli.sln
```

### Project Structure

```
studywise-cli/
├── src/Studywise.Cli/     # Main application
│   ├── Commands/          # CLI commands
│   ├── Auth/              # Authentication providers
│   └── Configuration/     # Configuration options
├── test/                  # Test projects
└── .github/workflows/     # CI/CD
```

## License

MIT
