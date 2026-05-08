using System.Text.Json.Serialization;
using WebApi.Database.Models;
using WebApi.Features;
using WebApi.Features.Todos;

namespace WebApi.Json;

[JsonSerializable(typeof(Todo[]))]
[JsonSerializable(typeof(CreateTodo.RequestBody), TypeInfoPropertyName = "CreateTodoRequestBody")]
[JsonSerializable(typeof(CreateTodo.ResponseBody), TypeInfoPropertyName = "CreateTodoResponseBody")]
[JsonSerializable(typeof(DeleteTodo.RequestBody), TypeInfoPropertyName = "DeleteTodoRequestBody")]
[JsonSerializable(typeof(DeleteTodo.ResponseBody), TypeInfoPropertyName = "DeleteTodoResponseBody")]
[JsonSerializable(typeof(GetTodoById.RequestBody), TypeInfoPropertyName = "GetTodoByIdRequestBody")]
[JsonSerializable(typeof(GetTodoById.ResponseBody), TypeInfoPropertyName = "GetTodoByIdResponseBody")]
[JsonSerializable(typeof(GetTodoList.RequestBody), TypeInfoPropertyName = "GetTodoListRequestBody")]
[JsonSerializable(typeof(GetTodoList.ResponseBody), TypeInfoPropertyName = "GetTodoListResponseBody")]
[JsonSerializable(typeof(UpdateTodo.RequestBody), TypeInfoPropertyName = "UpdateTodoRequestBody")]
[JsonSerializable(typeof(UpdateTodo.ResponseBody), TypeInfoPropertyName = "UpdateTodoResponseBody")]
[JsonSerializable(typeof(Failure))]
internal partial class AppJsonSerializerContext : JsonSerializerContext;