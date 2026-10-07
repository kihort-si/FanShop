using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using FanShop.Models;
using FanShop.ViewModels;
using Settings = FanShop.Models.Settings;

namespace FanShop.Services
{
    public static class PassDocumentGenerator
    {
        public static bool CreateWordPass(DateTime date, ObservableCollection<EmployeeWorkInfo> employees)
        {
            return CreateWordPass(date.ToString("dd MMMM yyyy"), IsDayWeekend(date), date, employees);
        }

        public static bool CreateWordPassForDates(
            IReadOnlyCollection<DateTime> dates,
            ObservableCollection<EmployeeWorkInfo> employees)
        {
            if (dates.Count == 0)
                return false;

            var orderedDates = dates
                .Select(x => x.Date)
                .Distinct()
                .OrderBy(x => x)
                .ToList();

            var firstDate = orderedDates.First();
            var lastDate = orderedDates.Last();

            var culture = new CultureInfo("ru-RU");

            var dateText = firstDate == lastDate
                ? firstDate.ToString("d MMMM", culture)
                : $"с {firstDate.ToString("d MMMM", culture)} по {lastDate.ToString("d MMMM", culture)}";
            
            return firstDate == lastDate ? CreateWordPass(dateText, IsDayWeekend(firstDate), orderedDates[0], employees) :
                    CreateWordPass(dateText, false, orderedDates[0], employees);
        }

        private static bool CreateWordPass(
            string dateText,
            bool isWeekend,
            DateTime fileDate,
            ObservableCollection<EmployeeWorkInfo> employees)
        {
            string templatePath = PassTemplateService.TemplatePath;
            if (!File.Exists(templatePath))
            {
                return false;
            }

            string tempPath = Path.GetTempFileName();
            string outputPath = Path.ChangeExtension(tempPath, ".docx");

            try
            {
                File.Copy(templatePath, outputPath, true);

                using (WordprocessingDocument wordDoc = WordprocessingDocument.Open(outputPath, true))
                {
                    var settings = Settings.Load();

                    ReplaceText(wordDoc, "{DATE}", dateText);
                    ReplaceText(wordDoc, "{HEAD}", settings.Head);
                    ReplaceText(wordDoc, "{WEEKEND}", isWeekend ? " в выходной день" : "");
                    ReplaceText(wordDoc, "{RESPONSIBLE_POSITION}", settings.ResponsiblePosition);
                    ReplaceText(wordDoc, "{RESPONSIBLE_PERSON}", settings.ResponsiblePerson);
                    ReplaceText(wordDoc, "{PHONE_NUMBER}", settings.ResponsiblePhoneNumber);
                    ReplaceText(wordDoc, "{GOAL}", settings.VisitGoal);

                    var body = wordDoc.MainDocumentPart?.Document.Body
                        ?? throw new InvalidDataException("В шаблоне пропуска отсутствует основная часть документа. Выберите корректный DOCX-шаблон.");
                    var table = body.Elements<Table>()
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
                            .Where(e => e.IncludeInPass)
                            .OrderBy(GetEmployeeSortKey, StringComparer.Ordinal)
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
                        {
                        }
                    });
                }

                return true;
            }
            catch (IOException ex) when (ex.Message.Contains("being used"))
            {
                outputPath = Path.Combine(Path.GetTempPath(), $"пропуск_{fileDate:yyyyMMdd}_{Guid.NewGuid():N}.docx");
                File.Copy(templatePath, outputPath, true);

                return CreateWordPass(dateText, isWeekend, fileDate, employees);
            }
            catch (Exception)
            {
                try
                {
                    if (File.Exists(outputPath)) File.Delete(outputPath);
                    if (File.Exists(tempPath)) File.Delete(tempPath);
                }
                catch { }
                return false;
            }
        }

        private static bool IsDayWeekend(DateTime date)
        {
            return date.DayOfWeek == DayOfWeek.Saturday ||
                   date.DayOfWeek == DayOfWeek.Sunday;
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

        private static string GetEmployeeSortKey(EmployeeWorkInfo employee)
        {
            return $"{Normalize(employee.Surname)}\u0000{Normalize(employee.FirstName)}\u0000{Normalize(employee.Employee.LastName)}";
        }

        private static string Normalize(string? value) =>
            (value ?? string.Empty).Trim().ToUpperInvariant().Replace('Ё', 'Е');

        private static void ReplaceText(WordprocessingDocument wordDoc, string searchText, string replaceText)
        {
            var mainPart = wordDoc.MainDocumentPart;
            if (mainPart?.Document == null)
                return;

            ReplaceTextInElement(mainPart.Document, searchText, replaceText);

            foreach (var headerPart in mainPart.HeaderParts)
            {
                if (headerPart.Header != null)
                    ReplaceTextInElement(headerPart.Header, searchText, replaceText);
            }

            foreach (var footerPart in mainPart.FooterParts)
            {
                if (footerPart.Footer != null)
                    ReplaceTextInElement(footerPart.Footer, searchText, replaceText);
            }
        }

        private static void ReplaceTextInElement(
            DocumentFormat.OpenXml.OpenXmlElement root,
            string searchText,
            string replaceText)
        {
            foreach (var paragraph in root.Descendants<Paragraph>())
            {
                while (ReplaceFirstOccurrence(paragraph, searchText, replaceText))
                {
                }
            }
        }

        private static bool ReplaceFirstOccurrence(
            Paragraph paragraph,
            string searchText,
            string replaceText)
        {
            var textNodes = paragraph.Descendants<Text>().ToList();
            if (textNodes.Count == 0)
                return false;

            var paragraphText = string.Concat(textNodes.Select(x => x.Text));
            var matchIndex = paragraphText.IndexOf(searchText, StringComparison.Ordinal);
            if (matchIndex < 0)
                return false;

            var matchEndIndex = matchIndex + searchText.Length;
            var currentIndex = 0;
            Text? firstMatchedNode = null;

            foreach (var textNode in textNodes)
            {
                var nodeStart = currentIndex;
                var nodeEnd = nodeStart + textNode.Text.Length;

                if (firstMatchedNode == null &&
                    matchIndex >= nodeStart &&
                    matchIndex < nodeEnd)
                {
                    firstMatchedNode = textNode;
                }

                if (firstMatchedNode != null &&
                    nodeStart < matchEndIndex &&
                    nodeEnd > matchIndex)
                {
                    var removeStart = Math.Max(matchIndex, nodeStart) - nodeStart;
                    var removeEnd = Math.Min(matchEndIndex, nodeEnd) - nodeStart;
                    var prefix = textNode.Text[..removeStart];
                    var suffix = textNode.Text[removeEnd..];

                    textNode.Text = textNode == firstMatchedNode
                        ? prefix + replaceText + suffix
                        : suffix;

                    textNode.Space = textNode.Text.StartsWith(' ') || textNode.Text.EndsWith(' ')
                        ? DocumentFormat.OpenXml.SpaceProcessingModeValues.Preserve
                        : null;
                }

                currentIndex = nodeEnd;
            }

            return firstMatchedNode != null;
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
