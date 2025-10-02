using System.ComponentModel.DataAnnotations;

namespace ClothingShop.Api.Dtos;

public record ProductCreateDto(
    [Required(ErrorMessage = "Product name is required")]
    [MaxLength(200, ErrorMessage = "Product name cannot exceed 200 characters")]
    string Name,
    
    [Required(ErrorMessage = "Product description is required")]
    [MaxLength(1000, ErrorMessage = "Description cannot exceed 1000 characters")]
    string Description,
    
    [Range(0, double.MaxValue, ErrorMessage = "Price must be greater than or equal to 0")]
    decimal Price,
    
    [MaxLength(500, ErrorMessage = "Image URL cannot exceed 500 characters")]
    string? Image
);

public record ProductUpdateDto(
    [MaxLength(200, ErrorMessage = "Product name cannot exceed 200 characters")]
    string? Name,
    
    [MaxLength(1000, ErrorMessage = "Description cannot exceed 1000 characters")]
    string? Description,
    
    [Range(0, double.MaxValue, ErrorMessage = "Price must be greater than or equal to 0")]
    decimal? Price,
    
    [MaxLength(500, ErrorMessage = "Image URL cannot exceed 500 characters")]
    string? Image
);
