namespace ClothingShop.Api.Dtos;

public record CartAddDto(Guid ProductId, int Quantity);
public record CartUpdateDto(int Quantity);


