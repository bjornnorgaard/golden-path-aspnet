using Microsoft.EntityFrameworkCore;
using WebApi.Database;

namespace WebApi.Configurations;

public static class DatabaseConfiguration
{
    extension(WebApplicationBuilder builder)
    {
        public void AddDatabase()
        {
            var cs = builder.Configuration.GetConnectionStrings().DefaultConnection;
            builder.Services.AddDbContext<TodoContext>((_, opts) => opts.UseNpgsql(cs, npgsql => npgsql.EnableRetryOnFailure()));
        }
    }

    extension(WebApplication app)
    {
        public void UseDatabase()
        {
            using var scope = app.Services.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<TodoContext>();

            var connection = dbContext.Database.GetDbConnection();
            connection.Open();
            try
            {
                using var lockCmd = connection.CreateCommand();
                lockCmd.CommandText = "SELECT pg_advisory_lock(7423982);";
                lockCmd.ExecuteNonQuery();

                try
                {
                    dbContext.Database.Migrate();
                }
                finally
                {
                    using var unlockCmd = connection.CreateCommand();
                    unlockCmd.CommandText = "SELECT pg_advisory_unlock(7423982);";
                    unlockCmd.ExecuteNonQuery();
                }
            }
            finally
            {
                connection.Close();
            }
        }
    }
}