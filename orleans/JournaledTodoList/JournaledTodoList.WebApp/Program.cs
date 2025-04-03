using JournaledTodoList.WebApp.Components;
using JournaledTodoList.WebApp.Services;
using Orleans.Providers;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();
builder.UseOrleans(siloBuilder =>
{
    var createShardKey = false;
    siloBuilder.UseMongoDBClient("mongodb://admin:admin@localhost:27017");
    siloBuilder.UseMongoDBClustering(options =>
    {
        options.DatabaseName = "OrleansTestApp";
        options.CreateShardKeyForCosmos = createShardKey;
    });
    siloBuilder.AddMongoDBGrainStorageAsDefault(options =>
    {
        options.DatabaseName = "OrleansTestAppPubSubStore";
        options.CreateShardKeyForCosmos = createShardKey;
    });
    siloBuilder.AddLogStorageBasedLogConsistencyProviderAsDefault();
    siloBuilder.AddStateStorageBasedLogConsistencyProvider(name: Constants.StateStorageProviderName);
});
builder.Services.AddScoped<TodoListService>();

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

var app = builder.Build();

app.MapDefaultEndpoints();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();

app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
