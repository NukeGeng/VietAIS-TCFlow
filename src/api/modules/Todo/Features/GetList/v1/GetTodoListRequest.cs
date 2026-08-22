using FSH.Framework.Core.Paging;
using MediatR;

namespace VietAIS.TCFlow.WebApi.Todo.Features.GetList.v1;
public record GetTodoListRequest(PaginationFilter Filter) : IRequest<PagedList<TodoDto>>;
