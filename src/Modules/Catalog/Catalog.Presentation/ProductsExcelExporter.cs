using Catalog.Application.Contracts;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;

namespace Catalog.Presentation;

/// <summary>
/// Single-worksheet xlsx export for <see cref="ProductDto"/> using the
/// OpenXML SDK. Columns: Id, Name, Price, Stock, CreatedAt.
/// Strings are written inline (no shared-strings table) to keep the writer
/// streaming-friendly and easy to follow.
/// </summary>
public static class ProductsExcelExporter
{
    public static byte[] ToExcel(IReadOnlyList<ProductDto> products)
    {
        using var ms = new MemoryStream();
        using (var doc = SpreadsheetDocument.Create(ms, SpreadsheetDocumentType.Workbook))
        {
            var workbookPart = doc.AddWorkbookPart();
            workbookPart.Workbook = new Workbook();

            var sheetPart = workbookPart.AddNewPart<WorksheetPart>();
            var sheetData = new SheetData();
            sheetPart.Worksheet = new Worksheet(sheetData);

            var sheets = workbookPart.Workbook.AppendChild(new Sheets());
            sheets.Append(new Sheet
            {
                Id = workbookPart.GetIdOfPart(sheetPart),
                SheetId = 1U,
                Name = "Products",
            });

            sheetData.Append(HeaderRow("Id", "Name", "Price", "Stock", "CreatedAt"));
            foreach (var p in products)
            {
                sheetData.Append(DataRow(
                    p.Id.ToString(),
                    p.Name,
                    p.Price.ToString(System.Globalization.CultureInfo.InvariantCulture),
                    p.Stock.ToString(System.Globalization.CultureInfo.InvariantCulture),
                    p.CreatedAt.ToString("O", System.Globalization.CultureInfo.InvariantCulture)));
            }

            workbookPart.Workbook.Save();
        }
        return ms.ToArray();
    }

    private static Row HeaderRow(params string[] headers) => DataRow(headers);

    private static Row DataRow(params string[] values)
    {
        var row = new Row();
        foreach (var v in values)
        {
            row.Append(new Cell
            {
                DataType = CellValues.String,
                CellValue = new CellValue(v ?? string.Empty),
            });
        }
        return row;
    }
}
