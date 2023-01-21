using AuditInterceptorSampleApp.Classes;
using AuditInterceptorSampleApp.Data;
using Microsoft.EntityFrameworkCore;

namespace AuditInterceptorSampleApp;

internal partial class Program
{
    static async Task Main(string[] args)
    {
        AnsiConsole.MarkupLine("[cyan]Creating database[/]");

        var (success, exception) = await SetupDatabase.CreateDatabase();
        if (success)
        {
            AnsiConsole.MarkupLine("[cyan]Populating database[/]");
            var (verified, exception1) = await SetupDatabase.InitialCreateDatabase();
            if (verified)
            {
                AnsiConsole.MarkupLine("[cyan]Performing updates[/]");
                await UpdateRecords();
                AnsiConsole.MarkupLine("[cyan]Done, check out the log file under[/] [yellow]LogFiles[/] [cyan]from the app folder[/]");
            }
            else
            {
                AnsiConsole.WriteException(exception);
            }
        }
        else
        {
            AnsiConsole.WriteException(exception);
        }

        AnsiConsole.MarkupLine("[yellow]Press ENTER to exit[/]");
        Console.ReadLine();
    }

    private static async Task UpdateRecords()
    {
        await using var context = new BookContext();

        int identifier = 1;
        var book = await context.Books.FirstOrDefaultAsync(x => x.Id == identifier);
        book.Title = "C# in Depth - changed";
        book.Price += 10;


        identifier = 4;
        book = await context.Books.FirstOrDefaultAsync(x => x.Id == identifier);
        book.Title = "Entity Framework Core in Action - updated";
        book.Price -= 10;

        await context.SaveChangesAsync();
    }
}