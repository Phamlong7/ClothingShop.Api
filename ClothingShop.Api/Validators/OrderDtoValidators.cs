using ClothingShop.Api.Dtos;
using FluentValidation;

namespace ClothingShop.Api.Validators;

public class PlaceOrderDtoValidator : AbstractValidator<PlaceOrderDto>
{
    public PlaceOrderDtoValidator()
    {
        RuleFor(x => x.PaymentMethod)
            .Must(pm => string.IsNullOrWhiteSpace(pm) || new[] { "simulate", "payos", "vnpay" }.Contains(pm.ToLowerInvariant()))
            .WithMessage("paymentMethod must be one of: simulate, payos, vnpay");
    }
}

public class PayOrderDtoValidator : AbstractValidator<PayOrderDto>
{
    public PayOrderDtoValidator()
    {
        RuleFor(x => x.Provider).NotEmpty().MaximumLength(50);
    }
}


