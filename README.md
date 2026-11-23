# Employee API

A learning project for building a REST API with ASP.NET Core and C#. The goal is to practice the fundamentals of designing HTTP endpoints, handling requests, persisting data, validating input, and testing API behavior.

## What this project does

The API manages employee records through an `EmployeesController`. Its endpoints support:

- Listing employees, with first and last name filters and pagination parameters.
- Retrieving an employee by ID.
- Creating, updating, and deleting employees.
- Returning standard HTTP outcomes such as `200 OK`, `201 Created`, `204 No Content`, `400 Bad Request`, and `404 Not Found`.

Employee data is stored with Entity Framework Core and SQLite. The project includes EF Core migrations and startup data migration/seeding. It also models benefits and employee-benefit relationships.

## What I am learning

This project gives me hands-on practice with:

- ASP.NET Core controllers, attribute routing, route and query parameters, and model binding.
- HTTP methods, status codes, request/response DTOs, and REST resource design.
- Dependency injection for controllers, database contexts, validators, and other services.
- Entity Framework Core, SQLite, database migrations, relationships, and asynchronous queries.
- Input validation with FluentValidation and an MVC action filter that returns validation errors.
- Filtering and pagination for collection endpoints.
- Logging, audit fields, and OpenAPI/Swagger documentation.
- Integration testing with xUnit, `WebApplicationFactory`, and an in-memory test server setup.

The project is intentionally a place to learn and improve these concepts incrementally. Some features, such as authorization and the employee benefits endpoints, are not currently enabled as active API routes.

## Projects and supporting material

- `EmployeeAPI/` — ASP.NET Core API implementation.
- `EmployeeAPI.test/` — API integration tests.
- `Notes/` — personal ASP.NET Core API reference notes.
- `skills/` — reusable learning notes and guidance for API topics.

## Run locally

You need the .NET SDK that supports the `net10.0` target framework.

```bash
dotnet run --project EmployeeAPI/EmployeeApi.csproj
```

In the Development environment, OpenAPI and Swagger UI are enabled. The API uses the SQLite connection configured in `EmployeeAPI/appsettings.json`.

## Run the tests

```bash
dotnet test EmployeeAPI.test/EmployeeAPI.test.csproj
```
