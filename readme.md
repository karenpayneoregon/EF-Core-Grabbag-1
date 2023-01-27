In this article learn about methods to examine data in-line with current code and logging to physical files with and without EF Core Interceptors.

The average developer stepping into EF Core has a learning curve which once mastered EF Core becomes extremely easy to use, well for the basics. These developers tend to not understand what is available to assist with when things do not go as planned.

Also, many of these developers come from working with a data provider, writing SQL statements in code. Some will use parameterized SQL statements while others do not so we will talk about how to protect data properly.

## Source code

Requires Microsoft Visual Studio 2022, 17.4.x. Clone the following [GitHub repository](https://github.com/karenpayneoregon/EF-Core-Grabbag-1).

## Examining changed data DebugView.LongView

Yes we can set breakpoints and inspect data in a Visual Studio local window but there is an easier way for development mode.

Create an instance of a DbContext, we will name it Context. Make some changes, before calling SaveChanges add the following line.

```csharp
var temp = context.ChangeTracker.DebugView.LongView
```

Set a breakpoint on the next line, when hit inspect information in the variable temp, or simply output the information to Visual Studio's Output Window.

```csharp
Console.WriteLine(context.ChangeTracker.DebugView.LongView);
```

Or

```csharp
Debug.WriteLine(context.ChangeTracker.DebugView.LongView);
```

## Examining changed data Original and current values

Another method is to access original and proposed values. Here we have mocked data, make a change to a property and then examine both original and changed values.

```csharp
    private static async Task EditExistingBookPeek()
    {

        int identifier = 1;

        await using var context = new BookContext();

        Book book = await context.Books.AsTracking().FirstOrDefaultAsync(b => b.Id == identifier);

        if (book is null)
        {
            return;
        }

        book!.Price += 1;

        var original = context
            .Entry(book)
            .Property(product => product.Price)
            .OriginalValue;


        var current = context
            .Entry(book)
            .Property(product => product.Price)
            .CurrentValue;

    }
```

## Examining changed data with an Interceptor

What is an [interceptor](https://learn.microsoft.com/en-us/ef/core/logging-events-diagnostics/interceptors)?

From Microsoft 
*Entity Framework Core (EF Core) interceptors enable interception, modification, and/or suppression of EF Core operations. This includes low-level database operations such as executing a command, as well as higher-level operations, such as calls to SaveChanges.*

*Interceptors are different from logging and diagnostics in that they allow modification or suppression of the operation being intercepted.*

Let's look at one for writing current and proposed changes to a file.

- Override both SavingChanges and SavingChangesAsync
- Inject a method, in this case Inspect which iterates those entities that are currently being tracked and write them to a SeriLog file as json.
- Perform the save operation.

Model used for the interceptor

```csharp
public class CompareModel
{
    public object OriginalValue { get; set; }

    public object NewValue { get; set; }
    public string EntityState { get; set; }
}
```

---

```csharp
public class AuditInterceptor : SaveChangesInterceptor
{
    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = new CancellationToken())
    {
        Inspect(eventData);
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        Inspect(eventData);
        return base.SavingChanges(eventData, result);
    }

    public override int SavedChanges(SaveChangesCompletedEventData eventData, int result)
    {
        Inspect(eventData);
        return base.SavedChanges(eventData, result);
    }

    private static void Inspect(DbContextEventData eventData)
    {
        var changesList = new List<CompareModel>();

        foreach (EntityEntry entry in eventData.Context!.ChangeTracker.Entries())
        {
            switch (entry.State)
            {
                case EntityState.Added:
                    changesList.Add(new()
                    {
                        OriginalValue = null,
                        NewValue = entry.CurrentValues.ToObject(),
                        EntityState = EntityState.Added.ToString()
                    });
                    break;
                case EntityState.Deleted:
                    changesList.Add(new()
                    {
                        OriginalValue = entry.OriginalValues.ToObject(),
                        NewValue = null,
                        EntityState = EntityState.Deleted.ToString()
                    });
                    break;
                case EntityState.Modified:
                    changesList.Add(new()
                    {
                        OriginalValue = entry.OriginalValues.ToObject(),
                        NewValue = entry.CurrentValues.ToObject(),
                        EntityState = EntityState.Modified.ToString()
                    });
                    break;
                case EntityState.Detached:
                    break;
                case EntityState.Unchanged:
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }

            Log.Information($"\nchange list:{changesList.ToJson()}");
        }
    }
}
```

Sample data logged to a file

Results for several changed records

```json
[2023-01-21 13:05:54.172 [Information] 
change list:[
  {
    "OriginalValue": {
      "Id": 1,
      "Title": "C# in Depth",
      "Price": 44.99,
      "CategoryId": 1
    },
    "NewValue": {
      "Id": 1,
      "Title": "C# in Depth - changed",
      "Price": 54.99,
      "CategoryId": 1
    },
    "EntityState": "Modified"
  },
  {
    "OriginalValue": {
      "Id": 4,
      "Title": "Entity Framework Core in Action",
      "Price": 55.99,
      "CategoryId": 2
    },
    "NewValue": {
      "Id": 4,
      "Title": "Entity Framework Core in Action - updated",
      "Price": 45.99,
      "CategoryId": 2
    },
    "EntityState": "Modified"
  }
]
```


### Using a third party library for logging

There are a number of logging libraries, for this article we will use SeriLog which is easy to use and easy (for the most part) to configure via code or via settings in appsettings.json file.

NuGet packages

- [Serilog](https://www.nuget.org/packages/Serilog/2.12.1-dev-01635) base library
- [Serilog.Sinks.Console](https://www.nuget.org/packages/Serilog.Sinks.Console/4.1.1-dev-00901) for writing to the console
- [Serilog.Sinks.File]https://www.nuget.org/packages/Serilog.Sinks.File/5.0.1-dev-00947) for writing to files

For this project, SeriLog is setup under Classes\Program.cs. I recomment commenting out the `.WriteTo.Console(theme: AnsiConsoleTheme.Code)` for this project as the `.WriteTo.File(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "LogFiles", "EF-Log.txt")` captures the same information.

```csharp
Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Verbose()
    .WriteTo.Console(theme: AnsiConsoleTheme.Code)
    .WriteTo.File(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "LogFiles", "EF-Log.txt"),
        rollingInterval: RollingInterval.Day,
        outputTemplate: "[{Timestamp:yyyy-MM-dd HH:mm:ss.fff} [{Level}] {Message}{NewLine}{Exception}")
    .CreateLogger();
```

For a web app, in prgram.cs

```csharp
if (builder.Environment.IsDevelopment())
{
    Log.Logger = new LoggerConfiguration()
        .MinimumLevel.Verbose()
        .WriteTo.Console(theme: AnsiConsoleTheme.Code)
        .WriteTo.File(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "LogFiles", "Log.txt"), 
            rollingInterval: RollingInterval.Day, 
            outputTemplate: "[{Timestamp:yyyy-MM-dd HH:mm:ss.fff} [{Level}] {Message}{NewLine}{Exception}")
        .CreateLogger();
```

In BookContext a new option has been added for SeriLog

```csharp
private static void SeriLogging(DbContextOptionsBuilder optionsBuilder)
{

    optionsBuilder
        .UseSqlServer(ConnectionString())
        .EnableSensitiveDataLogging()
        .LogTo(Log.Logger.Information, LogLevel.Information, null)
        .EnableDetailedErrors();

}
```


## Safe guarding data

When writing SQL for a data provider without parameters, data is exposed to hackers. With EF Core, pass data with a variable or parameter and the data is exposed.


Example, run this code and the values for the properties are exposed.

```csharp
context.Books.Add(new Book()
{
    Price = 11.45m, 
    Title = "My second book", 
    CategoryId = 1, 
    Category = context.Categories.FirstOrDefault(x => x.CategoryId == 1)
});

```

In this case variables are sent, nothing is exposed

```csharp
decimal value = 11.45m;
string bookTitle = "My second book";
int categoryIdentifier = 1;

context.Books.Add(new Book()
{
    Price = value, 
    Title = bookTitle, 
    CategoryId = categoryIdentifier, 
    Category = context.Categories.FirstOrDefault(x => x.CategoryId == categoryIdentifier)
});
```

If we log the transaction via general logging for the DbContext as per the last sample, each value are not visible

```csharp
public static void StandardLogging(DbContextOptionsBuilder optionsBuilder)
{
        
    optionsBuilder
        .UseSqlServer(ConnectionString())
        .LogTo(message => Debug.WriteLine(message));

}
```

For debug purposes (and only in development mode) add `EnableSensitiveDataLogging` to enable inspecting for property values.

```csharp
public static void StandardLogging(DbContextOptionsBuilder optionsBuilder)
{
        
    optionsBuilder
        .UseSqlServer(ConnectionString())
        .EnableSensitiveDataLogging()
        .LogTo(message => Debug.WriteLine(message));

}
```

## In closing

For a novice developer what has been presented can literally save hours of time attempting to figure problems with code. Take time to study the code presented rather than simply copy and paste into a project which without proper understanding can become troublesome. 

There are two console projects include with commented code along with details in their respective readme files.



