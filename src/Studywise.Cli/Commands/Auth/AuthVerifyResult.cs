namespace Studywise.Cli.Commands.Auth;

/// <summary>
/// The CLI-normalized result of a successful <c>studywise auth verify</c>.
/// </summary>
/// <param name="UserId">The identifier of the user the API key authenticates as.</param>
/// <param name="AuthMethod">The authentication scheme the API accepted, for example <c>ApiKey</c>.</param>
public sealed record AuthVerifyResult(string UserId, string AuthMethod);
