namespace TodoApi.Domain;

public interface ITodoService
{
    IReadOnlyList<TodoItem> GetAll();
    TodoItem? GetById(int id);
    TodoItem Create(string title);
    bool Update(int id, string title, bool isComplete); // false = not found
    bool Delete(int id);                                 // false = not found
}
