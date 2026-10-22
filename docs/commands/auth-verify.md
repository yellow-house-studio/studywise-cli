# `studywise auth verify`

Verifies that the configured API key authenticates against the Studywise API and
reports which user it authenticates as.

```
studywise auth verify [--json]
```

## Options

| Option | Description |
|---|---|
| `--json` | Emit the result as a structured record instead of the human line. |

## Output

### Success (stdout)

One human-readable line:

```
Auth verify: OK — connected as users/abc-123 (ApiKey)
```

`unknown` is substituted for either field when the API omits it.

### Success (`--json`, stdout)

```json
{
  "userId": "users/abc-123",
  "authMethod": "ApiKey"
}
```

### Stable field set

| Field | Type | Description |
|---|---|---|
| `userId` | string | The identifier of the user the API key authenticates as. |
| `authMethod` | string | The authentication scheme the API accepted, for example `ApiKey`. |

Adding a field is non-breaking. Removing or renaming a field, or switching its
JSON type, is breaking.

## Errors

Failures write to **stderr**. With `--json`, the human line is followed by a
structured envelope on the same stream.

### Human line

```
studywise auth verify: <cause> (<hint>)
```

| Cause | Hint |
|---|---|
| `no API key is configured` | `set STUDYWISE_API_KEY` |
| `HTTP 401 Unauthorized: the API key was rejected` | `check the key and try again` |
| `HTTP 403 Forbidden: the API key may not perform this request` | `check the key's permissions` |
| `HTTP 404 Not Found: the auth verify endpoint was not found` | `check the API base URL` |
| `HTTP 429 Too Many Requests: the API is rate limiting this key` | `slow down and try again` |
| `HTTP 5xx <status>: the API returned an error` | `try again later` |
| `HTTP 4xx <status>: the API rejected the request` | `check the command's arguments` |
| `connection to <baseUrl> failed: <underlying error message>` | `check the network and the API base URL` |
| `timed out after 30s waiting on <baseUrl>` | `try again` |

### Structured envelope (`--json`)

```json
{
  "exitCode": 1,
  "error": {
    "kind": "unauthorized",
    "message": "HTTP 401 Unauthorized: the API key was rejected (check the key and try again)",
    "request": {
      "method": "GET",
      "path": "/api/v1/auth/verify"
    },
    "details": {
      "statusCode": 401
    }
  }
}
```

`kind` is one of `validation`, `conflict`, `unauthorized`, `notFound`,
`forbidden`, `rateLimit`, `server`, `network`, `timeout`, `usage`. The set is a
contract: adding a kind is non-breaking, renaming one is breaking.

`details` is command-specific. For this command it carries `statusCode` when
the API answered with an HTTP error, and is `null` otherwise.

### Exit codes

| Code | Meaning |
|---|---|
| `0` | The API key authenticates. |
| `1` | Operational failure — the key was rejected, the API failed, the network failed, or the request timed out. |
| `2` | Usage error — no API key is configured. |

## Notes

- The API key is read from `STUDYWISE_API_KEY`, falling back to the `apiKey`
  field of `~/.config/studywise/config.json`. See
  [docs/auth.md](../auth.md).
- The request is made by the SDK's `AuthClient`, which sends the
  `X-Studywise-Api-Key` header. The CLI does not follow redirects for it, so the
  key is not forwarded to a redirect target.
- `studywise doctor --check auth-verify` runs the same check and reports it as
  one line among the other diagnostics. This command is the standalone,
  scriptable form of it.
