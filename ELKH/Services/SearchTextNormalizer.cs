namespace ELKH.Services;

internal static class SearchTextNormalizer
{
    public static string NormalizeQuery(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var cleaned = value.Trim().Replace('"', ' ').Replace('\'', ' ');
        var decomposed = cleaned.Normalize(System.Text.NormalizationForm.FormD);
        var buffer = new System.Text.StringBuilder(decomposed.Length);

        foreach (var ch in decomposed)
        {
            if (System.Globalization.CharUnicodeInfo.GetUnicodeCategory(ch) != System.Globalization.UnicodeCategory.NonSpacingMark)
            {
                buffer.Append(ch);
            }
        }

        return buffer.ToString()
            .Normalize(System.Text.NormalizationForm.FormC)
            .ToLowerInvariant();
    }
}
