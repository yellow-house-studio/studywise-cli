# Plan: CLI Education Level List

## What
CLI command `studywise education-levels list --country <iso>` that calls existing API endpoint `GET /api/v1/education-levels?countryCode={iso}`.

## Why
Allow operators to discover education levels for a specific country, supporting CLI-based workflows for managing education data.

## Acceptance Criteria (Given/When/Then)
- Given no --country flag, When user runs the command, Then validation error is shown
- Given --country SWE, When user runs the command with valid auth, Then returns education levels from API with 200
- Given invalid country code, When user runs the command, Then validation error is shown

## Verification
- Automated: dotnet test covers validation and API integration
- Manual: studywise education-levels list --country SWE

## Boundaries
- No API changes
- No new endpoint
- No global/unbounded education-level list

## Dependencies
- Existing API endpoint GET /api/v1/education-levels?countryCode={iso} must exist