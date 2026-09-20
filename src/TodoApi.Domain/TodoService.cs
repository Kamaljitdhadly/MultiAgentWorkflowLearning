using System.Collections.Concurrent;

namespace TodoApi.Domain;

public class TodoService : ITodoService
{
    private readonly ConcurrentDictionary<int, TodoItem> _todos = new();
    private int _nextId = 0;

    public IReadOnlyList<TodoItem> GetAll()
    {
        return _todos.Values.ToList();
    }

    public TodoItem? GetById(int id)
    {
        return _todos.TryGetValue(id, out var todo) ? todo : null;
    }

    public TodoItem Create(string title)
    {
        var id = Interlocked.Increment(ref _nextId);
        var todo = new TodoItem
        {
            Id = id,
            Title = title,
            IsComplete = false,
            CreatedAt = DateTime.UtcNow
        };

        _todos[id] = todo;
        return todo;
    }

    public bool Update(int id, string title, bool isComplete)
    {
        while (_todos.TryGetValue(id, out var existing))
        {
            var updated = new TodoItem
            {
                Id = existing.Id,
                Title = title,
                IsComplete = isComplete,
                CreatedAt = existing.CreatedAt
            };

            if (_todos.TryUpdate(id, updated, existing))
            {
                return true;
            }
            // existing changed concurrently (another update, or it was deleted) — retry against current state
        }

        return false;
    }

    public bool MarkComplete(int id, bool isComplete)
    {
        while (_todos.TryGetValue(id, out var existing))
        {
            var updated = new TodoItem
            {
                Id = existing.Id,
                Title = existing.Title,
                IsComplete = isComplete,
                CreatedAt = existing.CreatedAt
            };

            if (_todos.TryUpdate(id, updated, existing))
            {
                return true;
            }
            // existing changed concurrently (another update, or it was deleted) — retry against current state
        }

        return false;
    }

    public bool Delete(int id)
    {
        return _todos.TryRemove(id, out _);
    }
}
