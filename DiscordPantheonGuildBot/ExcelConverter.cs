using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;
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
    
    public static MemoryStream XlsxToFixedLength(MemoryStream xlsxStream)
    {
        var fixedLengthStream = new MemoryStream();
        using (var spreadsheetDocument = SpreadsheetDocument.Open(xlsxStream, false))
        {
            if (spreadsheetDocument.WorkbookPart == null) throw new Exception("No workbook part found");
            WorkbookPart workbookPart = spreadsheetDocument.WorkbookPart;
            if (workbookPart.Workbook == null) throw new Exception("No workbook found");
            if (workbookPart.Workbook.Sheets == null || !workbookPart.Workbook.Sheets.Any())
                throw new Exception("No workbook sheets found");
            Sheet sheet = workbookPart.Workbook.Sheets.GetFirstChild<Sheet>() ?? throw new InvalidOperationException();

            if (string.IsNullOrWhiteSpace(sheet.Id)) throw new Exception("Nosheet id found");
            WorksheetPart worksheetPart = (WorksheetPart)workbookPart.GetPartById(sheet.Id!);
            if (worksheetPart.Worksheet == null) throw new Exception("No worksheet found");
            SheetData sheetData = worksheetPart.Worksheet.GetFirstChild<SheetData>() ??
                                  throw new InvalidOperationException();

            var rows = new List<List<string>>();
            var columnWidths = new Dictionary<int, int>();

            foreach (Row row in sheetData.Elements<Row>())
            {
                var rowData = new List<string>();
                int columnIndex = 0;
                foreach (Cell cell in row.Elements<Cell>())
                {
                    string cellValue = GetCellValue(spreadsheetDocument, cell);
                    rowData.Add(cellValue);

                    int length = cellValue.Length;
                    if (!columnWidths.ContainsKey(columnIndex) || length > columnWidths[columnIndex])
                    {
                        columnWidths[columnIndex] = length;
                    }

                    columnIndex++;
                }

                rows.Add(rowData);
            }

            var sb = new StringBuilder();
            foreach (var rowData in rows)
            {
                for (int i = 0; i < rowData.Count; i++)
                {
                    if (i < rowData.Count - 1)
                    {
                        int width = columnWidths[i] + 5;
                        sb.Append(rowData[i].PadRight(width));
                    }
                    else
                    {
                        sb.Append(rowData[i]);
                    }
                }

                sb.AppendLine();
            }

            byte[] fixedLengthBytes = Encoding.UTF8.GetBytes(sb.ToString());
            fixedLengthStream.Write(fixedLengthBytes, 0, fixedLengthBytes.Length);
            fixedLengthStream.Position = 0; // Reset stream position for reading
        }

        return fixedLengthStream;
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