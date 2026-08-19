using FSH.Framework.Core.Persistence;
using VietAIS.TCFlow.WebApi.Todo.Domain;
using VietAIS.TCFlow.WebApi.Todo.Exceptions;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace VietAIS.TCFlow.WebApi.Todo.Features.Delete.v1;
public sealed class DeleteTodoHandler(
    ILogger<DeleteTodoHandler> logger,
    [FromKeyedServices("todo")] IRepository<TodoItem> repository)
    : IRequestHandler<DeleteTodoCommand>
{
    public async Task Handle(DeleteTodoCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var todoItem = await repository.GetByIdAsync(request.Id, cancellationToken);
        _ = todoItem ?? throw new TodoItemNotFoundException(request.Id);
        await repository.DeleteAsync(todoItem, cancellationToken);
        logger.LogInformation("todo with id : {TodoId} deleted", todoItem.Id);
    }
}
