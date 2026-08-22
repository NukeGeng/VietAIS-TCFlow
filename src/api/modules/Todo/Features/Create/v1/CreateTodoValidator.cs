using FluentValidation;
using VietAIS.TCFlow.WebApi.Todo.Persistence;

namespace VietAIS.TCFlow.WebApi.Todo.Features.Create.v1;
public class CreateTodoValidator : AbstractValidator<CreateTodoCommand>
{
    public CreateTodoValidator(TodoDbContext context)
    {
        RuleFor(p => p.Title).NotEmpty();
        RuleFor(p => p.Note).NotEmpty();
    }
}
