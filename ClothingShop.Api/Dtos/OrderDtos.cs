namespace ClothingShop.Api.Dtos;

public record PlaceOrderDto(string? PaymentMethod);
public record PayOrderDto(string Provider, string? PaymentIntentId);


