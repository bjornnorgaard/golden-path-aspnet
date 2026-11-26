using System.Net.Http.Json;
using System.Reflection;
using Goldenpath.Todos.V1;
using Goldenpath.Users.V1;
using Google.Protobuf.WellKnownTypes;
using Google.Rpc;
using Grpc.AspNetCore.Server;
using Grpc.Core;
using Grpc.Health.V1;
using Grpc.Net.Client;
using Grpc.Reflection.V1Alpha;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using WebApi.Configurations;
using WebApi.Tests.Contracts;
using WebApi.Tests.Fixture;

namespace WebApi.Tests;

/// <summary>
/// Black-box tests of the gRPC transport: the real application (same wiring as production) runs
/// in memory against PostgreSQL and is called through generated gRPC client stubs, built from the same
/// .proto files as the server.
/// </summary>
public sealed class GrpcTests : TestBase
{
    private GrpcChannel AuthenticatedChannel => field ??= CreateChannel(Factory.Inner, authenticated: true);

    private TodosService.TodosServiceClient Todos => new(AuthenticatedChannel);

    private UsersService.UsersServiceClient Users => new(AuthenticatedChannel);

    private static GrpcChannel CreateChannel(WebApplicationFactory<Program> factory, bool authenticated)
    {
        HttpMessageHandler handler = factory.Server.CreateHandler();
        if (authenticated)
        {
            handler = new AuthenticatedHandler(handler);
        }

        return GrpcChannel.ForAddress(factory.Server.BaseAddress, new GrpcChannelOptions { HttpHandler = handler });
    }

    private sealed class AuthenticatedHandler(HttpMessageHandler inner) : DelegatingHandler(inner)
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            request.Headers.Add(TestAuthHandler.AuthenticatedHeader, "true");
            return base.SendAsync(request, cancellationToken);
        }
    }

    private static async Task<RpcException> ExpectFailureAsync(Func<Task> call)
    {
        try
        {
            await call();
        }
        catch (RpcException ex)
        {
            return ex;
        }

        throw new InvalidOperationException("The call was expected to fail but succeeded.");
    }

    private static string[] ViolatedFields(RpcException exception) =>
        exception.GetRpcStatus()!.GetDetail<BadRequest>()!.FieldViolations.Select(violation => violation.Field).ToArray();

    [Test]
    public async Task TodoLifecycle_WorksOverGrpc()
    {
        var dueBy = DateTimeOffset.UtcNow.AddDays(2);
        var created = (await Todos.CreateTodoAsync(new CreateTodoRequest { Title = "Created over gRPC", DueBy = Timestamp.FromDateTimeOffset(dueBy) })).Todo;
        await Assert.That(created.Title).IsEqualTo("Created over gRPC");
        await Assert.That(created.IsComplete).IsFalse();
        await Assert.That(Math.Abs((created.DueBy.ToDateTimeOffset() - dueBy).TotalSeconds)).IsLessThan(1);

        var fetched = (await Todos.GetTodoByIdAsync(new GetTodoByIdRequest { Id = created.Id })).Todo;
        await Assert.That(fetched.Title).IsEqualTo(created.Title);

        var list = await Todos.GetTodoListAsync(new GetTodoListRequest());
        await Assert.That(list.Todos.Any(todo => todo.Id == created.Id)).IsTrue();

        var updated = (await Todos.UpdateTodoAsync(new UpdateTodoRequest { Id = created.Id, Title = "Renamed over gRPC" })).Todo;
        await Assert.That(updated.Title).IsEqualTo("Renamed over gRPC");
        await Assert.That(updated.DueBy).IsNull();

        var toggled = (await Todos.ToggleTodoAsync(new ToggleTodoRequest { Id = created.Id })).Todo;
        await Assert.That(toggled.IsComplete).IsTrue();

        var deleted = await Todos.DeleteTodoAsync(new DeleteTodoRequest { Id = created.Id });
        await Assert.That(deleted.Id).IsEqualTo(created.Id);

        var notFound = await ExpectFailureAsync(async () => await Todos.GetTodoByIdAsync(new GetTodoByIdRequest { Id = created.Id }));
        await Assert.That(notFound.StatusCode).IsEqualTo(StatusCode.NotFound);
    }

    [Test]
    public async Task TodoCreatedOverGrpc_IsServedByRest()
    {
        var created = (await Todos.CreateTodoAsync(new CreateTodoRequest { Title = "Visible over REST" })).Todo;

        var response = await Client.PostAsJsonAsync(TestRoutes.Todos.GetById, new GetTodoByIdApiRequest { Id = created.Id });

        await Assert.That(response.IsSuccessStatusCode).IsTrue();
    }

    [Test]
    public async Task CreateTodo_InvalidTitle_IsInvalidArgumentWithFieldViolation()
    {
        var failure = await ExpectFailureAsync(async () => await Todos.CreateTodoAsync(new CreateTodoRequest { Title = "ab" }));

        await Assert.That(failure.StatusCode).IsEqualTo(StatusCode.InvalidArgument);
        await Assert.That(ViolatedFields(failure)).Contains("title");
    }

    [Test]
    public async Task GetTodoList_NegativeOffset_IsInvalidArgument()
    {
        var failure = await ExpectFailureAsync(async () => await Todos.GetTodoListAsync(new GetTodoListRequest { Offset = -1 }));

        await Assert.That(failure.StatusCode).IsEqualTo(StatusCode.InvalidArgument);
        await Assert.That(ViolatedFields(failure)).Contains("offset");
    }

    [Test]
    public async Task TodoOperations_MalformedId_IsInvalidArgumentOnId()
    {
        var calls = new Func<Task>[]
        {
            async () => await Todos.GetTodoByIdAsync(new GetTodoByIdRequest { Id = "nope" }),
            async () => await Todos.UpdateTodoAsync(new UpdateTodoRequest { Id = "nope", Title = "Long enough" }),
            async () => await Todos.ToggleTodoAsync(new ToggleTodoRequest { Id = "nope" }),
            async () => await Todos.DeleteTodoAsync(new DeleteTodoRequest { Id = "nope" })
        };

        foreach (var call in calls)
        {
            var failure = await ExpectFailureAsync(call);
            await Assert.That(failure.StatusCode).IsEqualTo(StatusCode.InvalidArgument);
            await Assert.That(ViolatedFields(failure)).Contains("id");
        }
    }

    [Test]
    public async Task TodoOperations_UnknownId_IsNotFound()
    {
        var id = Guid.NewGuid().ToString();
        var calls = new Func<Task>[]
        {
            async () => await Todos.GetTodoByIdAsync(new GetTodoByIdRequest { Id = id }),
            async () => await Todos.UpdateTodoAsync(new UpdateTodoRequest { Id = id, Title = "Long enough" }),
            async () => await Todos.ToggleTodoAsync(new ToggleTodoRequest { Id = id }),
            async () => await Todos.DeleteTodoAsync(new DeleteTodoRequest { Id = id })
        };

        foreach (var call in calls)
        {
            var failure = await ExpectFailureAsync(call);
            await Assert.That(failure.StatusCode).IsEqualTo(StatusCode.NotFound);
        }
    }

    [Test]
    public async Task Users_WorkOverGrpc()
    {
        var current = (await Users.GetUserByIdAsync(new GetUserByIdRequest { Id = TestAuthHandler.TestUserId.ToString() })).User;
        await Assert.That(current.Email).IsEqualTo(TestAuthHandler.AllowedEmail);

        var list = await Users.GetUserListAsync(new GetUserListRequest());
        await Assert.That(list.Users.Count).IsGreaterThan(0);

        var malformed = await ExpectFailureAsync(async () => await Users.GetUserByIdAsync(new GetUserByIdRequest { Id = "nope" }));
        await Assert.That(malformed.StatusCode).IsEqualTo(StatusCode.InvalidArgument);

        var missing = await ExpectFailureAsync(async () => await Users.GetUserByIdAsync(new GetUserByIdRequest { Id = Guid.NewGuid().ToString() }));
        await Assert.That(missing.StatusCode).IsEqualTo(StatusCode.NotFound);

        var unknownUpdate = await ExpectFailureAsync(async () => await Users.UpdateUserAsync(new UpdateUserRequest { Id = Guid.NewGuid().ToString(), DisplayName = "x" }));
        await Assert.That(unknownUpdate.StatusCode).IsEqualTo(StatusCode.NotFound);

        var unknownDelete = await ExpectFailureAsync(async () => await Users.DeleteUserAsync(new DeleteUserRequest { Id = Guid.NewGuid().ToString() }));
        await Assert.That(unknownDelete.StatusCode).IsEqualTo(StatusCode.NotFound);
    }

    // Deny-by-default: every gRPC endpoint is covered by the global fallback policy, and the set of
    // anonymous ones is pinned. Unary endpoints are additionally called without a session.
    [Test]
    public async Task EveryGrpcMethod_RequiresAuthenticationUnlessAllowListed()
    {
        var endpoints = Factory.Services.GetRequiredService<EndpointDataSource>().Endpoints
            .Select(endpoint => (Endpoint: endpoint, Method: endpoint.Metadata.GetMetadata<GrpcMethodMetadata>()?.Method))
            .Where(item => item.Method is not null)
            .ToArray();
        await Assert.That(endpoints.Length).IsGreaterThan(0);

        var anonymous = endpoints
            .Where(item => item.Endpoint.Metadata.GetMetadata<Microsoft.AspNetCore.Authorization.IAllowAnonymous>() is not null)
            .Select(item => item.Method!.FullName)
            .Order()
            .ToArray();
        await Assert.That(anonymous).IsEquivalentTo(GrpcConfiguration.AnonymousMethods.Order().ToArray());

        using var channel = CreateChannel(Factory.Inner, authenticated: false);
        var invoker = channel.CreateCallInvoker();
        foreach (var (_, method) in endpoints.Where(item => item.Method!.Type == MethodType.Unary && !GrpcConfiguration.AnonymousMethods.Contains(item.Method!.FullName)))
        {
            var raw = new Method<byte[], byte[]>(MethodType.Unary, method!.ServiceName, method.Name, Marshallers.Create(bytes => bytes, bytes => bytes), Marshallers.Create(bytes => bytes, bytes => bytes));
            var failure = await ExpectFailureAsync(async () => await invoker.AsyncUnaryCall(raw, null, new CallOptions(), []));
            await Assert.That(failure.StatusCode).IsEqualTo(StatusCode.Unauthenticated);
        }
    }

    [Test]
    public async Task HealthCheck_IsAnonymous()
    {
        using var channel = CreateChannel(Factory.Inner, authenticated: false);

        var response = await new Health.HealthClient(channel).CheckAsync(new HealthCheckRequest());

        await Assert.That(response.Status).IsEqualTo(HealthCheckResponse.Types.ServingStatus.Serving);
    }

    [Test]
    public async Task Reflection_IsOffByDefault()
    {
        var failure = await ExpectFailureAsync(async () => await ListServicesAsync(AuthenticatedChannel));

        await Assert.That(failure.StatusCode).IsEqualTo(StatusCode.Unimplemented);
    }

    [Test]
    public async Task Reflection_WhenEnabled_StillRequiresAuthentication()
    {
        await using var factory = Factory.Inner.WithWebHostBuilder(builder => builder.UseSetting("Grpc:ReflectionEnabled", "true"));
        using var anonymous = CreateChannel(factory, authenticated: false);
        using var authenticated = CreateChannel(factory, authenticated: true);

        var denied = await ExpectFailureAsync(async () => await ListServicesAsync(anonymous));
        var services = await ListServicesAsync(authenticated);

        await Assert.That(denied.StatusCode).IsEqualTo(StatusCode.Unauthenticated);
        await Assert.That(services).Contains("goldenpath.todos.v1.TodosService");
    }

    private static async Task<string[]> ListServicesAsync(GrpcChannel channel)
    {
        using var call = new ServerReflection.ServerReflectionClient(channel).ServerReflectionInfo();
        await call.RequestStream.WriteAsync(new ServerReflectionRequest { ListServices = "" });
        await call.RequestStream.CompleteAsync();
        await call.ResponseStream.MoveNext(CancellationToken.None);
        return call.ResponseStream.Current.ListServicesResponse.Service.Select(service => service.Name).ToArray();
    }

    // The generated service bases have virtual methods that answer UNIMPLEMENTED, so (unlike the
    // REST and GraphQL interfaces) a forgotten RPC does not stop the build. This test does.
    [Test]
    public async Task EveryRpcInTheContract_IsImplemented()
    {
        var bases = typeof(Program).Assembly.GetTypes()
            .Where(type => type is { IsAbstract: true, IsNested: true } && type.GetCustomAttribute<BindServiceMethodAttribute>() is not null)
            .ToArray();
        await Assert.That(bases.Length).IsEqualTo(2);

        foreach (var serviceBase in bases)
        {
            var implementations = typeof(Program).Assembly.GetTypes().Where(type => !type.IsAbstract && serviceBase.IsAssignableFrom(type)).ToArray();
            await Assert.That(implementations.Length).IsEqualTo(1);

            foreach (var rpc in serviceBase.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly).Where(method => method.IsVirtual))
            {
                var implemented = implementations[0].GetMethod(rpc.Name, rpc.GetParameters().Select(parameter => parameter.ParameterType).ToArray());
                await Assert.That(implemented!.DeclaringType).IsEqualTo(implementations[0]);
            }
        }
    }

    // A gRPC client cannot follow a redirect to the login page, so the cookie scheme answers 401 for
    // gRPC requests (and keeps redirecting everything else).
    [Test]
    public async Task CookieChallenge_ForGrpc_Is401_AndRedirectsOtherwise()
    {
        var options = Factory.Services.GetRequiredService<IOptionsMonitor<CookieAuthenticationOptions>>().Get(CookieAuthenticationDefaults.AuthenticationScheme);
        var scheme = new AuthenticationScheme(CookieAuthenticationDefaults.AuthenticationScheme, null, typeof(CookieAuthenticationHandler));

        var grpcContext = new DefaultHttpContext();
        grpcContext.Request.ContentType = "application/grpc";
        await options.Events.RedirectToLogin(new RedirectContext<CookieAuthenticationOptions>(grpcContext, scheme, options, new AuthenticationProperties(), "/login"));

        var webContext = new DefaultHttpContext();
        webContext.Request.ContentType = "application/json";
        await options.Events.RedirectToLogin(new RedirectContext<CookieAuthenticationOptions>(webContext, scheme, options, new AuthenticationProperties(), "/login"));

        await Assert.That(grpcContext.Response.StatusCode).IsEqualTo(StatusCodes.Status401Unauthorized);
        await Assert.That(grpcContext.Response.Headers.Location.ToString()).IsEmpty();
        await Assert.That(webContext.Response.StatusCode).IsEqualTo(StatusCodes.Status302Found);
    }
}
