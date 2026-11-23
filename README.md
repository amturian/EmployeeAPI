<div align="center">

# Employee API

### Learning the fundamentals of building APIs with ASP.NET Core

![.NET 10](https://img.shields.io/badge/.NET-10-512BD4?logo=dotnet&logoColor=white)
![ASP.NET Core](https://img.shields.io/badge/ASP.NET_Core-Web_API-512BD4?logo=dotnet&logoColor=white)
![Entity Framework Core](https://img.shields.io/badge/Entity_Framework_Core-ORM-6B3FA0)
![SQLite](https://img.shields.io/badge/Database-SQLite-003B57?logo=sqlite&logoColor=white)

</div>

---

## About

This is a hands-on learning project for practicing the building blocks of a REST API with C# and ASP.NET Core. It focuses on how HTTP requests flow through an application, how data is validated and stored, and how API behavior can be tested.

## What the API can do

| Operation | Description |
| --- | --- |
| **List employees** | Supports first-name and last-name filters, plus pagination parameters. |
| **Get an employee** | Retrieves one employee by ID. |
| **Create an employee** | Validates input and returns a link to the new resource. |
| **Update an employee** | Changes employee details and records audit information. |
| **Delete an employee** | Removes an employee and returns an empty success response. |

The API uses Entity Framework Core with SQLite. Database migrations and startup seeding are included, along with models for benefits and employee-benefit relationships.

## What I’m learning

- **HTTP and REST:** routes, model binding, request and response DTOs, methods, and status codes.
- **ASP.NET Core:** controllers, dependency injection, filters, and the request pipeline.
- **Data access:** EF Core, SQLite, relationships, migrations, and asynchronous queries.
- **Validation:** FluentValidation and consistent validation error responses.
- **API usability:** filtering, pagination, logging, audit fields, and OpenAPI/Swagger docs.
- **Testing:** xUnit integration tests using `WebApplicationFactory` and an in-memory test server.

> This project is evolving as I learn. Authorization and employee benefits routes are not currently active API features.

## Repository guide

| Folder | Contents |
| --- | --- |
| [`EmployeeAPI/`](EmployeeAPI/) | ASP.NET Core API and data layer |
| [`EmployeeAPI.test/`](EmployeeAPI.test/) | API integration tests |
| [`Notes/`](Notes/) | Personal ASP.NET Core API reference notes |
| [`skills/`](skills/) | Reusable learning guides for API topics |

## Get started

**Prerequisite:** .NET SDK compatible with the `net10.0` target framework.

Start the API:

```bash
dotnet run --project EmployeeAPI/EmployeeApi.csproj
```

In the Development environment, OpenAPI and Swagger UI are enabled. The API uses the SQLite connection configured in `EmployeeAPI/appsettings.json`.

Run the tests:

```bash
dotnet test EmployeeAPI.test/EmployeeAPI.test.csproj
```

---

<div align="center">

*Built to learn, one API concept at a time.*

</div>
