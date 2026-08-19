using FluentValidation;
using VietAIS.TCFlow.WebApi.Todo.Persistence;

namespace VietAIS.TCFlow.WebApi.Todo.Features.Update.v1;
public class UpdateTodoValidator : AbstractValidator<UpdateTodoCommand>
{
    public UpdateTodoValidator(TodoDbContext context)
    {
        RuleFor(p => p.Title).NotEmpty();
        RuleFor(p => p.Note).NotEmpty();
    }
}
