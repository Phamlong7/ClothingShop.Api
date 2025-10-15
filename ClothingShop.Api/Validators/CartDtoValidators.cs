using ClothingShop.Api.Dtos;
using FluentValidation;

namespace ClothingShop.Api.Validators;

public class CartAddDtoValidator : AbstractValidator<CartAddDto>
{
    public CartAddDtoValidator()
    {
        RuleFor(x => x.ProductId).NotEmpty();
        RuleFor(x => x.Quantity).GreaterThan(0).LessThanOrEqualTo(100);
    }
}

public class CartUpdateDtoValidator : AbstractValidator<CartUpdateDto>
{
    public CartUpdateDtoValidator()
    {
        RuleFor(x => x.Quantity).GreaterThan(0).LessThanOrEqualTo(100);
    }
}


