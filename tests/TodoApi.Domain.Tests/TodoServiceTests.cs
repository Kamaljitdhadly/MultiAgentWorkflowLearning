using TodoApi.Domain;

namespace TodoApi.Domain.Tests;

public class TodoServiceTests
{
    [Fact]
    public void GetAll_ReturnsEmpty_WhenNoTodosCreated()
    {
        // Arrange
        var service = new TodoService();

        // Act
        var result = service.GetAll();

        // Assert
        Assert.Empty(result);
    }

    [Fact]
    public void Create_ReturnsTodoWithGeneratedId_AndIsCompleteFalse()
    {
        // Arrange
        var service = new TodoService();

        // Act
        var todo = service.Create("Buy milk");

        // Assert
        Assert.True(todo.Id > 0);
        Assert.Equal("Buy milk", todo.Title);
        Assert.False(todo.IsComplete);
    }

    [Fact]
    public void Create_SetsCreatedAtCloseToUtcNow()
    {
        // Arrange
        var service = new TodoService();
        var before = DateTime.UtcNow;

        // Act
        var todo = service.Create("Buy milk");
        var after = DateTime.UtcNow;

        // Assert
        Assert.InRange(todo.CreatedAt, before.AddSeconds(-1), after.AddSeconds(1));
    }

    [Fact]
    public void Create_MultipleCalls_ProduceUniqueSequentialIds()
    {
        // Arrange
        var service = new TodoService();

        // Act
        var first = service.Create("First");
        var second = service.Create("Second");
        var third = service.Create("Third");

        // Assert
        Assert.Equal(first.Id + 1, second.Id);
        Assert.Equal(second.Id + 1, third.Id);
        Assert.Equal(3, new[] { first.Id, second.Id, third.Id }.Distinct().Count());
    }

    [Fact]
    public void GetById_ReturnsMatchingTodo_WhenExists()
    {
        // Arrange
        var service = new TodoService();
        var created = service.Create("Buy milk");

        // Act
        var found = service.GetById(created.Id);

        // Assert
        Assert.NotNull(found);
        Assert.Equal(created.Id, found!.Id);
        Assert.Equal(created.Title, found.Title);
    }

    [Fact]
    public void GetById_ReturnsNull_WhenIdDoesNotExist()
    {
        // Arrange
        var service = new TodoService();

        // Act
        var found = service.GetById(999);

        // Assert
        Assert.Null(found);
    }

    [Fact]
    public void Update_ChangesTitleAndIsComplete_ReturnsTrue_WhenExists()
    {
        // Arrange
        var service = new TodoService();
        var created = service.Create("Buy milk");

        // Act
        var result = service.Update(created.Id, "Buy oat milk", true);

        // Assert
        Assert.True(result);
        var updated = service.GetById(created.Id);
        Assert.NotNull(updated);
        Assert.Equal("Buy oat milk", updated!.Title);
        Assert.True(updated.IsComplete);
        Assert.Equal(created.CreatedAt, updated.CreatedAt);
        Assert.Equal(created.Id, updated.Id);
    }

    [Fact]
    public void Update_ReturnsFalse_WhenIdDoesNotExist()
    {
        // Arrange
        var service = new TodoService();

        // Act
        var exception = Record.Exception(() => service.Update(999, "Does not exist", true));
        var result = service.Update(999, "Does not exist", true);

        // Assert
        Assert.Null(exception);
        Assert.False(result);
    }

    [Fact]
    public void Delete_RemovesItem_ReturnsTrue_WhenExists()
    {
        // Arrange
        var service = new TodoService();
        var created = service.Create("Buy milk");

        // Act
        var result = service.Delete(created.Id);

        // Assert
        Assert.True(result);
        Assert.Null(service.GetById(created.Id));
    }

    [Fact]
    public void Delete_ReturnsFalse_WhenIdDoesNotExist()
    {
        // Arrange
        var service = new TodoService();

        // Act
        var result = service.Delete(999);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public async Task Create_CalledConcurrently_NeverProducesDuplicateIds()
    {
        // Arrange
        var service = new TodoService();
        const int concurrentCreates = 100;

        // Act
        var tasks = Enumerable.Range(0, concurrentCreates)
            .Select(i => Task.Run(() => service.Create($"Todo {i}")))
            .ToArray();
        var results = await Task.WhenAll(tasks);

        // Assert
        var ids = results.Select(t => t.Id).ToList();
        Assert.Equal(concurrentCreates, ids.Count);
        Assert.Equal(concurrentCreates, ids.Distinct().Count());
    }
}
