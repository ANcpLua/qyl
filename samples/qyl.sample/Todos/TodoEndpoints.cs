using System.ComponentModel;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Qyl.Sample;

/// <summary>HTTP surface for todos. Handlers are method groups so the Request Delegate Generator binds them at compile time.</summary>
internal static class TodoEndpoints
{
    /// <summary>Maps the <c>/todos</c> endpoints.</summary>
    /// <param name="endpoints">The route builder of the host.</param>
    /// <returns>The supplied route builder for chaining calls.</returns>
    public static IEndpointRouteBuilder MapTodos(this IEndpointRouteBuilder endpoints)
    {
        var todos = endpoints.MapGroup("/todos").WithTags("Todos");

        todos.MapGet("/", GetTodos)
            .WithName("GetTodos");

        todos.MapGet("/{id:int}", GetTodoById)
            .WithName("GetTodoById");

        todos.MapGet("/{id:int}/xml", GetTodoXml)
            .WithName("GetTodoXml");

        todos.MapPost("/", CreateTodo)
            .WithName("CreateTodo")
            .ProducesValidationProblem();

        return endpoints;
    }

    /// <summary>Lists every todo.</summary>
    /// <param name="store">Todo storage.</param>
    /// <response code="200">All todos, ordered by identity.</response>
    public static Ok<Todo[]> GetTodos(TodoStore store) => TypedResults.Ok(store.All);

    /// <summary>Gets a todo as JSON.</summary>
    /// <param name="id">Identity of the todo.</param>
    /// <param name="store">Todo storage.</param>
    /// <response code="200">The todo.</response>
    /// <response code="404">No todo has that identity.</response>
    public static Results<Ok<Todo>, NotFound> GetTodoById(int id, TodoStore store) =>
        store.Find(id) is { } todo
            ? TypedResults.Ok(todo)
            : TypedResults.NotFound();

    /// <summary>Gets a todo as XML.</summary>
    /// <remarks>The document is written by the <c>XmlWriter</c> code generated for <c>[GenerateXml]</c>; no <c>XmlSerializer</c> and no runtime reflection are involved.</remarks>
    /// <param name="id">Identity of the todo.</param>
    /// <param name="store">Todo storage.</param>
    /// <response code="200">The todo as an XML document.</response>
    /// <response code="404">No todo has that identity.</response>
    public static Results<XmlHttpResult<Todo>, NotFound> GetTodoXml(int id, TodoStore store) =>
        store.Find(id) is { } todo
            ? QylResults.Xml(todo)
            : TypedResults.NotFound();

    /// <summary>Creates a todo.</summary>
    /// <remarks>The request is validated by the generated validation resolver before this handler runs.</remarks>
    /// <response code="201">The todo was stored; <c>Location</c> points at its JSON representation.</response>
    /// <response code="400">The request failed validation.</response>
    // The body is described with [Description] rather than <param>: the XML-comment transformer copies every <param> that is not a
    // route or query parameter onto the request body, last one winning, so a <param> for the store would replace the body text.
    public static CreatedAtRoute<Todo> CreateTodo([Description("The todo to create.")] CreateTodoRequest request, TodoStore store)
    {
        var todo = store.Add(request);

        return TypedResults.CreatedAtRoute(todo, "GetTodoById", new RouteValueDictionary { ["id"] = todo.Id });
    }
}
