using ELKH.Models;

namespace ELKH.ViewModels;

public sealed class CategoryBrowseResultVM
{
    public CategoryModel? CurrentCategory { get; set; }

    public List<CategoryModel> Categories { get; set; } = [];

    public List<ProductVM> Items { get; set; } = [];

    public int Page { get; set; }

    public int TotalPages { get; set; }

    public int Total { get; set; }

    public string Sort { get; set; } = string.Empty;
}

public sealed class CategoryProductCountVM
{
    public CategoryModel Category { get; set; } = default!;

    public int ProductCount { get; set; }
}
