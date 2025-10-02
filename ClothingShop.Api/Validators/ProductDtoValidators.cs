using ClothingShop.Api.Dtos;
using FluentValidation;

namespace ClothingShop.Api.Validators;

public class ProductCreateDtoValidator : AbstractValidator<ProductCreateDto>
{
    public ProductCreateDtoValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Product name is required")
            .MaximumLength(200).WithMessage("Product name cannot exceed 200 characters")
            .Must(name => !string.IsNullOrWhiteSpace(name)).WithMessage("Product name cannot contain only whitespace");

        RuleFor(x => x.Description)
            .NotEmpty().WithMessage("Product description is required")
            .MaximumLength(1000).WithMessage("Description cannot exceed 1000 characters")
            .Must(desc => !string.IsNullOrWhiteSpace(desc)).WithMessage("Description cannot contain only whitespace");

        RuleFor(x => x.Price)
            .GreaterThanOrEqualTo(0).WithMessage("Price must be greater than or equal to 0")
            .LessThan(1_000_000_000).WithMessage("Price is invalid");

        RuleFor(x => x.Image)
            .MaximumLength(500).WithMessage("Image URL cannot exceed 500 characters")
            .Must(BeAValidUrl).WithMessage("Image URL format is invalid")
            .When(x => !string.IsNullOrWhiteSpace(x.Image));
    }

    private bool BeAValidUrl(string? url)
    {
        if (string.IsNullOrWhiteSpace(url))
            return true;

        return Uri.TryCreate(url, UriKind.Absolute, out var uriResult)
            && (uriResult.Scheme == Uri.UriSchemeHttp || uriResult.Scheme == Uri.UriSchemeHttps);
    }
}

public class ProductUpdateDtoValidator : AbstractValidator<ProductUpdateDto>
{
    public ProductUpdateDtoValidator()
    {
        RuleFor(x => x.Name)
            .MaximumLength(200).WithMessage("Product name cannot exceed 200 characters")
            .Must(name => name == null || !string.IsNullOrWhiteSpace(name))
            .WithMessage("Product name cannot contain only whitespace")
            .When(x => x.Name != null);

        RuleFor(x => x.Description)
            .MaximumLength(1000).WithMessage("Description cannot exceed 1000 characters")
            .Must(desc => desc == null || !string.IsNullOrWhiteSpace(desc))
            .WithMessage("Description cannot contain only whitespace")
            .When(x => x.Description != null);

        RuleFor(x => x.Price)
            .GreaterThanOrEqualTo(0).WithMessage("Price must be greater than or equal to 0")
            .LessThan(1_000_000_000).WithMessage("Price is invalid")
            .When(x => x.Price.HasValue);

        RuleFor(x => x.Image)
            .MaximumLength(500).WithMessage("Image URL cannot exceed 500 characters")
            .Must(BeAValidUrl).WithMessage("Image URL format is invalid")
            .When(x => !string.IsNullOrWhiteSpace(x.Image));
    }

    private bool BeAValidUrl(string? url)
    {
        if (string.IsNullOrWhiteSpace(url))
            return true;

        return Uri.TryCreate(url, UriKind.Absolute, out var uriResult)
            && (uriResult.Scheme == Uri.UriSchemeHttp || uriResult.Scheme == Uri.UriSchemeHttps);
    }
}
