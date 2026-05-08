namespace WebApi.Endpoints;

public static class Routes
{
    public static class Todos
    {
        public const string Create = "/create-todo";
        public const string GetById = "/todos/{id}";
    }
}