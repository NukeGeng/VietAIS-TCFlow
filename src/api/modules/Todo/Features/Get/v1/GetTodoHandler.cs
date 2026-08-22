using FSH.Framework.Core.Caching;
using FSH.Framework.Core.Persistence;
using VietAIS.TCFlow.WebApi.Todo.Domain;
using VietAIS.TCFlow.WebApi.Todo.Exceptions;
using MediatR;
using Microsoft.Extensions.DependencyInjection;

namespace VietAIS.TCFlow.WebApi.Todo.Features.Get.v1;
public sealed class GetTodoHandler(
    [FromKeyedServices("todo")] IReadRepository<TodoItem> repository,
    ICacheService cache)
    : IRequestHandler<GetTodoRequest, GetTodoResponse>
{
    public async Task<GetTodoResponse> Handle(GetTodoRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var item = await cache.GetOrSetAsync(
            $"todo:{request.Id}",
            async () =>
            {
                var todoItem = await repository.GetByIdAsync(request.Id, cancellationToken);
                if (todoItem == null) throw new TodoItemNotFoundException(request.Id);
                return new GetTodoResponse(todoItem.Id, todoItem.Title!, todoItem.Note!);
            },
            cancellationToken: cancellationToken);
        return item!;
    }
}
