namespace WebApi.Tests.Contracts;

public static class TestRoutes
{
    public static class Todos
    {
        public const string Create = "/todos/create";
        public const string GetById = "/todos/get-by-id";
        public const string GetList = "/todos/get-list";
        public const string Update = "/todos/update";
        public const string Delete = "/todos/delete";
    }
}