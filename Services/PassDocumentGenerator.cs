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
            => CreateWordPassForDates(new[] { date }, employees);

        public static bool CreateWordPassForDates(
            IReadOnlyCollection<DateTime> dates,
            ObservableCollection<EmployeeWorkInfo> employees)
            => CreateWordPassForDates(dates, employees, out _);

        public static bool CreateWordPassForDates(IReadOnlyCollection<DateTime> dates,
            ObservableCollection<EmployeeWorkInfo> employees, out string? error)
        {
            return TryCreatePassDocuments(PlanPasses(dates).Select(plan =>
                new EmployeePassPlan(plan, employees, Array.Empty<int>())).ToArray(), out error);
        }

        public static bool CreateWordPass(EmployeePassPlan plan, out string? error)
            => TryCreatePassDocuments(new[] { plan }, out error);

        private static bool TryCreatePassDocuments(IReadOnlyCollection<EmployeePassPlan> plans, out string? error)
        {
            error = null;
            if (plans.Count == 0 || !plans.Any(p => p.Employees.Any(e => e.IncludeInPass)))
            {
                error = "Не выбраны даты или сотрудники с отметкой «Пропуск».";
                return false;
            }
            try
            {
                var directory = Path.Combine(Path.GetTempPath(), "FanShop-passes-" + Guid.NewGuid().ToString("N"));
                var paths = WritePassDocuments(PassTemplateService.TemplatePath, directory, plans, Settings.Load());
                foreach (var path in paths)
                    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(path) { UseShellExecute = true });
                return paths.Count > 0;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                System.Diagnostics.Trace.WriteLine($"Ошибка формирования пропуска: {ex}");
                return false;
            }
        }

        public static IReadOnlyList<string> WritePassDocuments(string templatePath, string outputDirectory,
            IEnumerable<DateTime> dates, IReadOnlyCollection<EmployeeWorkInfo> employees, Settings settings)
            => WritePassDocuments(templatePath, outputDirectory,
                PlanPasses(dates).Select(plan => new EmployeePassPlan(plan, employees, Array.Empty<int>())).ToArray(), settings);

        public static IReadOnlyList<string> WritePassDocuments(string templatePath, string outputDirectory,
            IReadOnlyCollection<EmployeePassPlan> plans, Settings settings)
        {
            if (plans.Count == 0) return Array.Empty<string>();
            if (!File.Exists(templatePath)) throw new FileNotFoundException("Не найден шаблон пропуска.", templatePath);
            Directory.CreateDirectory(outputDirectory);
            var paths = new List<string>();
            try
            {
                foreach (var document in plans.Where(p => p.Employees.Any(e => e.IncludeInPass)))
                {
                    var plan = document.Pass;
                    var kind = plan.IsWeekend ? "выходные" : "общий";
                    var outputPath = Path.Combine(outputDirectory, $"пропуск_{kind}_{Guid.NewGuid():N}.docx");
                    paths.Add(outputPath);
                    File.Copy(templatePath, outputPath);
                    WritePassDocument(outputPath, plan, document.Employees, settings);
                }
                return paths;
            }
            catch
            {
                foreach (var path in paths)
                    if (File.Exists(path)) File.Delete(path);
                throw;
            }
        }

        public sealed record PassPlan(DateTime[] Dates, bool IsWeekend, string DateText);

        public static IReadOnlyList<PassPlan> PlanPasses(IEnumerable<DateTime> dates)
        {
            var ordered = dates.Select(d => d.Date).Distinct().OrderBy(d => d).ToArray();
            if (ordered.Length == 0) return Array.Empty<PassPlan>();
            var plans = new List<PassPlan>();
            var weekdays = ordered.Where(d => !IsDayWeekend(d)).ToArray();
            var weekends = ordered.Where(IsDayWeekend).ToArray();
            if (weekdays.Length > 0) plans.Add(new(weekdays, false, FormatDates(weekdays)));
            if (weekends.Length > 0) plans.Add(new(weekends, true, FormatDates(weekends)));
            return plans;
        }

        public sealed record EmployeePassPlan(PassPlan Pass, IReadOnlyCollection<EmployeeWorkInfo> Employees,
            IReadOnlyCollection<int> AssignmentIds);

        public static IReadOnlyList<EmployeePassPlan> PlanAssignmentPasses(IEnumerable<WorkDayEmployee> assignments)
        {
            var result = new List<EmployeePassPlan>();
            foreach (var category in assignments.Where(a => a.IncludeInPass)
                .GroupBy(a => IsDayWeekend(a.WorkDay.Date)).OrderBy(g => g.Key))
            {
                var groups = category.GroupBy(a => a.EmployeeID)
                    .Select(g => new { Employee = g.First().Employee, Rows = g.ToArray(),
                        Dates = g.Select(a => a.WorkDay.Date.Date).Distinct().OrderBy(d => d).ToArray() })
                    .GroupBy(e => string.Join("|", e.Dates.Select(d => d.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture))));
                foreach (var group in groups)
                {
                    var dates = group.First().Dates;
                    var employees = group.Select(e => new EmployeeWorkInfo { Employee = e.Employee, IncludeInPass = true })
                        .OrderBy(GetEmployeeSortKey, StringComparer.Ordinal).ToArray();
                    result.Add(new(new(dates, category.Key, FormatDates(dates)), employees,
                        group.SelectMany(e => e.Rows).Select(a => a.WorkDayEmployeeID).Distinct().ToArray()));
                }
            }
            return result;
        }

        private static string FormatDates(DateTime[] dates)
        {
            var culture = CultureInfo.GetCultureInfo("ru-RU");
            if (dates.Length == 1) return dates[0].ToString("d MMMM yyyy", culture);
            var consecutive = dates.Zip(dates.Skip(1), (a, b) => b == a.AddDays(1)).All(x => x);
            if (!consecutive) return string.Join(", ", dates.Select(d => d.ToString("d MMMM yyyy", culture)));
            var startFormat = dates[0].Year == dates[^1].Year ? "d MMMM" : "d MMMM yyyy";
            return $"с {dates[0].ToString(startFormat, culture)} по {dates[^1].ToString("d MMMM yyyy", culture)}";
        }

        private static void WritePassDocument(string outputPath, PassPlan plan,
            IReadOnlyCollection<EmployeeWorkInfo> employees, Settings settings)
        {
            using (WordprocessingDocument wordDoc = WordprocessingDocument.Open(outputPath, true))
            {
                ReplaceText(wordDoc, "{DATE}", plan.DateText);
                ReplaceText(wordDoc, "{HEAD_POSITION}", settings.HeadPosition);
                ReplaceText(wordDoc, "{HEAD}", settings.Head);
                ReplaceText(wordDoc, "{WEEKEND}", plan.IsWeekend ? " в выходной день" : "");
                ReplaceText(wordDoc, "{RESPONSIBLE_POSITION}", settings.ResponsiblePosition);
                ReplaceText(wordDoc, "{RESPONSIBLE_PERSON}", settings.ResponsiblePerson);
                ReplaceText(wordDoc, "{PHONE_NUMBER}", settings.ResponsiblePhoneNumber);
                ReplaceText(wordDoc, "{GOAL}", settings.VisitGoal);

                var body = wordDoc.MainDocumentPart?.Document.Body
                    ?? throw new InvalidDataException("В шаблоне пропуска отсутствует основная часть документа. Выберите корректный DOCX-шаблон.");
                var table = body.Descendants<Table>()
                    .FirstOrDefault(t => t.Elements<TableRow>()
                        .FirstOrDefault()?.Elements<TableCell>()
                        .Any(c => string.Concat(c.InnerText.Where(ch => !char.IsWhiteSpace(ch)))
                            .Contains("№п/п", StringComparison.OrdinalIgnoreCase)) == true);

                if (table == null) throw new InvalidDataException("В шаблоне пропуска не найдена таблица сотрудников с заголовком «№ п/п».");
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
