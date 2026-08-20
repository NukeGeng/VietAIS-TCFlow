using FluentValidation;

namespace VietAIS.TCFlow.WebApi.Catalog;

public sealed class CreateProductCommandValidator : AbstractValidator<CreateProductCommand>
{
    public CreateProductCommandValidator()
    {
        RuleFor(product => product.Name).NotEmpty();
        RuleFor(product => product.CategoryId).NotEmpty();
    }
}
