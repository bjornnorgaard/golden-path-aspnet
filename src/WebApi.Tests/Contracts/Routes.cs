namespace WebApi.Tests.Contracts;

public static class Routes
{
    public static class Todos
    {
        public const string Create = "/todos/create";
        public static string GetById(string id) => $"/todos/{id}";
    }
}