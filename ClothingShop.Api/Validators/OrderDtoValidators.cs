using ClothingShop.Api.Dtos;
using FluentValidation;

namespace ClothingShop.Api.Validators;

public class PlaceOrderDtoValidator : AbstractValidator<PlaceOrderDto>
{
    public PlaceOrderDtoValidator()
    {
        RuleFor(x => x.PaymentMethod)
            .Must(pm => string.IsNullOrWhiteSpace(pm) || new[] { "simulate", "vnpay", "stripe" }.Contains(pm.ToLowerInvariant()))
            .WithMessage("paymentMethod must be one of: simulate, vnpay, stripe");
    }
}

public class PayOrderDtoValidator : AbstractValidator<PayOrderDto>
{
    public PayOrderDtoValidator()
    {
        RuleFor(x => x.Provider).NotEmpty().MaximumLength(50);
    }
}


