using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;

public class ExcelHelper
{
    // A generic method to create an Excel file from a List of objects
    public static MemoryStream CreateExcelStream<T>(List<T> dataList, string sheetName = "Data")
    {
        var memoryStream = new MemoryStream();

        // Create a spreadsheet document in the memory stream
        using (SpreadsheetDocument document = SpreadsheetDocument.Create(memoryStream, SpreadsheetDocumentType.Workbook, true))
        {
            WorkbookPart workbookPart = document.AddWorkbookPart();
            workbookPart.Workbook = new Workbook();

            WorksheetPart worksheetPart = workbookPart.AddNewPart<WorksheetPart>();
            SheetData sheetData = new SheetData();
            worksheetPart.Worksheet = new Worksheet(sheetData);

            Sheets sheets = workbookPart.Workbook.AppendChild(new Sheets());
            Sheet sheet = new Sheet()
            {
                Id = workbookPart.GetIdOfPart(worksheetPart),
                SheetId = 1,
                Name = sheetName
            };
            sheets.Append(sheet);

            // Get properties for headers and data
            var properties = typeof(T).GetProperties();

            // Add header row
            Row headerRow = new Row();
            foreach (var prop in properties)
            {
                headerRow.AppendChild(CreateCell(prop.Name, CellValues.String));
            }
            sheetData.AppendChild(headerRow);

            // Add data rows
            foreach (var item in dataList)
            {
                Row dataRow = new Row();
                foreach (var prop in properties)
                {
                    var value = prop.GetValue(item)?.ToString() ?? string.Empty;
                    var dataType = GetCellDataType(prop.PropertyType);
                    dataRow.AppendChild(CreateCell(value, dataType));
                }
                sheetData.AppendChild(dataRow);
            }

            workbookPart.Workbook.Save();
            // Closing the document is essential to flush all data to the stream

        }

        // Reset the stream position to the beginning so it can be read for the attachment
        memoryStream.Position = 0;
        return memoryStream;
    }

    private static Cell CreateCell(string text, CellValues dataType)
    {
        Cell cell = new Cell()
        {
            CellValue = new CellValue(text),
            DataType = new EnumValue<CellValues>(dataType)
        };
        return cell;
    }

    private static CellValues GetCellDataType(Type propertyType)
    {
        if (propertyType == typeof(int) || propertyType == typeof(double) || propertyType == typeof(decimal) || propertyType == typeof(float))
        {
            return CellValues.Number;
        }
        return CellValues.String;
    }
}