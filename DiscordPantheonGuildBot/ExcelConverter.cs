using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;
using System.IO;
using System.Text;


namespace DiscordPantheonGuildBot;

public static class ExcelConverter
{
    public static MemoryStream XlsxToTabDelimited(MemoryStream xlsxStream)
    {
        var tsvStream = new MemoryStream();
        using (var spreadsheetDocument = SpreadsheetDocument.Open(xlsxStream, false))
        {
            if(spreadsheetDocument.WorkbookPart==null) throw new Exception("No workbook part found");
            WorkbookPart workbookPart = spreadsheetDocument.WorkbookPart;
            if(workbookPart.Workbook==null) throw new Exception("No workbook found");
            if(workbookPart.Workbook.Sheets==null || !workbookPart.Workbook.Sheets.Any()) throw new Exception("No workbook sheets found");
            Sheet sheet = workbookPart.Workbook.Sheets.GetFirstChild<Sheet>() ?? throw new InvalidOperationException();
            
            if(string.IsNullOrWhiteSpace(sheet.Id)) throw new Exception("Nosheet id found");
            WorksheetPart worksheetPart = (WorksheetPart)workbookPart.GetPartById(sheet.Id!);
            if(worksheetPart.Worksheet==null) throw new Exception("No worksheet found");
            SheetData sheetData = worksheetPart.Worksheet.GetFirstChild<SheetData>() ?? throw new InvalidOperationException();

            var sb = new StringBuilder();

            foreach (Row row in sheetData.Elements<Row>())
            {
                foreach (Cell cell in row.Elements<Cell>())
                {
                    string cellValue = GetCellValue(spreadsheetDocument, cell);
                    sb.Append(cellValue);
                    sb.Append('\t'); // Tab delimiter
                }
                sb.AppendLine(); // Newline for the next row
            }

            byte[] tsvBytes = Encoding.UTF8.GetBytes(sb.ToString());
            tsvStream.Write(tsvBytes, 0, tsvBytes.Length);
            tsvStream.Position = 0; // Reset stream position for reading
        }
        return tsvStream;
    }

    // Helper to get cell value (handles different types)
    private static string GetCellValue(SpreadsheetDocument doc, Cell cell)
    {
        if (cell.DataType != null && cell.DataType.Value == CellValues.SharedString) {
            if (cell.CellValue != null) {
                int index = int.Parse(cell.CellValue.Text);
                if (doc.WorkbookPart != null)
                    if (doc.WorkbookPart.SharedStringTablePart != null)
                        if (doc.WorkbookPart.SharedStringTablePart.SharedStringTable != null) {
                            var text = doc.WorkbookPart.SharedStringTablePart.SharedStringTable.Elements<SharedStringItem>()
                                .ElementAt(index).Text;
                            if (text != null)
                                return text.Text;
                        }
            }
        }
        return cell.CellValue?.Text ?? "";
    }
}