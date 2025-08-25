using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using System.Collections.ObjectModel;
using System.IO;
using FanShop.Models;
using FanShop.ViewModels;
using Settings = FanShop.Models.Settings;

namespace FanShop.Services
{
    public static class PassDocumentGenerator
    {
        public static void CreateWordPass(DateTime date, ObservableCollection<EmployeeWorkInfo> employees)
        {
            string templatePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "FanShop", "болванка.docx");

            string tempPath = Path.GetTempFileName();
            string outputPath = Path.ChangeExtension(tempPath, ".docx");
            
            try
            {
                File.Copy(templatePath, outputPath, true);

                using (WordprocessingDocument wordDoc = WordprocessingDocument.Open(outputPath, true))
                {
                    var settings = Settings.Load();

                    ReplaceText(wordDoc, "{DATE}", date.ToString("dd MMMM yyyy"));
                    ReplaceText(wordDoc, "{HEAD}", settings.Head);
                    ReplaceText(wordDoc, "{RESPONSIBLE_POSITION}", settings.ResponsiblePosition);
                    ReplaceText(wordDoc, "{RESPONSIBLE_PERSON}", settings.ResponsiblePerson);
                    ReplaceText(wordDoc, "{PHONE_NUMBER}", settings.ResponsiblePhoneNumber);
                    ReplaceText(wordDoc, "{GOAL}", settings.VisitGoal);

                    var table = wordDoc.MainDocumentPart.Document.Body.Elements<Table>()
                        .FirstOrDefault(t => t.Elements<TableRow>()
                            .FirstOrDefault()?.Elements<TableCell>()
                            .Any(c => c.InnerText.Contains("№ п/п")) != null);

                    if (table != null)
                    {
                        var rows = table.Elements<TableRow>().Skip(1).ToList();
                        foreach (var row in rows)
                        {
                            row.Remove();
                        }

                        var sorted = employees
                            .OrderBy(e => e.Employee.Surname)
                            .ThenBy(e => e.Employee.FirstName)
                            .ThenBy(e => e.Employee.LastName)
                            .ToList();

                        for (int i = 0; i < sorted.Count; i++)
                        {
                            var emp = sorted[i];
                            var row = CreateRow(
                                new string[]
                                {
                                    (i + 1).ToString(),
                                    $"{emp.Surname} {emp.FirstName} {emp.Employee.LastName}",
                                    $"{emp.DateOfBirth:dd.MM.yyyy} {emp.Employee.PlaceOfBirth}",
                                    emp.Employee.Passport ?? ""
                                },
                                0.83f
                            );

                            table.Append(row);
                        }
                    }

                    wordDoc.MainDocumentPart.Document.Save();
                }
                
                var processInfo = new System.Diagnostics.ProcessStartInfo(outputPath) 
                { 
                    UseShellExecute = true 
                };
        
                var process = System.Diagnostics.Process.Start(processInfo);

                if (process != null)
                {
                    Task.Run(async () =>
                    {
                        try
                        {
                            await process.WaitForExitAsync();
                            await Task.Delay(1000);
                    
                            if (File.Exists(outputPath))
                            {
                                File.Delete(outputPath);
                            }
                            if (File.Exists(tempPath))
                            {
                                File.Delete(tempPath);
                            }
                        }
                        catch
                        { }
                    });
                }
            }
            catch (IOException ex) when (ex.Message.Contains("being used"))
            {
                outputPath = Path.Combine(Path.GetTempPath(), $"пропуск_{date:yyyyMMdd}_{Guid.NewGuid():N[..8]}.docx");
                File.Copy(templatePath, outputPath, true);
        
                CreateWordPass(date, employees);
                return;
            }
            catch (Exception)
            {
                try
                {
                    if (File.Exists(outputPath)) File.Delete(outputPath);
                    if (File.Exists(tempPath)) File.Delete(tempPath);
                }
                catch { }
                throw;
            }
        }
        
        private static TableRow CreateRow(string[] cellTexts, float minHeightInCm)
        {
            var row = new TableRow();
        
            for (int i = 0; i < cellTexts.Length; i++)
            {
                bool centerAlign = i == 0;
                row.Append(CreateCell(cellTexts[i], centerAlign));
            }
        
            uint minHeightInTwips = (uint)(minHeightInCm * 567);
        
            row.TableRowProperties = new TableRowProperties(
                new TableRowHeight() { Val = minHeightInTwips }
            );
        
            return row;
        }

        private static void ReplaceText(WordprocessingDocument wordDoc, string searchText, string replaceText)
        {
            var body = wordDoc.MainDocumentPart.Document.Body;
            var texts = body.Descendants<Text>().Where(t => t.Text.Contains(searchText)).ToList();

            foreach (var text in texts)
            {
                text.Text = text.Text.Replace(searchText, replaceText);
            }
        }

        private static TableCell CreateCell(string text, bool centerAlign = false)
        {
            var tableCellProperties = new TableCellProperties(
                new TableCellBorders(
                    new TopBorder() { Val = BorderValues.None },
                    new BottomBorder() { Val = BorderValues.None },
                    new LeftBorder() { Val = BorderValues.None },
                    new RightBorder() { Val = BorderValues.None }
                ),
                new TableCellWidth() { Type = TableWidthUnitValues.Auto },
                new TableCellMargin(
                    new LeftMargin() { Width = centerAlign ? "0" : "114", Type = TableWidthUnitValues.Dxa },
                    new RightMargin() { Width = centerAlign ? "0" : "57", Type = TableWidthUnitValues.Dxa },
                    new TopMargin() { Width = "0", Type = TableWidthUnitValues.Dxa },
                    new BottomMargin() { Width = "0", Type = TableWidthUnitValues.Dxa }
                )
            );
        
            var paragraphProperties = new ParagraphProperties(
                new Indentation() {
                    Left = "0", 
                    Right = "0",
                    FirstLine = "0",
                    Hanging = "0"
                },
                new Justification() { Val = centerAlign ? JustificationValues.Center : JustificationValues.Left },
                new SpacingBetweenLines() { After = "0", Before = "0" }
            );
        
            var runProperties = new RunProperties();
            var run = new Run(runProperties, new Text(text));
        
            var paragraph = new Paragraph(paragraphProperties, run);
        
            var tableCell = new TableCell(paragraph)
            {
                TableCellProperties = tableCellProperties
            };
        
            return tableCell;
        }
    }
}
