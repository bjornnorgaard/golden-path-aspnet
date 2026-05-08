namespace WebApi.Endpoints;

public static class Routes
{
    public static class Todos
    {
        public const string Create = "/todos";
        public const string GetById = "/todos/{id}";
    }
}