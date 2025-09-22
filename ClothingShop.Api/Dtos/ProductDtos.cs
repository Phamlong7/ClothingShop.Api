namespace ClothingShop.Api.Dtos;

public record ProductCreateDto(string Name, string Description, decimal Price, string? Image);
public record ProductUpdateDto(string? Name, string? Description, decimal? Price, string? Image);
