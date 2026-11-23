---
name: efcore-model-configuration
description: Configure or explain EF Core entity models with OnModelCreating, including keys, indexes, relationships, foreign keys, constraints, and migrations.
---

# EF Core model configuration

Help users configure or understand EF Core's model using `DbContext.OnModelCreating` and `ModelBuilder`. Preserve existing entity and database semantics unless a redesign is requested.

For this repository, use [ASP.NET Core API cheat sheet](../../Notes/ASP.NET%20Core%20API%20cheat%20sheet.md), especially its EmployeeAPI migrations and model-configuration section, and inspect `EmployeeAPI/AppDbContext.cs`, the entity classes, migrations, and model snapshot before proposing changes.

## Guidance

- Explain that `OnModelCreating` configures EF Core's model; it does not itself execute DDL or apply a migration. Model changes may require a generated migration that is reviewed and applied.
- Use Fluent API for keys, indexes, relationships, foreign keys, delete behavior, property constraints, and table/column mapping where conventions are insufficient or explicit configuration improves clarity.
- Distinguish a unique index from a primary key. A unique index enforces uniqueness but does not replace the entity's key.
- Check both sides of a relationship, navigation properties, FK property types/names, and existing conventions before adding explicit configuration. Use `.WithMany()` when there is no collection navigation; point it at the collection when one exists.
- Do not duplicate conventional configuration without a reason. Configure cascade/restrict behavior and constraint names deliberately when they affect data integrity or deletion behavior.
- Before changing a key or relationship, inspect its effects on entity identity, dependent rows, query code, and existing data. A migration that drops a table can lose data; review generated migration operations and rollback behavior.
- When a user provides an example that differs from the current model, explain the difference and adapt it. In this repository `EmployeeBenefit` has a surrogate `Id` plus a unique `(EmployeeId, BenefitId)` index; that is not a composite primary key.
- Keep the model snapshot and migrations consistent with `OnModelCreating`. Do not edit generated migration artifacts casually; use EF tooling when asked to generate migrations.
- When common audit metadata is requested, consider an abstract mapped base entity for shared fields such as UTC creation/modification timestamps and actor IDs. Confirm which entity types need the fields and remember inherited mapped properties change the schema and require migration review.
- Populate audit fields consistently at the save boundary (for example, a `SaveChanges` override or `SaveChangesInterceptor`) by inspecting `ChangeTracker` entries: initialize created fields for `Added`, update last-modified fields for `Modified`, preserve creation metadata, and account for detached updates that mark all properties modified.
- Keep actor lookup behind an application service such as `ICurrentUser`; avoid coupling the EF model or context directly to HTTP state. Distinguish current-row audit columns from an audit log that preserves every historical change.
- For new time-dependent code on modern .NET, prefer injected `TimeProvider` over ASP.NET Core authentication `ISystemClock`, which is obsolete starting with ASP.NET Core 8. Use a fake time provider in tests to cover boundary cases without real delays.
