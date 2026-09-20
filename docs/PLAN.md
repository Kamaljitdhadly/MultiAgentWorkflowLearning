# PLAN.md — Todo API (Learning Project)

## 1. Solution / Project Structure

Target framework for all projects: **net10.0**

```
MultiAgentWorkflowLearning/
  TodoApi.sln
  docs/
    PLAN.md
  src/
    TodoApi.Domain/
      TodoApi.Domain.csproj      (class library)
      TodoItem.cs
      ITodoService.cs
      TodoService.cs
    TodoApi.Api/
      TodoApi.Api.csproj         (ASP.NET Core Web API, minimal API)
      Program.cs
      appsettings.json
      appsettings.Development.json
      Properties/
        launchSettings.json
  tests/
    TodoApi.Domain.Tests/
      TodoApi.Domain.Tests.csproj (xUnit)
      TodoServiceTests.cs
```

**Style choice: Minimal API** (not controllers). Justification: the surface area is 5 endpoints over one resource — a single `Program.cs` with `app.MapGet/MapPost/MapPut/MapDelete` is more transparent for a learning pipeline than controller boilerplate, and keeps the Web API project as a thin host over `TodoApi.Domain`.

Project references: `TodoApi.Api` → `TodoApi.Domain`; `TodoApi.Domain.Tests` → `TodoApi.Domain`. The Api project is **not** referenced by the test project (unit tests target the domain/service layer only — see section 6).

## 2. Domain Model & Service Interface

`TodoApi.Domain/TodoItem.cs`:
```csharp
public class TodoItem
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public bool IsComplete { get; set; }
    public DateTime CreatedAt { get; set; }
}
```

`TodoApi.Domain/ITodoService.cs`:
```csharp
public interface ITodoService
{
    IReadOnlyList<TodoItem> GetAll();
    TodoItem? GetById(int id);
    TodoItem Create(string title);
    bool Update(int id, string title, bool isComplete); // false = not found
    bool Delete(int id);                                 // false = not found
}
```

`TodoService` implementation notes:
- Backing store: `ConcurrentDictionary<int, TodoItem>`.
- Id generation: private field `int _nextId = 0` incremented via `Interlocked.Increment(ref _nextId)` inside `Create`.
- `Create` sets `CreatedAt = DateTime.UtcNow` and `IsComplete = false` server-side — caller cannot set these.
- Register as a **singleton** in DI (must be, since it's the single in-memory store for the app's lifetime).

## 3. Endpoints

Base route: `/api/todos`. Request DTOs live in `TodoApi.Api` (e.g. as `record` types near `Program.cs` or in a `Dtos.cs` file): `CreateTodoRequest(string Title)`, `UpdateTodoRequest(string Title, bool IsComplete)`. Responses return `TodoItem` directly (no separate response DTO — keep it minimal).

| Route | Verb | Request Body | Success | Failure |
|---|---|---|---|---|
| `/api/todos` | GET | — | 200, `TodoItem[]` | — |
| `/api/todos/{id:int}` | GET | — | 200, `TodoItem` | 404 if not found |
| `/api/todos` | POST | `CreateTodoRequest` | 201, `TodoItem`, `Location: /api/todos/{id}` | 400 if `Title` is null/empty/whitespace |
| `/api/todos/{id:int}` | PUT | `UpdateTodoRequest` | 200, updated `TodoItem` | 404 if not found; 400 if `Title` empty/whitespace |
| `/api/todos/{id:int}` | DELETE | — | 204 No Content | 404 if not found |

400 responses should use `Results.ValidationProblem(...)` or `Results.BadRequest(new { error = "Title is required." })` — pick one style and use it consistently across POST and PUT.

## 4. Build Order (for Code agent)

1. `dotnet new sln -n TodoApi` (in repo root).
2. `dotnet new classlib -n TodoApi.Domain -o src/TodoApi.Domain -f net10.0` — delete the template's default `Class1.cs`.
3. `dotnet new web -n TodoApi.Api -o src/TodoApi.Api -f net10.0` (the **empty** ASP.NET Core template — top-level `Program.cs`, no controllers, no WeatherForecast scaffolding to clean up).
4. `dotnet new xunit -n TodoApi.Domain.Tests -o tests/TodoApi.Domain.Tests -f net10.0` — delete the template's default `UnitTest1.cs`.
5. `dotnet sln TodoApi.sln add src/TodoApi.Domain/TodoApi.Domain.csproj src/TodoApi.Api/TodoApi.Api.csproj tests/TodoApi.Domain.Tests/TodoApi.Domain.Tests.csproj`.
6. `dotnet add src/TodoApi.Api reference src/TodoApi.Domain`.
7. `dotnet add tests/TodoApi.Domain.Tests reference src/TodoApi.Domain`.
8. `dotnet add src/TodoApi.Api package Swashbuckle.AspNetCore`.
9. Implement `TodoItem.cs`, `ITodoService.cs`, `TodoService.cs` in `TodoApi.Domain`.
10. Implement `Program.cs` in `TodoApi.Api`:
    - `builder.Services.AddSingleton<ITodoService, TodoService>();`
    - `builder.Services.AddEndpointsApiExplorer(); builder.Services.AddSwaggerGen();`
    - `if (app.Environment.IsDevelopment()) { app.UseSwagger(); app.UseSwaggerUI(); }`
    - Map the 5 endpoints per section 3, using a route group: `var todos = app.MapGroup("/api/todos");`
    - Add `public partial class Program { }` at the bottom of the file (harmless top-level-statements marker) so a future `WebApplicationFactory<Program>` integration test can reference it if the Test agent wants one.
11. Write `TodoServiceTests.cs` per section 6.
12. `dotnet build TodoApi.sln` then `dotnet test TodoApi.sln` — both must pass before handing off.
13. Manually verify `dotnet run --project src/TodoApi.Api` serves Swagger UI at `/swagger`.

## 5. Review Agent — Specific Checks

- **Thread safety**: `TodoService` uses `ConcurrentDictionary` + `Interlocked.Increment` for ids — no plain `Dictionary` or unguarded `List<T>`, no non-atomic "read count then add" id logic.
- **Server-owned fields**: `Id`, `CreatedAt`, and initial `IsComplete` are never taken from the request DTO on `POST` — confirm `CreateTodoRequest` has no `Id`/`CreatedAt` fields that leak through.
- **Status codes**: POST returns `201` with a `Location` header (not `200`/`204`); DELETE returns `204` (not `200`); GET/PUT/DELETE on unknown id all return `404`, not `400` or an unhandled exception.
- **Validation edge cases**: whitespace-only title (`"   "`) must be rejected, not just `null`/`""` — check the implementation trims before checking.
- **Route casing/shape**: exact route is `/api/todos` (lowercase, plural) and `{id:int}` route constraint is used so a non-numeric id returns 404, not a 500.
- **Update semantics**: PUT replaces both `Title` and `IsComplete`; verify it doesn't silently ignore `IsComplete` from the request.
- **DI lifetime**: `ITodoService` is registered `Singleton`, not `Scoped`/`Transient` (Transient/Scoped would silently reset state per-request and lose data).
- **Nullability**: `GetById` returning `null` is correctly mapped to `Results.NotFound()` in `Program.cs`, no `NullReferenceException` risk.
- **Project reference direction**: `TodoApi.Api` → `TodoApi.Domain` only; no circular or reversed references; test project doesn't accidentally pull in ASP.NET Core packages it doesn't need.
- **Swagger reachable**: confirm `/swagger` actually renders and lists all 5 operations with correct request/response schemas.

## 6. Test Agent — Cases to Cover

All in `TodoApi.Domain.Tests/TodoServiceTests.cs`, against `ITodoService`/`TodoService` directly (construct a fresh instance per test — no shared static state):

1. `GetAll_ReturnsEmpty_WhenNoTodosCreated`
2. `Create_ReturnsTodoWithGeneratedId_AndIsCompleteFalse`
3. `Create_SetsCreatedAtCloseToUtcNow` (assert within a small tolerance, e.g. a few seconds)
4. `Create_MultipleCalls_ProduceUniqueSequentialIds`
5. `GetById_ReturnsMatchingTodo_WhenExists`
6. `GetById_ReturnsNull_WhenIdDoesNotExist`
7. `Update_ChangesTitleAndIsComplete_ReturnsTrue_WhenExists`
8. `Update_ReturnsFalse_WhenIdDoesNotExist` (and does not throw)
9. `Delete_RemovesItem_ReturnsTrue_WhenExists` (follow-up `GetById` returns null)
10. `Delete_ReturnsFalse_WhenIdDoesNotExist`
11. `Create_CalledConcurrently_NeverProducesDuplicateIds` (spin up N `Task.Run(() => service.Create(...))` in parallel, assert all resulting ids are distinct — validates thread safety from section 5)

Optional stretch (only if a reference from the test project to `TodoApi.Api` is added, using `Microsoft.AspNetCore.Mvc.Testing` + `WebApplicationFactory<Program>`):
12. `POST /api/todos` with blank title → `400`
13. `POST /api/todos` with valid title → `201` + `Location` header
14. `GET /api/todos/{unknownId}` → `404`
15. `DELETE /api/todos/{unknownId}` → `404`
