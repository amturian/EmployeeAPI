---
name: filtering-pagination
description: Implement, explain, or review query filtering and pagination in ASP.NET Core APIs using EF Core. Use when working on query parameters, dynamic filters, page sizing, offset paging, or keyset paging.
---

# Filtering and pagination for ASP.NET Core APIs

Help implement or explain API list endpoints that accept filters and pagination. Preserve the project's existing request, validation, query, and response conventions unless the task asks for a redesign.

When working in this repository, use [ASP.NET Core API cheat sheet](../../Notes/ASP.NET%20Core%20API%20cheat%20sheet.md), especially its EmployeeAPI filtering and pagination section, and inspect the request, controller, and validation code for current behavior.

## Guidance

- Bind query string values to a request/query DTO with `[FromQuery]`.
- Keep filtering in the database query (`IQueryable`) so EF Core can translate it; execute asynchronously only after the filters and page selection are composed.
- Decide whether the endpoint is read-only or will modify queried entities. Read-only entity queries can use `AsNoTracking()` to avoid change-tracker overhead; keep tracking when edits will be saved, or explicitly use `AsTracking()` if defaults are no-tracking.
- Explain change tracking accurately: `SaveChanges` detects changes and persists relevant `Added`, `Modified`, or `Deleted` entities; merely tracking an entity does not mean EF updates it. Explicitly setting `EntityState.Modified` is usually unnecessary for an entity loaded and modified by the same context and may mark all properties modified.
- Treat omitted nullable page values deliberately. The Employee API defaults `Page` to 1 and `RecordsPerPage` to 100 in the action.
- Validate page numbers as positive and page sizes as positive with a clear upper bound. Keep validation messages, tests, and documented limits aligned.
- Apply filters before pagination so the requested page is drawn from the filtered result set. Prefer nonblank checks for optional text filters.
- Apply a deterministic sort before `Skip`/`Take`; without ordering, offset pagination does not promise stable page membership.
- Explain that `Skip((page - 1) * pageSize)` and `Take(pageSize)` implement 1-based offset paging. Note its performance and concurrent-change tradeoffs when those matter.
- Consider keyset pagination for deep pages or rapidly changing/large datasets. Use a stable sort key and a cursor contract that fits the API.
- Check whether clients need total counts or other page metadata; don't add these fields unless requested or needed by existing client behavior.
- Be precise about provider-dependent behavior such as case sensitivity of `string.Contains`.
- When changing behavior, update or recommend endpoint tests for filter composition, defaults, page boundaries, validation limits, and deterministic ordering as appropriate. Do not run tests unless the user asks for verification.
