---
paths:
{{paths}}
---

# JavaScript and TypeScript rules

Managed by Mariliz AI Engineering. Defaults for {{detail}}; a recorded project decision overrides them.

## Structure

- Keep business rules out of UI components: components render and orchestrate, services and domain modules decide.
- Respect the layering the repository already uses (for example app, features, shared). Do not import "upwards".
- Prefer named exports. Avoid barrel files that re-export everything; they hide dependencies and slow builds.

## Code

- TypeScript in `strict` mode. Do not use `any`; model the type or use `unknown` and narrow it.
- Validate data that crosses a boundary (network, storage, user input) at runtime, then use the validated type.
- Handle loading, empty and error states explicitly. Do not leave a failed request without feedback.
- Keep accessibility in the definition of done: semantic elements, labels, keyboard operation, visible focus.

## Dependencies

- Use the repository's package manager only, and commit its lockfile. Do not mix managers.
- Add a dependency only when the standard library or an existing dependency cannot do the job.

## Tests

- Test behavior through the public surface (rendered output, function results), not implementation details.
- Run lint, type check, tests and build with the commands listed in `mariliz-repo.md` before finishing a task.
