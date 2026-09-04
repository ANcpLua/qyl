using System.Collections.Concurrent;

namespace Qyl.Sample;

/// <summary>In-memory todo storage, seeded with the todos the project template ships with.</summary>
public sealed class TodoStore
{
    private readonly ConcurrentDictionary<int, Todo> _todos = new();
    private int _lastId;

    /// <summary>Creates the store with the template's five sample todos.</summary>
    public TodoStore()
    {
        var today = DateOnly.FromDateTime(DateTime.Now);

        Seed(new Todo(1, "Walk the dog"));
        Seed(new Todo(2, "Do the dishes", today));
        Seed(new Todo(3, "Do the laundry", today.AddDays(1)));
        Seed(new Todo(4, "Clean the bathroom"));
        Seed(new Todo(5, "Clean the car", today.AddDays(2)));
    }

    /// <summary>Every todo, ordered by identity.</summary>
    public Todo[] All => [.. _todos.Values.OrderBy(static todo => todo.Id)];

    /// <summary>Finds a todo by identity.</summary>
    /// <param name="id">Identity of the todo.</param>
    /// <returns>The todo, or <see langword="null"/> when none has that identity.</returns>
    public Todo? Find(int id) => _todos.GetValueOrDefault(id);

    /// <summary>Stores a new todo from a validated request.</summary>
    /// <param name="request">The validated request.</param>
    /// <returns>The stored todo, including its new identity.</returns>
    public Todo Add(CreateTodoRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var todo = new Todo(Interlocked.Increment(ref _lastId), request.Title, request.DueBy);
        _todos[todo.Id] = todo;
        return todo;
    }

    private void Seed(Todo todo)
    {
        _todos[todo.Id] = todo;
        _lastId = Math.Max(_lastId, todo.Id);
    }
}
