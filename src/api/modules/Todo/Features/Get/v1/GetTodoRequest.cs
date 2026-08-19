using MediatR;

namespace VietAIS.TCFlow.WebApi.Todo.Features.Get.v1;
public class GetTodoRequest : IRequest<GetTodoResponse>
{
    public Guid Id { get; set; }
    public GetTodoRequest(Guid id) => Id = id;
}
