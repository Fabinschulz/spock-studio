---
paths:
{{paths}}
---

# .NET rules

Managed by Mariliz AI Engineering. Defaults for {{detail}}; a recorded project decision overrides them.

## Structure

- Keep the dependency direction inward: Domain does not reference EF Core, ASP.NET Core or HTTP types; Application
  depends on Domain; Infrastructure and the host depend on Application.
- Register services through dependency injection at the composition root. Do not resolve services with a static
  locator.
- Use `async`/`await` end to end with a `CancellationToken` on I/O. Do not block on tasks (`.Result`, `.Wait()`).

## Code

- Enable nullable reference types and treat new warnings as defects.
- Prefer immutable records for messages and value objects. Validate invariants in constructors or factory methods.
- Return a result type or throw a specific exception for expected failures; do not use exceptions for normal flow.
- Keep controllers and endpoints thin: map the request, call one use case, map the response.

## Data

- Schema changes go through migrations that are reviewed and idempotent. Never edit applied migrations.
- Queries that return collections have a bound (paging or a limit). Avoid N+1 queries.

## Tests

- Unit tests for domain and application logic; integration tests for persistence and HTTP with a real or in-memory
  provider that matches production behavior closely enough to catch mapping errors.
- Run the build and the tests with the commands listed in `mariliz-repo.md` before finishing a task.
