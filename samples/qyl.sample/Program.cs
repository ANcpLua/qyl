using Qyl.Sample;

var builder = WebApplication.CreateSlimBuilder(args);

builder.Services.AddQylApi(AppJsonSerializerContext.Default);
builder.Services.AddSingleton<TodoStore>();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.MapTodos();

app.Run();
