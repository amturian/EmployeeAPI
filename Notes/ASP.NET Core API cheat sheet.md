# ASP.NET Core API Cheat Sheet

Quick notes for building and explaining ASP.NET Core APIs with minimal APIs and controllers.

## Quick navigation

- [Routes and API design](#routes-and-route-groups)
- [Controllers, binding, and responses](#controllers)
- [Validation, DI, and middleware](#error-handling-in-net-apis)
- [Authentication and authorization](#authentication-and-authorization)
- [EF Core and the EmployeeAPI project](#employeeapi-project-walkthrough)
- [Interview review](#interview-review)
- [API build checklist](#building-a-small-api-checklist)

## Routes and route groups

`MapGet`, `MapPost`, and similar methods register HTTP endpoints. Their delegates handle matching requests; they are endpoint handlers, not middleware.

Use `MapGroup` to give related endpoints a shared route prefix. The group keeps the full URLs together while each endpoint only declares its own suffix.

```csharp
var employeeRoutes = app.MapGroup("/employees");

employeeRoutes.MapGet("/", () => Results.Ok(employees));
employeeRoutes.MapGet("/{id:int}", ([FromRoute] int id) => ...);
employeeRoutes.MapPost("/", ([FromBody] Employee employee) => ...);
```

These endpoints respond at `GET /employees`, `GET /employees/{id}`, and `POST /employees`. The `:int` route constraint matches an integer ID.

## Subresources and API design

A **subresource** is a resource exposed in the context of a parent, such as an employee's benefits: `GET /employees/1/benefits`. Start with business ownership and client needs when designing the routes. A nested URL does not require nested database tables or a separate microservice.

### Choosing a representation

| Variant | Example | Good fit / tradeoff |
| --- | --- | --- |
| Nested collection and item | `/employees/1/benefits`, `/employees/1/benefits/7` | The benefit belongs to that employee and is normally accessed through them. Clients need the parent ID. |
| Independent resource | `/benefits/7` | The benefit has an independent identity/lifecycle and clients often access it directly. Parent access still needs authorization. |
| Filtered collection | `/benefits?employeeId=1` | Cross-employee search or reporting is important. Filtering does not grant access to other employees' records. |
| Embedded data | `/employees/1` includes a `benefits` array | Fewer requests when clients usually need both. Larger responses and repeated data when they do not. |
| Shared catalog plus assignment | `/benefit-plans/3` and `/employees/1/enrollments/7` | Many employees share a plan; the enrollment holds employee-specific cost, dates, or status. |

For a shared plan, deleting one employee's enrollment should not delete the plan for everyone. Association records are useful when a relationship has its own data. This is a modeling option, not a requirement to turn every database join into an endpoint. See [Google's resource-association guidance](https://google.aip.dev/124).

If nested and independent routes expose the same item, decide which URL is its canonical identity and keep permissions and representations consistent. Keep paths shallow and based on business resources. See [Microsoft's API design guidance](https://learn.microsoft.com/en-us/azure/architecture/best-practices/api-design).

### Ownership and identity decisions

Ask: Can the child exist without its parent? Can it move between parents? Is its ID unique globally or only within one parent? What happens when the parent is deleted—delete children, retain history, or reject deletion? These answers guide nesting, identity, and lifecycle rules.

For `/employees/1/benefits/7`, verify that benefit 7 actually belongs to employee 1. Query by both identifiers or check the relationship. Authorize the caller against the requested resources; including IDs in a URL provides no protection by itself. For nested creates, derive the parent ID from the route and reject conflicting body values. See [OWASP's object-level authorization guidance](https://api-security.owasp.org/editions/2023/en/0xa1-broken-object-level-authorization/).

### Operations and responses

| Request | Example contract |
| --- | --- |
| `GET /employees/1/benefits` | `200` with the collection; `[]` when the employee exists with no benefits. |
| `GET /employees/1/benefits/7` | `200` with the child DTO; `404` if the addressed child is absent. |
| `POST /employees/1/benefits` | Create an assignment; return `201` and a `Location` for the new item. |
| `PUT /employees/1/benefits/7` | Replace the addressed resource's writable representation. |
| `PATCH /employees/1/benefits/7` | Apply a partial update using a documented patch format. |
| `DELETE /employees/1/benefits/7` | Remove the assignment; `204` is a common successful response. |

A missing parent can return `404`; an empty collection under an existing parent is different. Document authorization-related `403`/`404` behavior consistently. Repeated PUT/DELETE requests should preserve their idempotent intended effect; POST retries can duplicate creations unless the API provides deduplication. Idempotency concerns effects, so repeated responses need not have identical status codes. See [HTTP semantics](https://www.rfc-editor.org/rfc/rfc9110.html).

### Implementation and growth

- In this API, `[HttpGet("{employeeId}/benefits")]` combines with the controller's `/employees` prefix. A separate benefits controller with `[Route("employees/{employeeId:int}/benefits")]` is another organizational option; controller layout does not determine storage layout.
- Return a dedicated benefit DTO when that is the endpoint contract. The current project exposes benefits through `/employees/{employeeId}/benefits`; the `Benefits` property on `GetEmployeeResponse` is currently commented out. Keep fields consistent if benefits are later embedded in the employee response too.
- If a child collection can grow large, add bounded pagination and query just the required fields. With a database, index parent lookups and avoid one extra query per employee when listing employees with benefits.
- Decide which changes must succeed together. A local database transaction can protect a parent-and-child change. Splitting ownership across services adds coordination and possible temporary inconsistency; a nested route alone is no reason to split services.
- If concurrent edits matter, consider version checks such as ETag/`If-Match` to detect stale updates. Define deletion/history and duplicate-assignment rules before implementing writes.
- In the current seed data, benefit `Id` and `EmployeeId` remain `0`. Before adding individual-child routes, assign benefit IDs and establish their employee relationship. Choose whether IDs are globally unique or parent-scoped.

Useful cases to verify: missing parent, existing parent with no children, child under the wrong parent, unauthorized access, invalid creation, duplicate retries, and stale updates.

Interview explanation: “I choose resource boundaries from ownership, lifecycle, and client access patterns. Then I design identities, permissions, payload size, and consistency rules around those boundaries.”

## Controllers

A controller is a class that groups related HTTP endpoints, called **actions**. Use controllers when you want endpoints organized into classes instead of mapping each route directly in `Program.cs`.

- API controller classes commonly end in `Controller` and inherit from `ControllerBase`. `Controller` adds support for returning views; `ControllerBase` is intended for APIs.
- `[ApiController]` is an **attribute** (not a directive). It enables API behaviors such as inferring where action parameters come from and automatically returning HTTP 400 when model binding or data-annotation validation fails.
- `[ApiController]` does not automatically run FluentValidation rules. In the current `EmployeeAPI`, a registered `FluentValidationFilter` runs validators for action arguments; older controller examples may call validation explicitly.
- `[Route("[controller]")]` sets the route prefix. The `[controller]` token uses the class name without the `Controller` suffix, so `EmployeesController` maps to `/employees`.
- HTTP attributes define each action's route and method: `[HttpGet]`, `[HttpGet("{id}")]`, `[HttpPost]`, and `[HttpPut("{id}")]`.
- Register controllers and map their routes in `Program.cs`:
  ```csharp
  builder.Services.AddControllers();
  app.MapControllers();
  ```
- Controllers can receive dependencies, such as `IRepository<Employee>`, through constructor injection. Return helpers such as `Ok(...)`, `NotFound()`, and `CreatedAtAction(...)` to choose the HTTP response.

In this project, `EmployeesController` inherits from `BaseController`, which supplies `[ApiController]` and `[Route("[controller]")]`. The controller's actions then handle requests under `/employees`.

## Binding delegate parameters

ASP.NET Core binds handler parameters from the incoming request. Add an attribute when you want to state the source explicitly:

- `[FromRoute]` reads a value from a route segment, such as `{id}`.
- `[FromQuery]` reads a value from the query string, such as `?active=true`.
- `[FromBody]` deserializes the request body, commonly JSON, into a parameter such as `Employee`.
- `[FromHeader]` reads a value from an HTTP header.

For example, `([FromRoute] int id)` binds the route ID, while `([FromBody] Employee employee)` binds the posted JSON. These attributes come from `Microsoft.AspNetCore.Mvc`.

Minimal APIs choose how to bind each handler parameter in this order:

1. Explicit binding attributes, such as `[FromRoute]` or `[FromBody]`.
2. Special types, such as `HttpContext` and `CancellationToken`.
3. Route values, then query-string values.
4. Services registered in dependency injection.
5. The request body.

## Returning API responses with `Results`

`Results` provides helpers for common HTTP responses from minimal API handlers:

- `Results.Ok(value)` returns HTTP 200 and a response body.
- `Results.NotFound()` returns HTTP 404.
- `Results.Created(uri, value)` returns HTTP 201, a location for the new resource, and its body.

Example:

```csharp
return Results.Created($"/employees/{employee.Id}", employee);
```

Use a response that matches the outcome: a missing employee should return `NotFound`, while a successfully created employee should return `Created`.

Controller actions use `ControllerBase` helpers for the same kinds of responses: `Ok(...)`, `NotFound()`, `BadRequest(...)`, and `CreatedAtAction(...)`. `CreatedAtAction` returns HTTP 201 and builds a `Location` URL for another action, commonly the GET-by-ID action.

Use request DTOs for incoming data and response DTOs for outgoing data. This lets the API choose which fields clients can send or see. For example, `GetEmployeeResponse` omits `SocialSecurityNumber`; returning an `Employee` directly can expose fields that the response DTO leaves out. In this project, GET uses the response DTO while POST returns the `Employee` entity, so check that the POST response does not expose sensitive fields.

The null-forgiving operator (`!`) only tells the compiler to suppress a nullable warning; it does not check that a value exists at runtime. If a request field is required, enforce that with validation too.

## Error handling in .NET APIs

`ProblemDetails` is a standard structure for describing an HTTP error response. It gives clients consistent fields—such as the status, a short title, and optional detail—to help them understand what went wrong.

Use `Results.Problem(...)` to return a ProblemDetails response from a minimal API handler:

```csharp
return Results.Problem(
    title: "Employee not found",
    statusCode: StatusCodes.Status404NotFound);
```

ASP.NET Core can also produce ProblemDetails responses through configured error-handling middleware, so unexpected errors can use the same response format.

`Validator` from `System.ComponentModel.DataAnnotations` can manually check an object against attributes such as `[Required]` and collect validation errors as `ValidationResult` objects:

```csharp
var errors = new List<ValidationResult>();
var isValid = Validator.TryValidateObject(
    request,
    new ValidationContext(request),
    errors,
    validateAllProperties: true);
```

`isValid` is `false` if validation failed; `errors` contains the messages and member names. This gathers the errors, but your handler still needs to decide how to return them, for example in a `ProblemDetails` response.

`System.ComponentModel.DataAnnotations.ValidationResult` and `FluentValidation.Results.ValidationResult` are different types with the same short name. Check the `using` statement when reading code that uses `ValidationResult`.

### FluentValidation

[FluentValidation](https://docs.fluentvalidation.net/en/latest/aspnet.html) is a separate library for writing validation rules in dedicated validator classes, using readable method chains:

```csharp
public class CreateEmployeeRequestValidator : AbstractValidator<CreateEmployeeRequest>
{
    public CreateEmployeeRequestValidator()
    {
        RuleFor(request => request.FirstName).NotEmpty();
        RuleFor(request => request.LastName).NotEmpty();
    }
}
```

Register validators with dependency injection, then either call `ValidateAsync(request)` explicitly or connect validation through a filter/pipeline. The current `EmployeeAPI` scans the API assembly with `builder.Services.AddValidatorsFromAssemblyContaining<Program>()` and runs validators through `FluentValidationFilter`; registering validators alone does not execute them. In a Minimal API, return errors with `Results.ValidationProblem(result.ToDictionary())`. In a controller, `ValidationProblem(...)` creates a standard validation-error response; `BadRequest(...)` can also be used when its body is already a `ValidationProblemDetails` object.

`BaseController.ValidateAsync<T>` is also present as a helper that looks up `IValidator<T>` from the current request's services. The current controller endpoints use the global action filter instead. A missing validator is an application registration/configuration problem when validation is expected.

## Dependency injection

Dependency injection (DI) supplies a class or endpoint with the services it needs. Register the mapping at startup, then request the interface through a constructor or handler parameter:

```csharp
builder.Services.AddSingleton<IRepository<Employee>, EmployeeRepository>();
```

The service type requested by the consumer must match the type registered with DI. Common lifetimes are:

- **Singleton**: one instance for the application's lifetime. The sample repository uses this lifetime, so its in-memory list is shared across requests.
- **Scoped**: one instance per request. Database contexts are commonly scoped.
- **Transient**: a new instance each time the service is requested.

An in-memory repository loses its data when the app stops. A mutable singleton list is useful for learning, but production storage needs persistence and safe concurrent access.

### Ways to receive dependencies

Injection style describes **where the service is supplied**. Lifetime describes **how long its instance lives**; any injection style can request a singleton, scoped, or transient service.

- **Constructor injection**: receive services when the class is created and store them in fields. Useful for dependencies used throughout the class. Your `EmployeesController` receives its repository and logger this way.
  ```csharp
  public EmployeesController(IRepository<Employee> repository)
  {
      _repository = repository;
  }
  ```
- **Action/method parameter injection**: receive a service in the controller action that needs it. `[FromServices]` explicitly selects DI as the source.
  ```csharp
  public IActionResult GetAll([FromServices] IRepository<Employee> repository)
      => Ok(repository.GetAll());
  ```
- **Minimal API handler parameters**: registered service types can be inferred as DI parameters; `[FromServices]` can make this explicit.
  ```csharp
  app.MapGet("/employees", (IRepository<Employee> repository) =>
      Results.Ok(repository.GetAll()));
  ```
- **Service lookup**: request a dependency manually from `IServiceProvider`, for example `HttpContext.RequestServices.GetService<IValidator<T>>()` in your base controller. This is commonly called the *service locator* approach. Dependencies are less visible than constructor parameters. `GetService<T>()` returns `null` when absent; `GetRequiredService<T>()` throws.

Property/setter injection exists as a general DI technique, but ASP.NET Core's default controller activation does not automatically populate arbitrary service properties. A C# primary constructor is another syntax for constructor injection. Keyed services, using `[FromKeyedServices("key")]`, allow choosing among separately registered implementations.

See [Microsoft's controller DI documentation](https://learn.microsoft.com/en-us/aspnet/core/mvc/controllers/dependency-injection?view=aspnetcore-10.0).

## Async and `await`

`async`/`await` lets an API action pause while asynchronous work—such as a database or HTTP call—is pending, freeing the request thread to handle other work. `Task<T>` represents a result that will be available later. `async` does not create a new thread or make CPU-heavy work parallel by itself.

Use asynchronous APIs all the way through the call chain when the underlying operation supports them, for example `await validator.ValidateAsync(request)` or an asynchronous repository/database method.

## Logging

Inject `ILogger<T>` into a controller. `T` supplies the logger category, for example `EmployeesController`. Levels run from least to most severe:

| Level | When to use it |
| --- | --- |
| Trace | Very detailed diagnostic steps. |
| Debug | Details useful while investigating behavior. |
| Information | Normal application events and progress. |
| Warning | An unexpected condition the application can handle. |
| Error | An operation or request failed. |
| Critical | A serious failure affecting the application's ability to run. |
| None | Configuration value that disables logging. |

The minimum level is configured through `Logging:LogLevel` in `appsettings.json` or environment-specific settings. With `Information` as the minimum, `Debug` and `Trace` messages are filtered out. Category-specific settings can override the default.

### Structured logging

A message template keeps values separate from the text so supporting logging providers can store and search fields such as `EmployeeId`:

```csharp
_logger.LogInformation("Updating employee with ID: {EmployeeId}", id);
```

Use named placeholders and pass their values as arguments. String interpolation (`$"Updating employee {id}"`) produces text without the same separate named fields. Pass an exception directly when logging a failure:

```csharp
_logger.LogError(ex, "Failed to update employee {EmployeeId}", id);
```

Avoid logging sensitive values such as Social Security numbers.

See [Microsoft's logging documentation](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/logging/?view=aspnetcore-10.0).

## Middleware and endpoint mapping

Middleware is registered in the HTTP request pipeline, usually with `app.Use...`, and can run before or after endpoint handling. Examples include HTTPS redirection and Swagger UI. `MapGet` and `MapPost` map endpoints that the pipeline can execute for matching requests.

### Typical MVC / Razor Pages pipeline

This example shows a common order for a web app that serves pages, static files, and authenticated endpoints:

```csharp
app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.MapRazorPages();
app.MapDefaultControllerRoute();
```

- `UseHttpsRedirection` redirects HTTP requests to HTTPS.
- `UseStaticFiles` serves files such as CSS, JavaScript, and images, typically from `wwwroot`. A matching file can complete the request immediately. Files served here are public, since this middleware runs before authorization.
- `UseRouting` selects the matching endpoint and makes its metadata available to later middleware.
- `UseAuthentication` identifies the user; `UseAuthorization` checks whether that user may access the selected endpoint. Keep routing, authentication, and authorization in that order.
- `MapRazorPages` registers Razor Pages endpoints. `MapDefaultControllerRoute` registers the conventional MVC route `{controller=Home}/{action=Index}/{id?}`. These calls map endpoints.

Requests pass through middleware in registration order; response processing unwinds in reverse order. Middleware can stop the chain by handling the request itself.

Use the parts the app needs and register their corresponding services first. MVC views commonly use `AddControllersWithViews`, Razor Pages use `AddRazorPages`, and authentication needs its services and scheme configured. An API using attribute-routed controllers, like this project, uses `AddControllers()` and `MapControllers()`. Modern `WebApplication` can add routing automatically; explicit `UseRouting()` lets you control its position. Exception-handling middleware is usually placed early so it can catch failures further down the pipeline.

See [Microsoft's middleware documentation](https://learn.microsoft.com/aspnet/core/fundamentals/middleware).

## Filters

Filters are reusable code that runs around stages of controller request handling. They help share validation, logging, or response behavior across actions. MVC filters run inside the selected controller endpoint; middleware wraps the broader HTTP pipeline.

| Filter | Purpose |
| --- | --- |
| Authorization | Checks access early. Prefer authorization policies and `[Authorize]` for normal access rules. |
| Resource | Wraps most of MVC processing, including model binding. |
| Action | Runs around the action method and can inspect bound arguments or its result. Useful for shared request validation. |
| Exception | Handles certain unhandled exceptions during MVC processing; its coverage is narrower than exception middleware. |
| Result | Runs around execution of the action result, such as response formatting. |

An asynchronous action filter implements `IAsyncActionFilter`:

```csharp
public async Task OnActionExecutionAsync(
    ActionExecutingContext context, ActionExecutionDelegate next)
{
    // Before the action and remaining action filters.
    await next();
    // After they finish.
}
```

A validation filter can set `context.Result` to a 400 response and return without calling `next()`. This skips the action when input is invalid. The tutorial's `FluentValidationFilter` uses this pattern to return `ValidationProblemDetails`.

Filters can apply globally through `AddControllers(options => options.Filters.Add<MyFilter>())`, or to selected controllers/actions through filter attributes. Service-based filters need appropriate DI registration. Minimal APIs use endpoint filters, such as `AddEndpointFilter`, for similar handler-level behavior.

See [Microsoft's filters documentation](https://learn.microsoft.com/en-us/aspnet/core/mvc/controllers/filters?view=aspnetcore-10.0).

## OpenAPI and Swagger

OpenAPI is the standard format that describes an API. Swagger is a family of tools that work with OpenAPI documents. Swagger UI is the interactive browser page for exploring and trying endpoints.

With `Swashbuckle.AspNetCore` installed and `AddSwaggerGen`, `UseSwagger`, and `UseSwaggerUI` configured in Development, open `/swagger` on the app's local URL. For example, `http://localhost:5152/swagger` for `EmployeeApi` when using its HTTP launch profile. The JSON document is usually at `/swagger/v1/swagger.json`.

ASP.NET Core's built-in OpenAPI support uses `AddOpenApi()` and `MapOpenApi()`; its default JSON endpoint is `/openapi/v1.json`. Swagger UI and the OpenAPI JSON document are related tools/endpoints, with different setup and URLs.

### Response documentation attributes

`[ProducesResponseType]` describes an action's possible HTTP responses for OpenAPI/Swagger:

```csharp
[ProducesResponseType(typeof(GetEmployeeResponse), StatusCodes.Status200OK)]
[ProducesResponseType(StatusCodes.Status404NotFound)]
[ProducesResponseType(StatusCodes.Status500InternalServerError)]
```

- **200**: the response body is described as a `GetEmployeeResponse`.
- **404 / 500**: these responses are documented; these declarations don't explicitly specify their body types.
- `typeof(GetEmployeeResponse)` supplies the C# type's metadata, and `StatusCodes` provides readable names for HTTP status numbers.

These attributes document the contract. The action still needs to return the appropriate status and body through `Ok(...)`, `NotFound()`, etc. Keep the declarations consistent with actual behavior. For collections, document `IEnumerable<GetEmployeeResponse>`; when errors use ProblemDetails, explicitly document `ProblemDetails` or `ValidationProblemDetails` as appropriate.

See [Microsoft's OpenAPI metadata documentation](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/openapi/include-metadata?view=aspnetcore-10.0).

### XML documentation comments

C# documentation comments start with `///`. IDEs can show them in IntelliSense, and OpenAPI tools can use them to describe endpoints:

```csharp
/// <summary>Gets an employee by ID.</summary>
/// <param name="id">The employee's identifier.</param>
/// <returns>The employee response.</returns>
/// <response code="200">Employee found.</response>
/// <response code="404">Employee not found.</response>
```

Place them above the action and its attributes. `<summary>` describes its purpose; `<param>` describes a parameter; `<returns>` describes the result; `<response>` describes a status; `<remarks>` adds detail or examples. Parameter names must match the method.

In the `.csproj` file, add these properties inside a `<PropertyGroup>`:

```xml
<PropertyGroup>
  <GenerateDocumentationFile>true</GenerateDocumentationFile>
  <NoWarn>$(NoWarn);1591</NoWarn>
</PropertyGroup>
```

`GenerateDocumentationFile` enables XML documentation output during builds. Warning **CS1591** reports public types or members without XML comments; `NoWarn` suppresses that warning. `$(NoWarn);1591` preserves any existing suppressed warnings and adds 1591. Suppressing the warning is optional.

Then configure Swashbuckle in `Program.cs` to include the generated XML file:

```csharp
builder.Services.AddSwaggerGen(options =>
{
    options.IncludeXmlComments(
        Path.Combine(AppContext.BaseDirectory, "EmployeeApi.xml"));
});
```

Replace `EmployeeApi.xml` with the actual generated filename for another project. By default it matches the assembly name; you can derive it with `$"{typeof(Program).Assembly.GetName().Name}.xml"`. `AppContext.BaseDirectory` points to the application's base directory, where the build outputs the file. Comments describe behavior; the action implements it.

See [Microsoft's Swashbuckle XML documentation guide](https://learn.microsoft.com/en-us/aspnet/core/tutorials/getting-started-with-swashbuckle?view=aspnetcore-8.0#xml-comments).

## Unit tests

- `IClassFixture<WebApplicationFactory<Program>>` tells xUnit: “Create one `WebApplicationFactory<Program>` for this test class, share it with the tests, then dispose of it when the class's tests are finished.”
- `WebApplicationFactory<Program>` starts the API in a test server in memory, so tests can send it HTTP requests without manually running the app.
- A class fixture is shared state. If tests share a singleton in-memory repository, data created in one test can affect another; seed and reset data deliberately or give tests isolated app state.

## LINQ

LINQ (Language Integrated Query) provides methods for working with sequences, such as lists and arrays. With the in-memory `employees` list in `Program.cs`, these methods operate on objects in memory; they do not access a database.

- `Select` maps each item to a new value or shape:
  ```csharp
  employees.Select(employee => new GetEmployeeResponse { FirstName = employee.FirstName });
  ```
- `Where` filters items that match a condition: `employees.Where(employee => employee.City == "Bucharest")`.
- `OrderBy` sorts items: `employees.OrderBy(employee => employee.LastName)`.
- `Any` checks whether at least one item matches: `employees.Any(employee => employee.Id == 1)`.
- `SingleOrDefault` returns the only matching item, or `null` if there is none; it throws if multiple items match. The API uses it to find an employee by ID.
- `Max` returns the greatest value, used by the API to assign the next employee ID.

LINQ can also query databases through providers such as Entity Framework Core. In that case, supported operations can be translated into SQL.

### `IEnumerable<T>` and `IQueryable<T>`

Both describe sequences you can iterate with `foreach`. `IQueryable<T>` also implements `IEnumerable<T>`.

| Type | How LINQ works | Common example |
| --- | --- | --- |
| `IEnumerable<T>` | LINQ-to-Objects operators run .NET functions on items as they are enumerated. | Filtering a `List<Employee>`. |
| `IQueryable<T>` | Query operators build an expression tree for a provider to interpret or translate. | EF Core translating supported filters into SQL. |

`IEnumerable` does not guarantee that all data is already in memory: it can generate or stream items. `IQueryable` is not limited to databases; its behavior depends on the provider. Calling `AsQueryable()` on a list does not turn it into a database query. See [IQueryable documentation](https://learn.microsoft.com/en-us/dotnet/api/system.linq.iqueryable?view=net-10.0).

With a configured EF Core context, compose the query before fetching the results:

```csharp
IQueryable<Employee> query = db.Employees
    .Where(employee => employee.City == "Bucharest");

var matches = await query.ToListAsync();
```

EF can translate the filter into SQL, so only matching rows are returned. Fetching with `ToListAsync()` first and filtering afterward transfers the unfiltered results. `AsEnumerable()` changes subsequent LINQ operations to .NET processing; it does not execute or buffer the query itself. `IQueryable` alone does not guarantee a small result—use filters, projections, and pagination deliberately.

### Deferred execution and materialization

`Where` and `Select` usually defer their work until the sequence is consumed. This applies to both LINQ-to-Objects and provider-backed queries.

- `foreach` consumes items and can stream them.
- `ToList()` / `ToArray()` consume the sequence and store its results in memory; EF provides `ToListAsync()` / `ToArrayAsync()` for async queries.
- Scalar operations such as `Count()` or `First()` also trigger work; an EF provider can execute them in the database.
- Enumerating again can repeat the work or execute another database query. A deferred sequence is not automatically a cached snapshot.
- Deferred execution does not always mean one-item-at-a-time processing: LINQ-to-Objects `OrderBy` must buffer and sort before returning its first item.

Keep the EF DbContext alive while executing its queries. See [deferred versus lazy evaluation](https://learn.microsoft.com/dotnet/standard/linq/deferred-execution-lazy-evaluation).

### Expression trees

An expression tree represents code as inspectable data. The same lambda syntax can describe executable behavior or an expression tree, depending on its target type:

```csharp
Func<Employee, bool> predicate = employee => employee.Id == 1;
Expression<Func<Employee, bool>> expression = employee => employee.Id == 1;
// Expression<T> requires using System.Linq.Expressions.
```

The delegate can be called directly. The expression describes the parameter, property access, and comparison, so a query provider can inspect them and generate an equivalent database operation. Translation support depends on the provider; arbitrary C# methods may not translate.

### `yield return`

`yield return` creates an iterator that produces values as the caller requests them:

```csharp
IEnumerable<int> GetExampleIds()
{
    yield return 1;
    yield return 2;
}
```

Calling `GetExampleIds()` creates the sequence. Its body runs during enumeration, pauses at each `yield return`, and resumes when another item is requested. The compiler generates a state machine to remember progress. This can avoid building a full list upfront. `yield break` ends iteration. See [the C# yield reference](https://learn.microsoft.com/dotnet/csharp/language-reference/statements/yield).

## Entity Framework Core introduction

**Entity Framework Core (EF Core)** is an object-relational mapper (ORM): it maps .NET objects to database data so you can query and save using C#. A database provider connects it to a database engine, such as SQL Server, PostgreSQL, or SQLite. For relational databases, EF Core generates SQL for supported queries and changes; raw SQL is still available when needed. See the [EF Core overview](https://learn.microsoft.com/en-us/ef/core/).

| Concept | Meaning |
| --- | --- |
| Entity | A mapped C# object, such as an `Employee`; an entity commonly corresponds to a database row. |
| `DbContext` | A session for querying entities, tracking changes, and saving them. It holds the EF model and coordinates database operations. |
| `DbSet<Employee>` | A queryable collection of employee entities, commonly mapped to a table. One employee is an entity; a DbSet represents the collection. Declaring it does not load the entire table. |
| Change tracking | EF remembers tracked entities and their changes so it can generate updates when saved. |
| `SaveChangesAsync()` | Persists tracked additions, changes, and deletions to the database. |

### LINQ and saving changes

Assuming `db` is a configured context with an `Employees` DbSet:

```csharp
var employee = await db.Employees
    .FirstOrDefaultAsync(employee => employee.Id == 1);

if (employee != null)
{
    employee.City = "Bucharest";
    await db.SaveChangesAsync();
}
```

With normal tracking enabled, EF tracks the retrieved employee and detects the changed city. `Where`/`Select` build a query; operations such as `ToListAsync` or `FirstOrDefaultAsync` execute it. Supported LINQ expressions become database queries; arbitrary C# methods cannot always be translated.

### Code first and database first

- **Code first**: define entity classes and model configuration, then use migrations to evolve the database schema. A migration records schema changes; generating it and applying it to the database are separate steps.
- **Database first**: begin with an existing schema and reverse-engineer (scaffold) entity classes and a DbContext. Decide how future schema changes will be reflected in that code.

These approaches describe which model is the source of truth; they both use EF Core for queries and saving. See [schema-management approaches](https://learn.microsoft.com/en-us/ef/core/managing-schemas/).

### Remember for Web APIs

`AddDbContext` normally registers a context with a scoped lifetime—one per request. A DbContext is not thread-safe; await each operation before starting another on the same instance. See [DbContext lifetime guidance](https://learn.microsoft.com/en-us/ef/core/dbcontext-configuration/).

In an earlier in-memory API stage, EF Core can replace repository storage. The current `EmployeeAPI` uses `AppDbContext` directly in its controller. EF Core handles database access; request validation, authorization, and response DTO design remain API concerns.

## Repository pattern (earlier in-memory API stage)

The repository pattern keeps data access operations behind a repository interface. API endpoints call methods such as `GetAll`, `GetById`, `Create`, and `Update` instead of working with the storage directly.

- `IRepository<Employee>` defines the operations the API can use.
- `EmployeeRepository` implements those operations in the earlier example, storing employees in an in-memory list. It is not the persistence approach used by the current `EmployeeAPI` project.
- Dependency injection connects the interface to its implementation:
  ```csharp
  builder.Services.AddSingleton<IRepository<Employee>, EmployeeRepository>();
  ```
- Handlers request `IRepository<Employee>`; they do not need to know how the repository stores the data. The implementation can later change, for example, from a list to a database, while keeping the same interface.
- The repository is the source of employee data. A separate list in `Program.cs` does not seed it unless the repository is given those records.
- Be aware of object references: this repository returns the actual objects in its list. The controller mutates an employee before calling `Update`, so those changes appear in the list. A database-backed or detached repository may require `Update` to copy/save every changed field explicitly.
- This repository's `GetEmployeeBySsn` is an unfinished stub that throws `NotImplementedException`; implement or remove stubs before relying on them.

## Extension methods

An extension method is a static helper that C# lets you call with dot syntax as if it belonged to an existing type. It adds a method-like convenience, not a property, and does not change the original type.

In this project's `Extensions.cs`, `this` on the first parameter makes `ToModelStateDictionary` callable on a FluentValidation `ValidationResult`:

```csharp
validationResults.ToModelStateDictionary();
```

The method is still defined in the separate `Extensions` class and returns a `ModelStateDictionary`. The calling file needs a `using` for that class's namespace.

## EmployeeAPI project walkthrough

This section connects the general API concepts above to the whole `EmployeeAPI` application. The project demonstrates a controller-based REST API, operation-specific request and response objects, FluentValidation, SQLite persistence with EF Core, filtering and pagination, and integration tests.

### Request lifecycle

```text
HTTP request
  -> ASP.NET Core routing and model binding
  -> EmployeesController action
  -> FluentValidationFilter validates action request objects
  -> AppDbContext queries or changes SQLite data
  -> controller maps data into a response DTO / HTTP result
  -> JSON response
```

`Program.cs` registers services and builds the middleware pipeline. `BaseController` supplies `[ApiController]`, the `[controller]` route prefix, and JSON response metadata to `EmployeesController`. Constructor injection supplies `AppDbContext` and `ILogger<EmployeesController>`.

### Routes in this project

| Method | Route | Behavior |
| --- | --- | --- |
| GET | `/employees` | List employees; accepts optional name filters and page values. |
| GET | `/employees/{id}` | Return one employee or `404 Not Found`. |
| POST | `/employees` | Validate and create an employee; returns `201 Created`. |
| PUT | `/employees/{id}` | Validate and update employee contact/address fields; returns `200 OK` or `404`. |
| DELETE | `/employees/{id}` | Delete an employee; returns `204 No Content` or `404`. |
| GET | `/employees/{employeeId}/benefits` | Return the employee's benefit assignments and effective costs. |

The controller is the HTTP layer; it is not the database model. `CreatedAtAction` on create returns a `Location` that points to the GET-by-ID action. The current POST action returns the EF `Employee` entity, unlike the GET actions, which map to `GetEmployeeResponse`; returning a DTO from POST would keep the HTTP boundary consistent and avoid exposing entity fields such as `SocialSecurityNumber`.

### Request and response DTOs

DTO means **data transfer object**. Use request DTOs to define client input and response DTOs to define client-visible output. Entities represent data and relationships in persistence. Keeping these responsibilities separate:

- Avoids returning large EF entity graphs when clients only need a few fields.
- Prevents JSON cycles, such as `Employee -> EmployeeBenefit -> Employee`.
- Prevents accidental exposure of persistence fields and reduces over-posting risk.
- Keeps the API contract from changing every time the database model changes.
- Gives each operation its own input contract and validation rules.

The current request types are:

| Type | Purpose |
| --- | --- |
| `CreateEmployeeRequest` | Fields a client can supply to create an employee. |
| `UpdateEmployeeRequest` | Writable address and contact fields for an update. |
| `GetAllEmployeesRequest` | Query parameters for filtering and paging. |

`GetEmployeeResponse` and `GetEmployeeResponseEmployeeBenefit` define response shapes. The controller maps EF entities into these DTOs. A DTO prevents over-posting only when the action actually binds to it; avoid binding an entity directly for client writes.

### Validation and Problem Details

`Program.cs` calls `AddValidatorsFromAssemblyContaining<Program>()` to register FluentValidation validators and installs `FluentValidationFilter` as an MVC action filter. The filter finds the validator for each non-null action argument, runs it, and short-circuits invalid requests with a 400 `ValidationProblemDetails` response. This is the project's custom validation path; registering FluentValidation alone would not automatically execute these rules.

Examples in the project:

- Create validation requires first name, last name, and social security number.
- List validation requires positive page values and limits `RecordsPerPage` to 100 when supplied. Omitted nullable values use the action defaults.
- Update validation checks the address against the existing employee's address.

`AddProblemDetails()` registers Problem Details services. Validation errors are explicitly created by the filter. Do not assume that registering Problem Details alone also installs global exception-handling middleware.

### SQLite, `DbContext`, migrations, and seeding

`AppDbContext` is EF Core's session with the database. It exposes `DbSet<Employee>`, `DbSet<Benefit>`, and `DbSet<EmployeeBenefit>`. It combines query access with unit-of-work behavior: tracked changes are saved together when `SaveChanges` or `SaveChangesAsync` is called.

The application reads `DefaultConnection` from `appsettings.json`, which points to SQLite. At startup, `SeedData.MigrateAndSeed` calls `Database.Migrate()` and inserts initial employees and benefit records when the corresponding tables are empty. Migrations preserve schema changes over time:

- `Up` applies a schema change.
- `Down` reverses that migration where possible.
- The model snapshot records EF Core's view of the latest model for future migration comparisons.

Typical commands, run from the solution/project context with the EF tooling installed:

```bash
dotnet ef migrations add DescribeTheChange --project EmployeeAPI
dotnet ef database update --project EmployeeAPI
```

Review generated migration operations before applying them, especially drops or table rebuilds that may lose data. `OnModelCreating(ModelBuilder)` configures EF's model; it does not itself apply SQL. The current `AppDbContext` adds a unique index on `(EmployeeId, BenefitId)`. A unique index is not the same as a composite primary key: `EmployeeBenefit` still has its own `Id` key.

Current model configuration:

```csharp
modelBuilder.Entity<EmployeeBenefit>()
    .HasIndex(eb => new { eb.EmployeeId, eb.BenefitId })
    .IsUnique();
```

`EmployeeBenefit` has `EmployeeId` and `BenefitId` foreign-key properties plus `Employee` and `Benefit` navigation properties. EF conventions can infer the relationships. If explicit configuration is needed, use `.HasOne(...).WithMany().HasForeignKey(...)`; use `.WithMany(e => e.EmployeeBenefits)` when the principal has a collection navigation. `HasKey(eb => new { ... })` would instead make the pair the composite primary key and would require changing the current `Id` key/model and creating a migration.

### Benefits model

```text
Employee 1 ---- * EmployeeBenefit * ---- 1 Benefit
```

`EmployeeBenefit` is an explicit join entity because the relationship itself stores `CostToEmployee`. It has foreign keys and navigation properties to both principal entities. The benefits endpoint loads assignments and benefits using `Include`/`ThenInclude`. It returns the employee-specific cost when available, otherwise `Benefit.BaseCost`.

This is a common modeling pattern: when a many-to-many relationship has attributes of its own (cost, enrollment date, status), represent the association as an entity.

### EF Core change tracking

EF tracks entities returned by tracking queries and records entity states: `Added`, `Unchanged`, `Modified`, `Deleted`, or `Detached`. `SaveChanges` detects changes and persists the relevant inserts, updates, and deletes; it does not update every tracked entity just because it is tracked.

The project globally configures `QueryTrackingBehavior.NoTracking` in `Program.cs`. That reduces overhead for read-only queries. `UpdateEmployee` opts back into tracking with `.AsTracking()`, changes the loaded entity, and saves. `CreateEmployee` uses `Add` (state `Added`); `DeleteEmployee` uses `Remove` (state `Deleted`). Explicitly setting `EntityState.Modified` is not needed when the same context loaded and tracks the entity, and it can mark more fields modified than necessary.

Interview summary: “I use no-tracking queries for read paths and tracked entities for updates. EF Core's change tracker detects the changed properties and `SaveChangesAsync` persists them.”

### Auditing fields with a base entity

When many entities need the same record metadata, a shared abstract base entity can define common audit properties:

```csharp
public abstract class AuditableEntity
{
    public DateTimeOffset CreatedAtUtc { get; set; }
    public string? CreatedBy { get; set; }
    public DateTimeOffset? LastModifiedAtUtc { get; set; }
    public string? LastModifiedBy { get; set; }
}

public class Employee : AuditableEntity
{
    public int Id { get; set; }
    public required string FirstName { get; set; }
    public required string LastName { get; set; }
}
```

The base class avoids repeating the same properties on every entity. Use it when many records share the same audit semantics; keep it out of entities that do not need those fields. EF Core maps inherited public properties, so adding the base class changes the EF model and normally requires a migration.

Set the fields centrally when EF saves changes, commonly by overriding `SaveChanges`/`SaveChangesAsync` or by using a `SaveChangesInterceptor`:

1. For entries in `EntityState.Added`, set `CreatedAtUtc` and `CreatedBy`.
2. For entries in `EntityState.Modified`, set `LastModifiedAtUtc` and `LastModifiedBy`.
3. Do not replace the original `CreatedAtUtc` or `CreatedBy` when modifying a record. If detached updates mark every property modified, explicitly protect the created fields from being overwritten.
4. Leave delete handling to the application's retention/audit policy; a deleted row no longer stores its own metadata.

Use UTC timestamps, preferably `DateTimeOffset` for an unambiguous instant. Populate `CreatedBy` and `LastModifiedBy` from an application abstraction such as `ICurrentUser` that reads the authenticated user ID. Keeping this behind an interface makes auditing independent of `HttpContext` and easier to test. Handle background jobs or anonymous system actions with a documented system identity or nullable user field.

These four columns describe the current record's creation and most recent update. They do **not** preserve every prior value. If the requirement is a full history of what changed and when, use a separate audit/history table or another temporal/versioning strategy.

### Time-dependent code and `ISystemClock`

Time-dependent code includes expiration checks, token validity, scheduled work, and audit timestamps. Calling `DateTime.UtcNow` directly makes tests depend on wall-clock time and can force fragile waits. Injecting a clock lets production use real UTC time while tests control the current instant deterministically.

`ISystemClock` was an ASP.NET Core abstraction for reading the current UTC time, historically used by authentication components and useful for testing expiration behavior. In ASP.NET Core 8 and later, `Microsoft.AspNetCore.Authentication.ISystemClock` is obsolete; the framework moved to `System.TimeProvider`. For new .NET 8+ code, inject `TimeProvider` and call `GetUtcNow()`. See [the ASP.NET Core breaking change](https://learn.microsoft.com/aspnet/core/breaking-changes/8/isystemclock-obsolete) and [the .NET TimeProvider overview](https://learn.microsoft.com/dotnet/standard/datetime/timeprovider-overview).

Register the production provider once:

```csharp
builder.Services.AddSingleton(TimeProvider.System);
```

Then inject `TimeProvider` where time is needed. Tests can supply a controllable fake from the `Microsoft.Extensions.TimeProvider.Testing` package, set a fixed UTC instant, invoke the operation, and assert the resulting expiration or audit value without sleeping. Advancing fake time also makes it straightforward to test just-before/at/after-expiry cases. Keep timestamp creation in one place, such as the context or save interceptor, so all audited entities use the same instant during one save.

The current EmployeeAPI does not yet define audit base classes or audit columns. The properties and clock examples here describe a natural extension; adding them would require model changes and a migration. `TimeProvider` would also be preferable to introducing the obsolete ASP.NET Core `ISystemClock` in this .NET 10 project.

### Filtering and pagination

`GET /employees` accepts `Page`, `RecordsPerPage`, `FirstNameContains`, and `LastNameContains`. Missing page values default to page 1 and 100 rows. For page `p` and size `s`, offset paging uses `Skip((p - 1) * s).Take(s)`.

Build the query in this order:

1. Start with `IQueryable<Employee>`.
2. Add optional `Where` filters.
3. Add deterministic `OrderBy` (for example, by `Id`).
4. Apply `Skip` and `Take`.
5. Execute asynchronously with `ToArrayAsync` or `ToListAsync`.

`IQueryable` lets EF Core translate composed filters and paging to SQL; materialization executes the query. In the current `GetAllEmployees` action, `Skip`/`Take` are added before the optional `Where` calls, so filtering applies to the page slice. Reorder it as above to page over the filtered set. The current query also lacks `OrderBy`, so row order and page boundaries are not guaranteed to be stable.

Offset paging is straightforward, but deep offsets can be expensive and inserts/deletes can shift page boundaries. For large, changing data sets, keyset pagination can use a stable key and a “start after” cursor. Add total-count or page metadata when the client needs it; it is not returned by the current endpoint.

`Contains` is a substring match. Whether it is case-sensitive depends on the database provider and collation. The 100-record cap limits response size; keep that cap, its validator, and API documentation consistent. The current endpoint returns only the employee array, with no total count or page metadata.

### Hosting and API tooling

`Program.cs` registers controllers, EF Core, validators, `IHttpContextAccessor`, Problem Details, OpenAPI, and Swagger. `app.MapControllers()` exposes the attribute-routed actions. OpenAPI/Swagger UI are enabled only in Development. HTTPS redirection is configured. `EmployeeAPI/EmployeeApi.http` is a scratch file for sending requests during development.

The middleware pipeline is code ordered: middleware can act before and after the next component. Routing selects endpoints; endpoint mapping makes controller actions available. In a production application, configure centralized exception handling, authentication, and authorization as required by its needs. The current project has not implemented authentication or authorization.

### Authentication and authorization

**Authentication** establishes who the caller is. **Authorization** decides what that authenticated caller may access or do. An authenticated user can still be forbidden from a particular operation or resource.

#### JWT bearer basics

A JSON Web Token (JWT) is a compact token often used as a bearer access token. It has a header, a claims payload, and a signature. The signature helps the API verify integrity and trusted issuance; a normal signed JWT is **not encrypted**, so do not put secrets in its payload. Clients send it in the HTTP header:

```http
Authorization: Bearer <access-token>
```

The API must validate the token's signature, issuer, audience, and expiration. Invalid, expired, or unacceptable tokens should not authenticate the caller. Follow the identity provider's supported token flow rather than inventing a token protocol. See [Microsoft's JWT bearer configuration guidance](https://learn.microsoft.com/aspnet/core/security/authentication/configure-jwt-bearer-authentication).

Typical ASP.NET Core setup registers an authentication scheme and authorization services, configures JWT bearer validation, then ensures authentication runs before authorization in the request pipeline:

```csharp
builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options => { /* configure trusted issuer/audience */ });
builder.Services.AddAuthorization();

app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
```

Protect a controller or action with `[Authorize]`; `[AllowAnonymous]` can be used for an intentionally public endpoint. A request without acceptable authentication commonly receives `401 Unauthorized`. An authenticated caller who lacks permission commonly receives `403 Forbidden`.

#### Claims, roles, and policies

Claims are facts about an identity, such as a subject/user ID or department. Roles are one kind of authorization input, often used for broad groups such as `Administrator`. Policies express requirements and are usually a better fit for business rules that combine roles, claims, or custom checks:

```csharp
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("EngineeringAdmin", policy =>
        policy.RequireRole("Administrator")
              .RequireClaim("Department", "Engineering"));
});
```

Apply it with `[Authorize(Policy = "EngineeringAdmin")]`. Policies and role checks still need trusted claims from a validated identity; never let an unauthenticated request choose its own role or permissions. See [role-based authorization](https://learn.microsoft.com/aspnet/core/security/authorization/roles) and [claims-based authorization](https://learn.microsoft.com/aspnet/core/security/authorization/claims).

#### Resource-level authorization

Endpoint-level `[Authorize]` checks that a caller is authenticated or satisfies a general policy. It does not prove the caller may access a particular employee record. For routes such as `/employees/{id}`, load the resource and check ownership/tenant membership or another business permission before returning or modifying it. A client-controlled ID is not authorization. This prevents insecure direct object reference (IDOR)/broken object-level authorization.

#### Choose a credential flow for the client

- For a browser-based application, evaluate a secure server-managed cookie/session or a standards-based OpenID Connect/OAuth setup. Cookies can be `HttpOnly`, `Secure`, and `SameSite`, but because browsers send them automatically, consider CSRF defenses. Avoid putting long-lived credentials in browser-accessible storage.
- For native, command-line, or service-to-service clients, OAuth/OIDC bearer access tokens from a trusted identity provider are common. An API key can fit a narrowly scoped machine integration, provided it can be rotated/revoked and its permissions are limited; it is not a user login system.
- Use a mature identity provider or ASP.NET Core Identity for user accounts and token issuance. Keep signing keys and credentials in a secret store/environment configuration, rotate them, and use short-lived access tokens with an appropriate refresh/revocation design.

#### The course's deliberately unsafe token endpoint

The `4-Z-final` sample includes `SecurityController` and `HttpContextExtensions.GenerateJwt`. The endpoint accepts a caller-supplied username and role and signs a token valid for 365 days; the sample key is in configuration. That means an arbitrary caller can mint a token claiming any role. The lesson explicitly presents this as a demonstration of ASP.NET Core security features, not reusable authentication code. In the final sample, `[Authorize]` on `EmployeesController` demonstrates protecting routes, while the token endpoint undermines that protection by letting anyone choose their identity and role.

Do not reuse that token-generation endpoint, long expiration, or sample signing key. A real API should issue tokens only after authenticating credentials through a trusted identity system and should validate tokens using the issuer's metadata or correctly configured signing/issuer/audience/lifetime validation.

The current `EmployeeAPI/Program.cs` does not register JWT bearer authentication or authorization, and its controller does not enforce `[Authorize]`; the copied/course security example is not evidence that the current API is secured.

#### Security test cases

For protected routes, integration tests should cover: no token (`401`), valid identity with allowed access, valid identity lacking a role/policy (`403`), expired or invalid-signature token (`401`), and access to another user's or tenant's resource being denied. Also test intended public routes explicitly. Avoid tests that accept caller-provided role claims from an untrusted token issuer.

### Integration tests

`EmployeeAPI.test` uses xUnit and `WebApplicationFactory<Program>` to run the real ASP.NET Core pipeline in a test host. `CustomWebApplicationFactory` replaces the app database registration with an open in-memory SQLite connection. Unlike EF Core's non-relational InMemory provider, SQLite exercises relational behavior and migrations.

The tests make HTTP requests and check successful list/detail/create/update/delete paths, filtering, validation errors, and not-found responses. One update test also reads the database to verify the saved value. When adding features, cover both success and failure cases and the relevant boundary conditions.

#### Database test choices

| Test database | What it gives you | Main limitation |
| --- | --- | --- |
| EF Core InMemory provider | Fast, simple test double for limited cases. | It is not relational; query, transaction, raw SQL, and constraint behavior can differ substantially. EF Core docs discourage it for testing relational application behavior. |
| SQLite in-memory | A real relational database with quick setup and no persistent file. This is what `CustomWebApplicationFactory` currently uses. | It tests SQLite behavior, not another production provider's SQL dialect, collation, types, or provider-specific features. |
| Target database in Testcontainers | A disposable real database engine close to production, such as PostgreSQL in a Docker container. Exercises the actual provider and schema behavior. | Requires Docker and more setup/startup time; tests still need controlled data and cleanup. |

This is a trade-off ladder, not a rule that every project must use containers. EF Core's testing guidance recommends having coverage against the actual production database system; SQLite is a practical relational alternative when its differences are understood. See [Choosing a testing strategy](https://learn.microsoft.com/ef/core/testing/choosing-a-testing-strategy).

#### What Testcontainers does

Testcontainers for .NET lets a test start a disposable Docker container for a dependency such as PostgreSQL, use it during the test run, and stop/remove it during cleanup. An xUnit fixture can share the container startup cost across tests in a class or collection. Testcontainers manages the container lifecycle; it does not automatically isolate rows, reset a database, apply migrations, or seed your data. See the [Testcontainers for .NET PostgreSQL module](https://dotnet.testcontainers.org/modules/postgres/) and [xUnit integration](https://dotnet.testcontainers.org/test_frameworks/xunit_net/).

To adapt this project to PostgreSQL Testcontainers:

1. Ensure a Docker daemon is available to the test process.
2. Add the PostgreSQL EF Core provider and the Testcontainers PostgreSQL module to the appropriate projects.
3. Start the PostgreSQL container in an xUnit fixture before the API test host is created.
4. In `CustomWebApplicationFactory`, replace the app's SQLite registration with `UseNpgsql` and the container's connection string.
5. Generate/apply migrations for PostgreSQL. EF migrations can contain provider-specific operations; do not assume SQLite migrations are automatically portable. The course's final version replaced the SQLite provider and generated a new migration set.
6. Seed known test data and ensure each test gets isolated state, then dispose the fixture/container.

#### What the provider switch can reveal

A test that passes with SQLite can fail with PostgreSQL because providers differ in SQL translation, constraints, data types, collation, and transaction behavior. Treat the failure as evidence to inspect the query, migration, seed logic, and test data—not as proof that the new provider is wrong.

The lesson describes two useful gotchas: tests that each insert the same `John Doe` can observe duplicate rows when they share a database, and employee-benefit seed relationships may not be persisted after changing tracking behavior. Make tests independent with per-test data/reset/cleanup. For seed code, verify the actual join rows were saved; with global `NoTracking`, do not assume that assigning navigation properties on detached employees will persist the relationship. Load entities with tracking or add `EmployeeBenefit` rows explicitly and verify with a query.

This matters in the current project: `CustomWebApplicationFactory` is an xUnit class fixture, so its in-memory SQLite connection is reused across tests in the class. The create test adds another employee named John Doe, while the filter test expects exactly one John. Tests that mutate shared state need isolation or unique data. The app also sets global no-tracking behavior, so check that the seed routine's employee-benefit assignments are tracked or explicitly added.

Interview summary: “SQLite in-memory gives fast relational integration tests, but it does not reproduce PostgreSQL behavior. I use Testcontainers for a smaller set of integration tests against the actual production provider, and isolate/reset data so tests remain repeatable.”

### Where the project code lives

| Path | Responsibility |
| --- | --- |
| `EmployeeAPI/Program.cs` | Service registration, pipeline, OpenAPI/Swagger, and startup database initialization. |
| `EmployeeAPI/Employees/EmployeesController.cs` | HTTP routes, query/write operations, and entity-to-response mapping. |
| `EmployeeAPI/Employees/*Request.cs` | Operation-specific request DTOs and validators. |
| `EmployeeAPI/Employees/GetEmployeeResponse.cs` | Response DTOs. |
| `EmployeeAPI/Employee.cs` | EF Core entities for employees, benefits, and benefit assignments. |
| `EmployeeAPI/AppDbContext.cs` | EF Core sets and model configuration. |
| `EmployeeAPI/SeedData.cs` | Migration application and sample data. |
| `EmployeeAPI/Migrations/` | Generated schema history and model snapshot. |
| `EmployeeAPI/FluentValidationFilter.cs` | Validation execution and 400 Problem Details response. |
| `EmployeeAPI/Extensions.cs` | Validation-result to model-state helper. |
| `EmployeeAPI.test/CustomWebApplicationFactory.cs` | Test-host database replacement with in-memory SQLite. |
| `EmployeeAPI.test/UnitTest1.cs` | HTTP integration tests. |

## Interview review

Use these prompts to practice explaining design choices rather than memorizing definitions.

### How would you describe this API's architecture?

“ASP.NET Core routes HTTP requests to controller actions. Request DTOs define input, FluentValidation checks it through an action filter, the controller uses an injected EF Core `DbContext` to query or update SQLite, then maps entities to response DTOs and returns an appropriate status code.”

### Why use request and response DTOs instead of entities?

“Entities model persistence and may include navigation properties, internal fields, and cycles. DTOs control what clients can send and receive, avoid over-posting and accidental exposure, reduce payload size, and decouple the API contract from the database schema.”

### What is the `DbContext` and change tracker?

“`DbContext` represents a unit of work and a database session. Its change tracker records entity state and detects changes. `SaveChangesAsync` persists added, modified, or deleted entities. I use `AsNoTracking` for read-only queries and tracking for entities I will update.”

### What is a migration?

“A migration is a versioned set of schema changes generated from the EF model. `Up` applies changes and `Down` attempts to reverse them. I inspect generated operations and apply migrations to move the database schema forward.”

### What is the difference between `IQueryable` and `IEnumerable` here?

“An `IQueryable` represents a query EF Core can compose and translate to SQL. `Where`, ordering, and paging can run in the database before results are materialized. After `ToArrayAsync`, I have in-memory objects; additional LINQ then runs in the application.”

### How do you make pagination correct?

“Filter first, order by a stable key, then apply `Skip` and `Take`. Validate positive page numbers and cap page size. For deep pages or frequently changing data, consider cursor/keyset pagination.”

## Building a small API checklist

1. Define resources, routes, ownership, and expected status codes.
2. Create request DTOs, response DTOs, and persistence entities for their separate purposes.
3. Register controllers, dependencies, database context, and validation.
4. Implement endpoint behavior and map between DTOs and entities.
5. Validate input and return consistent error responses.
6. Configure database schema, migrations, and seed/test data where needed.
7. Add authorization before exposing protected resources.
8. Test the HTTP behavior, persistence effects, validation failures, and not-found cases.
9. Document routes and request/response schemas with OpenAPI.
