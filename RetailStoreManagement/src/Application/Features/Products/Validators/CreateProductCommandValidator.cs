using Application.Features.Products.Commands;
using FluentValidation;

namespace Application.Features.Products.Validators;

/// <summary>
/// Validator cho CreateProductCommand
/// </summary>
public class CreateProductCommandValidator : AbstractValidator<CreateProductCommand>
{
    public CreateProductCommandValidator()
    {
        RuleFor(x => x.ProductName)
            .NotEmpty().WithMessage("Tên sản phẩm không được để trống")
            .MaximumLength(100).WithMessage("Tên sản phẩm không được vượt quá 100 ký tự");

        RuleFor(x => x.Barcode)
            .NotEmpty().WithMessage("Mã barcode không được để trống")
            .MaximumLength(50).WithMessage("Mã barcode không được vượt quá 50 ký tự");

        RuleFor(x => x.Price)
            .GreaterThan(0).WithMessage("Giá phải lớn hơn 0");

        RuleFor(x => x.CategoryId)
            .GreaterThan(0).WithMessage("Vui lòng chọn danh mục");

        RuleFor(x => x.SupplierId)
            .GreaterThan(0).WithMessage("Vui lòng chọn nhà cung cấp");
    }
}
