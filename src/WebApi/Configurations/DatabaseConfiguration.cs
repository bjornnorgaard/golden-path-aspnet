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
            builder.Services.AddDbContext<TodoContext>((_, opts) => opts.UseNpgsql(cs));
        }
    }

    extension(WebApplication app)
    {
        public void UseDatabase()
        {
            using var scope = app.Services.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<TodoContext>();
            dbContext.Database.Migrate();
        }
    }
}