# Shared API contracts

TypeScript definitions in this folder mirror `src/backend/Shora.Contracts` (C# records).

When adding or changing an API request/response:

1. Update the C# record in `Shora.Contracts`.
2. Update the matching TypeScript interface here (same property names; camelCase in TS, PascalCase in C# — JSON serialization uses camelCase by default).
3. Import from `@contracts/*` in the Angular app.

This keeps the frontend and backend aligned without code generation for MVP.

`ProblemDetails` in `common.ts` documents the RFC 7807 error JSON shape (ASP.NET framework type, not a C# Contracts record). `error-codes.ts` mirrors `Shora.Application.Common.ErrorCodes`.
