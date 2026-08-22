using FluentValidation;

namespace VietAIS.TCFlow.WebApi.Catalog.Application.Products.Create.v1;

public sealed class CreateProductCommandValidator : AbstractValidator<CreateProductCommand>
{
    public CreateProductCommandValidator()
    {
        RuleFor(product => product.Name).NotEmpty().MinimumLength(2).MaximumLength(75);
        RuleFor(product => product.Price).GreaterThan(0);
    }
}
