# Contributing to Plugwright

Thank you for your interest in Plugwright.

## Before you start

- The design and its rationale are in [`docs/design/specification.md`](docs/design/specification.md) (German). Read the section for the feature you want to work on.
- For larger changes, open an issue first, so we can agree on the approach.

## Build and test

You need the .NET 10 SDK.

```bash
dotnet build
dotnet test
```

Build the documentation:

```bash
dotnet tool restore
dotnet docfx docs/docfx.json --warningsAsErrors --serve
```

## Rules

- Every change ships with tests, documentation and, where it adds a feature, a runnable sample under `samples/`.
- Code snippets in the documentation are included from `samples/`, never pasted by hand.
- Every public type and member has XML documentation comments.
- Warnings are errors. Keep the public API small; everything else is `internal`.
- Code, comments, documentation and commit messages are in English.
- Tests use xUnit and the built-in `Assert` or Shouldly.
