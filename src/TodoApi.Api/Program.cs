using TodoApi.Domain;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSingleton<ITodoService, TodoService>();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

var todos = app.MapGroup("/api/todos");

todos.MapGet("", (ITodoService service) =>
{
    return Results.Ok(service.GetAll());
});

todos.MapGet("/{id:int}", (int id, ITodoService service) =>
{
    var todo = service.GetById(id);
    return todo is not null ? Results.Ok(todo) : Results.NotFound();
});

todos.MapPost("", (CreateTodoRequest request, ITodoService service) =>
{
    if (string.IsNullOrWhiteSpace(request.Title))
    {
        return Results.BadRequest(new { error = "Title is required." });
    }

    var todo = service.Create(request.Title);
    return Results.Created($"/api/todos/{todo.Id}", todo);
});

todos.MapPut("/{id:int}", (int id, UpdateTodoRequest request, ITodoService service) =>
{
    if (string.IsNullOrWhiteSpace(request.Title))
    {
        return Results.BadRequest(new { error = "Title is required." });
    }

    var updated = service.Update(id, request.Title, request.IsComplete);
    if (!updated)
    {
        return Results.NotFound();
    }

    var todo = service.GetById(id);
    return Results.Ok(todo);
});

todos.MapDelete("/{id:int}", (int id, ITodoService service) =>
{
    var deleted = service.Delete(id);
    return deleted ? Results.NoContent() : Results.NotFound();
});

todos.MapPatch("/{id:int}/complete", (int id, MarkCompleteRequest request, ITodoService service) =>
{
    var updated = service.MarkComplete(id, request.IsComplete);
    if (!updated)
    {
        return Results.NotFound();
    }

    var todo = service.GetById(id);
    return Results.Ok(todo);
});

app.Run();

public record CreateTodoRequest(string Title);
public record UpdateTodoRequest(string Title, bool IsComplete);
public record MarkCompleteRequest(bool IsComplete);

public partial class Program { }
