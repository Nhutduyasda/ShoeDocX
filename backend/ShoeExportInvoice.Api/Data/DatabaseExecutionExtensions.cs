using Microsoft.EntityFrameworkCore;

namespace ShoeExportInvoice.Api.Data;

public static class DatabaseExecutionExtensions
{
    // The operation must create its transaction and reload database state inside
    // the delegate. Discard entities from a rolled-back attempt before replaying.
    public static Task<T> ExecuteWithRetryAsync<T>(this AppDbContext context, Func<Task<T>> operation)
    {
        var attempt = 0;
        return context.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
        {
            if (attempt++ > 0) context.ChangeTracker.Clear();
            return await operation();
        });
    }
}
