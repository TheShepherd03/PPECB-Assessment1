using ClosedXML.Excel;
using PPECB.Application.Abstractions;
using PPECB.Application.DTOs;

namespace PPECB.Infrastructure.Services;

/// <summary>
/// Excel import/export backed by ClosedXML. Chosen over EPPlus because its licence stays
/// free for commercial use, and over raw OpenXML because it keeps the code readable.
/// </summary>
public class ExcelService : IExcelService
{
    private const string ProductsSheetName = "Products";

    private static readonly string[] ImportHeaders =
    {
        "Name", "Description", "CategoryCode", "CategoryName", "Price"
    };

    public byte[] ExportProducts(IEnumerable<ProductDto> products)
    {
        using var workbook = new XLWorkbook();
        var sheet = workbook.Worksheets.Add(ProductsSheetName);

        var headers = new[]
        {
            "ProductCode", "Name", "Description", "CategoryCode", "CategoryName",
            "Price", "CreatedBy", "CreatedDate", "UpdatedBy", "UpdatedDate"
        };

        for (var i = 0; i < headers.Length; i++)
        {
            sheet.Cell(1, i + 1).Value = headers[i];
        }

        sheet.Row(1).Style.Font.Bold = true;

        var row = 2;
        foreach (var product in products)
        {
            sheet.Cell(row, 1).Value = product.ProductCode;
            sheet.Cell(row, 2).Value = product.Name;
            sheet.Cell(row, 3).Value = product.Description ?? string.Empty;
            sheet.Cell(row, 4).Value = product.CategoryCode;
            sheet.Cell(row, 5).Value = product.CategoryName;
            sheet.Cell(row, 6).Value = product.Price;
            sheet.Cell(row, 6).Style.NumberFormat.Format = "#,##0.00";
            sheet.Cell(row, 7).Value = product.CreatedBy;
            sheet.Cell(row, 8).Value = product.CreatedDate;
            sheet.Cell(row, 8).Style.NumberFormat.Format = "yyyy-mm-dd hh:mm";
            sheet.Cell(row, 9).Value = product.UpdatedBy ?? string.Empty;

            if (product.UpdatedDate.HasValue)
            {
                sheet.Cell(row, 10).Value = product.UpdatedDate.Value;
                sheet.Cell(row, 10).Style.NumberFormat.Format = "yyyy-mm-dd hh:mm";
            }

            row++;
        }

        sheet.Columns().AdjustToContents();
        sheet.SheetView.FreezeRows(1);

        return ToBytes(workbook);
    }

    public byte[] BuildImportTemplate()
    {
        using var workbook = new XLWorkbook();
        var sheet = workbook.Worksheets.Add(ProductsSheetName);

        for (var i = 0; i < ImportHeaders.Length; i++)
        {
            sheet.Cell(1, i + 1).Value = ImportHeaders[i];
        }

        sheet.Row(1).Style.Font.Bold = true;

        // One example row so the expected shape is obvious.
        sheet.Cell(2, 1).Value = "Sample product";
        sheet.Cell(2, 2).Value = "Optional description";
        sheet.Cell(2, 3).Value = "ABC123";
        sheet.Cell(2, 4).Value = "(optional if CategoryCode is supplied)";
        sheet.Cell(2, 5).Value = 99.99;

        sheet.Columns().AdjustToContents();
        return ToBytes(workbook);
    }

    public ExcelImportParseResult ParseProducts(Stream workbookStream)
    {
        var result = new ExcelImportParseResult();

        XLWorkbook workbook;
        try
        {
            workbook = new XLWorkbook(workbookStream);
        }
        catch (Exception)
        {
            result.Errors.Add(new ExcelImportErrorDto(
                0, "The file could not be read as an Excel workbook (.xlsx)."));
            return result;
        }

        using (workbook)
        {
            var sheet = workbook.Worksheets.FirstOrDefault(w =>
                            string.Equals(w.Name, ProductsSheetName, StringComparison.OrdinalIgnoreCase))
                        ?? workbook.Worksheets.FirstOrDefault();

            if (sheet is null)
            {
                result.Errors.Add(new ExcelImportErrorDto(0, "The workbook contains no worksheets."));
                return result;
            }

            var headerRow = sheet.FirstRowUsed();
            if (headerRow is null)
            {
                result.Errors.Add(new ExcelImportErrorDto(0, "The worksheet is empty."));
                return result;
            }

            // Map headers by name so column order does not matter.
            var columns = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            foreach (var cell in headerRow.CellsUsed())
            {
                var name = cell.GetString().Trim();
                if (!string.IsNullOrEmpty(name) && !columns.ContainsKey(name))
                {
                    columns[name] = cell.Address.ColumnNumber;
                }
            }

            if (!columns.ContainsKey("Name"))
            {
                result.Errors.Add(new ExcelImportErrorDto(
                    headerRow.RowNumber(),
                    $"A 'Name' column is required. Expected headers: {string.Join(", ", ImportHeaders)}."));
                return result;
            }

            if (!columns.ContainsKey("CategoryCode") && !columns.ContainsKey("CategoryName"))
            {
                result.Errors.Add(new ExcelImportErrorDto(
                    headerRow.RowNumber(),
                    "A 'CategoryCode' or 'CategoryName' column is required."));
                return result;
            }

            var lastRow = sheet.LastRowUsed()?.RowNumber() ?? headerRow.RowNumber();

            for (var rowNumber = headerRow.RowNumber() + 1; rowNumber <= lastRow; rowNumber++)
            {
                var row = sheet.Row(rowNumber);
                if (row.IsEmpty()) continue;

                var parsed = new ExcelProductRow
                {
                    RowNumber = rowNumber,
                    Name = ReadString(row, columns, "Name"),
                    Description = ReadString(row, columns, "Description"),
                    CategoryCode = ReadString(row, columns, "CategoryCode"),
                    CategoryName = ReadString(row, columns, "CategoryName")
                };

                var priceRaw = ReadString(row, columns, "Price");
                parsed.PriceRaw = priceRaw;
                parsed.Price = TryReadPrice(row, columns, priceRaw);

                // Skip rows that are entirely blank across the columns we care about.
                if (string.IsNullOrWhiteSpace(parsed.Name) &&
                    string.IsNullOrWhiteSpace(parsed.CategoryCode) &&
                    string.IsNullOrWhiteSpace(parsed.CategoryName) &&
                    string.IsNullOrWhiteSpace(priceRaw))
                {
                    continue;
                }

                result.Rows.Add(parsed);
            }
        }

        return result;
    }

    private static string? ReadString(IXLRow row, IReadOnlyDictionary<string, int> columns, string header)
    {
        if (!columns.TryGetValue(header, out var column)) return null;
        var value = row.Cell(column).GetString().Trim();
        return string.IsNullOrEmpty(value) ? null : value;
    }

    /// <summary>
    /// Reads the price as a real number where possible, falling back to parsing the
    /// displayed text so values typed as text still import.
    /// </summary>
    private static decimal? TryReadPrice(IXLRow row, IReadOnlyDictionary<string, int> columns, string? raw)
    {
        if (!columns.TryGetValue("Price", out var column)) return null;

        var cell = row.Cell(column);
        if (cell.DataType == XLDataType.Number && cell.TryGetValue<decimal>(out var numeric))
        {
            return numeric;
        }

        if (string.IsNullOrWhiteSpace(raw)) return null;

        return decimal.TryParse(
            raw,
            System.Globalization.NumberStyles.Currency | System.Globalization.NumberStyles.AllowLeadingSign,
            System.Globalization.CultureInfo.InvariantCulture,
            out var parsed)
            ? parsed
            : null;
    }

    private static byte[] ToBytes(XLWorkbook workbook)
    {
        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }
}
