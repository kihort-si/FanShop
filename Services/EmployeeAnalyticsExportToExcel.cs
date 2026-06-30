using System.Drawing;
using System.Globalization;
using Microsoft.EntityFrameworkCore;
using OfficeOpenXml;
using OfficeOpenXml.Style;
using FanShop.ViewModels;

namespace FanShop.Services;

public static class EmployeeAnalyticsExportToExcel
{
    public static void ExportToExcel(
        string targetPath,
        DateTime startDate,
        DateTime endDate,
        IReadOnlyCollection<CostKpiItem> kpis,
        IReadOnlyCollection<MatchCostAnalysis> matches,
        IReadOnlyCollection<EmployeeCostSummary> employees,
        IReadOnlyCollection<WorkDayCostExplanation> workDays,
        IReadOnlyCollection<ManagementInsight> insights)
    {
        try
        {
            ExcelPackage.License.SetNonCommercialOrganization("Зенит-Трейд");

            using var package = new ExcelPackage();
            
            CreateDashboardSheet(
                package,
                startDate,
                endDate,
                kpis,
                matches,
                employees,
                workDays);

            CreateSummarySheet(
                package,
                startDate,
                endDate,
                kpis);

            CreateMatchesSheet(package, matches);
            CreateEmployeesSheet(package, employees);
            CreateWorkDaysSheet(package, workDays);
            CreateInsightsSheet(package, insights);
            
            CreateAnalysisSheet(
                package,
                matches,
                employees,
                workDays);
            
            CreateConclusionSheet(
                package,
                startDate,
                endDate,
                matches,
                employees,
                workDays);

            package.SaveAs(new FileInfo(targetPath));
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex);
        }
    }
    
    private static void CreateDashboardSheet(
        ExcelPackage package,
        DateTime startDate,
        DateTime endDate,
        IReadOnlyCollection<CostKpiItem> kpis,
        IReadOnlyCollection<MatchCostAnalysis> matches,
        IReadOnlyCollection<EmployeeCostSummary> employees,
        IReadOnlyCollection<WorkDayCostExplanation> workDays)
    {
        var ws = package.Workbook.Worksheets.Add("Дашборд");

        ws.View.ShowGridLines = false;

        ws.Cells["A1:H1"].Merge = true;
        ws.Cells["A1"].Value = "Аналитика затрат на персонал";
        ws.Cells["A1"].Style.Font.Bold = true;
        ws.Cells["A1"].Style.Font.Size = 24;

        ws.Cells["A2:H2"].Merge = true;
        ws.Cells["A2"].Value =
            $"Период: {startDate:dd.MM.yyyy} - {endDate:dd.MM.yyyy}";
        ws.Cells["A2"].Style.Font.Italic = true;

        CreateKpiCard(ws, "A4:B7", kpis.ElementAt(0));
        CreateKpiCard(ws, "C4:D7", kpis.ElementAt(1));
        CreateKpiCard(ws, "E4:F7", kpis.ElementAt(2));
        CreateKpiCard(ws, "G4:H7", kpis.ElementAt(3));

        CreateKpiCard(ws, "A9:B12", kpis.ElementAt(4));
        CreateKpiCard(ws, "C9:D12", kpis.ElementAt(5));

        ws.Column(1).Width = 18;
        ws.Column(2).Width = 18;
        ws.Column(3).Width = 18;
        ws.Column(4).Width = 18;
        ws.Column(5).Width = 18;
        ws.Column(6).Width = 18;
        ws.Column(7).Width = 18;
        ws.Column(8).Width = 18;
        
        CreateEmployeesChart(ws, employees);
        CreateMatchCostChart(ws, matches);
        CreateEmployeesPerMatchChart(ws, matches);
        CreateWorkTypeChart(ws, workDays);
    }
    
    private static void CreateKpiCard(
        ExcelWorksheet ws,
        string address,
        CostKpiItem item)
    {
        var range = ws.Cells[address];

        range.Merge = true;

        range.Style.Fill.PatternType =
            ExcelFillStyle.Solid;

        range.Style.Fill.BackgroundColor.SetColor(
            Color.FromArgb(245,248,252));

        range.Style.Border.BorderAround(
            ExcelBorderStyle.Thin,
            Color.LightGray);

        range.Style.HorizontalAlignment =
            ExcelHorizontalAlignment.Center;

        range.Style.VerticalAlignment =
            ExcelVerticalAlignment.Center;

        range.Style.WrapText = true;

        var cell = ws.Cells[address.Split(':')[0]];

        cell.Value =
            $"{item.Value}\n\n{item.Title}";
        cell.Style.Font.Bold = true;
        cell.Style.Font.Size = 16;
    }
    
    private static void CreateEmployeesChart(
        ExcelWorksheet ws,
        IReadOnlyCollection<EmployeeCostSummary> employees)
    {
        int startRow = 20;

        ws.Cells[startRow,1].Value = "Сотрудник";
        ws.Cells[startRow,2].Value = "Затраты";

        int row = startRow + 1;

        foreach (var employee in employees.OrderByDescending(x => x.TotalSalary))
        {
            ws.Cells[row,1].Value = employee.EmployeeName;
            ws.Cells[row,2].Value = (double)employee.TotalSalary;
            ws.Cells[row, 2].Style.Numberformat.Format = "#,##0 \"₽\"";
            row++;
        }

        var chart = ws.Drawings.AddBarChart(
            "EmployeesChart",
            OfficeOpenXml.Drawing.Chart.eBarChartType.BarClustered);

        chart.Title.Text = "Затраты по сотрудникам";

        chart.SetPosition(3,0,9,0);

        chart.SetSize(850,350);

        var series = chart.Series.Add(
            ws.Cells[startRow+1,2,row-1,2],
            ws.Cells[startRow+1,1,row-1,1]);

        series.Header = "Затраты";
    }
    
    private static void CreateMatchCostChart(
        ExcelWorksheet ws,
        IReadOnlyCollection<MatchCostAnalysis> matches)
    {
        const int startRow = 20;
        const int startColumn = 5;

        ws.Cells[startRow, startColumn].Value = "Матч";
        ws.Cells[startRow, startColumn + 1].Value = "Стоимость";

        int row = startRow + 1;

        foreach (var match in matches.OrderBy(m => m.MatchDate))
        {
            ws.Cells[row, startColumn].Value = match.MatchTitle;
            ws.Cells[row, startColumn + 1].Value = (double)match.TotalSalary;
            ws.Cells[row, startColumn + 1].Style.Numberformat.Format = "#,##0 \"₽\"";
            row++;
        }

        var chart = ws.Drawings.AddChart(
            "MatchCostChart",
            OfficeOpenXml.Drawing.Chart.eChartType.ColumnClustered);

        chart.Title.Text = "Стоимость подготовки матчей";

        chart.SetPosition(3, 0, 18, 0);
        chart.SetSize(700, 350);

        var series = chart.Series.Add(
            ws.Cells[startRow + 1, startColumn + 1, row - 1, startColumn + 1],
            ws.Cells[startRow + 1, startColumn, row - 1, startColumn]);

        series.Header = "Затраты";
    }
    
    private static void CreateEmployeesPerMatchChart(
        ExcelWorksheet ws,
        IReadOnlyCollection<MatchCostAnalysis> matches)
    {
        const int startRow = 55;

        ws.Cells[startRow, 1].Value = "Матч";
        ws.Cells[startRow, 2].Value = "Сотрудников";

        int row = startRow + 1;

        foreach (var match in matches.OrderBy(m => m.MatchDate))
        {
            ws.Cells[row, 1].Value = match.MatchTitle;
            ws.Cells[row, 2].Value = match.UniqueEmployees;
            row++;
        }

        var chart = ws.Drawings.AddChart(
            "EmployeesPerMatchChart",
            OfficeOpenXml.Drawing.Chart.eChartType.ColumnClustered);

        chart.Title.Text = "Количество сотрудников на матч";

        chart.SetPosition(32, 0, 0, 0);

        chart.SetSize(700, 320);

        var series = chart.Series.Add(
            ws.Cells[startRow + 1, 2, row - 1, 2],
            ws.Cells[startRow + 1, 1, row - 1, 1]);

        series.Header = "Сотрудники";
    }
    
    private static void CreateWorkTypeChart(
        ExcelWorksheet ws,
        IReadOnlyCollection<WorkDayCostExplanation> workDays)
    {
        const int row = 55;
        const int col = 5;

        var matchRelated =
            workDays.Count(x => x.RelatedMatch != "Не перед матчем");

        var nonMatch =
            workDays.Count - matchRelated;

        ws.Cells[row, col].Value = "Тип";
        ws.Cells[row, col + 1].Value = "Количество";

        ws.Cells[row + 1, col].Value = "Перед матчами";
        ws.Cells[row + 1, col + 1].Value = matchRelated;

        ws.Cells[row + 2, col].Value = "Вне матчей";
        ws.Cells[row + 2, col + 1].Value = nonMatch;

        var chart = ws.Drawings.AddChart(
            "WorkTypeChart",
            OfficeOpenXml.Drawing.Chart.eChartType.Pie);

        chart.Title.Text = "Матчевые / не матчевые смены";

        chart.SetPosition(32, 0, 18, 0);

        chart.SetSize(550, 320);

        var series = chart.Series.Add(
            ws.Cells[row + 1, col + 1, row + 2, col + 1],
            ws.Cells[row + 1, col, row + 2, col]);

        series.Header = "Смены";
    }

    private static void CreateSummarySheet(
        ExcelPackage package,
        DateTime startDate,
        DateTime endDate,
        IReadOnlyCollection<CostKpiItem> kpis)
    {
        var ws = package.Workbook.Worksheets.Add("Сводка");

        ws.Cells["A1"].Value = "Аналитика затрат на сотрудников";
        ws.Cells["A1"].Style.Font.Bold = true;
        ws.Cells["A1"].Style.Font.Size = 18;

        ws.Cells["A3"].Value = "Период";
        ws.Cells["B3"].Value =
            $"{startDate:dd.MM.yyyy} - {endDate:dd.MM.yyyy}";

        ws.Cells["A4"].Value = "Дата формирования";
        ws.Cells["B4"].Value = DateTime.Now;
        ws.Cells["B4"].Style.Numberformat.Format =
            "dd.MM.yyyy HH:mm";

        ws.Cells["A6"].Value = "Показатель";
        ws.Cells["B6"].Value = "Значение";
        ws.Cells["C6"].Value = "Описание";

        using (var range = ws.Cells["A6:C6"])
        {
            ApplyHeaderStyle(range);
        }

        var row = 7;

        foreach (var item in kpis)
        {
            ws.Cells[row, 1].Value = item.Title;
            ws.Cells[row, 2].Value = item.Value;
            ws.Cells[row, 3].Value = item.Description;

            ApplyRowStyle(ws.Cells[row, 1, row, 3]);

            row++;
        }

        ws.Cells.AutoFitColumns();

        ws.View.FreezePanes(7, 1);

        ws.Cells[6, 1, row - 1, 3].AutoFilter = true;
    }

    private static void ApplyHeaderStyle(ExcelRange range)
    {
        range.Style.Font.Bold = true;

        range.Style.Fill.PatternType =
            ExcelFillStyle.Solid;

        range.Style.Fill.BackgroundColor.SetColor(
            Color.FromArgb(0, 158, 225));

        range.Style.Font.Color.SetColor(Color.White);

        range.Style.Border.Top.Style =
            ExcelBorderStyle.Thin;

        range.Style.Border.Bottom.Style =
            ExcelBorderStyle.Thin;

        range.Style.Border.Left.Style =
            ExcelBorderStyle.Thin;

        range.Style.Border.Right.Style =
            ExcelBorderStyle.Thin;

        range.Style.HorizontalAlignment =
            ExcelHorizontalAlignment.Center;
    }

    private static void ApplyRowStyle(ExcelRange range)
    {
        range.Style.Border.Top.Style =
            ExcelBorderStyle.Thin;

        range.Style.Border.Bottom.Style =
            ExcelBorderStyle.Thin;

        range.Style.Border.Left.Style =
            ExcelBorderStyle.Thin;

        range.Style.Border.Right.Style =
            ExcelBorderStyle.Thin;

        range.Style.VerticalAlignment =
            ExcelVerticalAlignment.Center;
    }

    private static void PaintRow(
        ExcelRange range,
        string colorHex)
    {
        if (string.IsNullOrWhiteSpace(colorHex))
            return;

        colorHex = colorHex.TrimStart('#');

        if (colorHex.Length != 6)
            return;

        int r = int.Parse(
            colorHex.Substring(0, 2),
            NumberStyles.HexNumber);

        int g = int.Parse(
            colorHex.Substring(2, 2),
            NumberStyles.HexNumber);

        int b = int.Parse(
            colorHex.Substring(4, 2),
            NumberStyles.HexNumber);

        range.Style.Fill.PatternType =
            ExcelFillStyle.Solid;

        range.Style.Fill.BackgroundColor.SetColor(
            Color.FromArgb(r, g, b));
    }
    
    private static void CreateMatchesSheet(
        ExcelPackage package,
        IReadOnlyCollection<MatchCostAnalysis> matches)
    {
        var ws = package.Workbook.Worksheets.Add("Матчи");

        ws.Cells["A1"].Value = "Матч";
        ws.Cells["B1"].Value = "Подготовка";
        ws.Cells["C1"].Value = "Дней";
        ws.Cells["D1"].Value = "Рабочих дней";
        ws.Cells["E1"].Value = "Сотрудников";
        ws.Cells["F1"].Value = "Выходов";
        ws.Cells["G1"].Value = "Затраты";
        ws.Cells["H1"].Value = "Ситуация";
        ws.Cells["I1"].Value = "Описание";

        ApplyHeaderStyle(ws.Cells["A1:I1"]);

        var row = 2;

        foreach (var match in matches)
        {
            ws.Cells[row, 1].Value = match.MatchTitle;
            ws.Cells[row, 2].Value = match.PreparationWindow;
            ws.Cells[row, 3].Value = match.WindowDays;
            ws.Cells[row, 4].Value = match.WorkDays;
            ws.Cells[row, 5].Value = match.UniqueEmployees;
            ws.Cells[row, 6].Value = match.TotalShifts;
            ws.Cells[row, 7].Value = (double)match.TotalSalary;
            ws.Cells[row, 8].Value = match.Situation;
            ws.Cells[row, 9].Value = match.Explanation;

            ws.Cells[row, 7].Style.Numberformat.Format = "#,##0 ₽";

            ApplyRowStyle(ws.Cells[row, 1, row, 9]);

            if (match.IsAfterLongBreak)
            {
                PaintRow(ws.Cells[row, 1, row, 9], "#EEE9FF");
            }
            else if (match.IsPressurePeriod)
            {
                PaintRow(ws.Cells[row, 1, row, 9], "#FFF3D6");
            }

            row++;
        }

        ws.Cells.AutoFitColumns();

        ws.Column(9).Width = 60;

        ws.View.FreezePanes(2, 1);

        ws.Cells[1, 1, row - 1, 9].AutoFilter = true;
    }
    
    private static void CreateEmployeesSheet(
        ExcelPackage package,
        IReadOnlyCollection<EmployeeCostSummary> employees)
    {
        var ws = package.Workbook.Worksheets.Add("Сотрудники");

        ws.Cells["A1"].Value = "Сотрудник";
        ws.Cells["B1"].Value = "Оплачено смен";
        ws.Cells["C1"].Value = "Неоплачено";
        ws.Cells["D1"].Value = "Матчевых";
        ws.Cells["E1"].Value = "Не матчевых";
        ws.Cells["F1"].Value = "Затраты";
        ws.Cells["G1"].Value = "Комментарий";

        ApplyHeaderStyle(ws.Cells["A1:G1"]);

        var row = 2;

        foreach (var employee in employees)
        {
            ws.Cells[row, 1].Value = employee.EmployeeName;
            ws.Cells[row, 2].Value = employee.PaidShifts;
            ws.Cells[row, 3].Value = employee.UnpaidShifts;
            ws.Cells[row, 4].Value = employee.MatchRelatedShifts;
            ws.Cells[row, 5].Value = employee.NonMatchShifts;
            ws.Cells[row, 6].Value = (double)employee.TotalSalary;
            ws.Cells[row, 7].Value = employee.Explanation;

            ws.Cells[row, 6].Style.Numberformat.Format = "#,##0 ₽";

            ApplyRowStyle(ws.Cells[row, 1, row, 7]);

            if (employee.UnpaidShifts > 0)
            {
                PaintRow(ws.Cells[row, 1, row, 7], "#FFF3D6");
            }

            row++;
        }

        ws.ConditionalFormatting.AddDatabar(
            ws.Cells[2, 6, row - 1, 6].Address,
            Color.FromArgb(0, 158, 225));
        
        var maxSalary = employees.Max(x => x.TotalSalary);

        for (int i = 2; i < row; i++)
        {
            if ((double)ws.Cells[i, 6].Value == (double)maxSalary)
            {
                ws.Cells[i, 1, i, 7].Style.Font.Bold = true;
            }
        }

        ws.Cells.AutoFitColumns();

        ws.Column(7).Width = 55;

        ws.View.FreezePanes(2, 1);

        ws.Cells[1, 1, row - 1, 7].AutoFilter = true;
    }
    
    private static void CreateWorkDaysSheet(
        ExcelPackage package,
        IReadOnlyCollection<WorkDayCostExplanation> workDays)
    {
        var ws = package.Workbook.Worksheets.Add("Рабочие дни");

        ws.Cells["A1"].Value = "Дата";
        ws.Cells["B1"].Value = "Сотрудник";
        ws.Cells["C1"].Value = "Продолжительность";
        ws.Cells["D1"].Value = "В ЗП";
        ws.Cells["E1"].Value = "Сумма";
        ws.Cells["F1"].Value = "Матч";
        ws.Cells["G1"].Value = "Тип";
        ws.Cells["H1"].Value = "Контекст";

        ApplyHeaderStyle(ws.Cells["A1:H1"]);

        int row = 2;

        foreach (var item in workDays)
        {
            ws.Cells[row, 1].Value = item.Date;
            ws.Cells[row, 1].Style.Numberformat.Format = "dd.MM.yyyy";

            ws.Cells[row, 2].Value = item.EmployeeName;
            ws.Cells[row, 3].Value = item.WorkDuration;
            ws.Cells[row, 4].Value = item.IncludeInSalary ? "Да" : "Нет";
            ws.Cells[row, 5].Value = (double)item.SalaryAmount;
            ws.Cells[row, 6].Value = item.RelatedMatch;
            ws.Cells[row, 7].Value = item.WorkType;
            ws.Cells[row, 8].Value = item.Context;

            ws.Cells[row, 5].Style.Numberformat.Format = "#,##0 ₽";

            ApplyRowStyle(ws.Cells[row, 1, row, 8]);

            if (!item.IncludeInSalary)
            {
                PaintRow(ws.Cells[row, 1, row, 8], "#FDECEC");
            }
            else if (item.RelatedMatch != "Не перед матчем")
            {
                PaintRow(ws.Cells[row, 1, row, 8], "#EEF8FF");
            }

            row++;
        }

        ws.Cells.AutoFitColumns();

        ws.Column(8).Width = 80;

        ws.View.FreezePanes(2, 1);

        ws.Cells[1, 1, row - 1, 8].AutoFilter = true;
    }
    
    private static void CreateInsightsSheet(
        ExcelPackage package,
        IReadOnlyCollection<ManagementInsight> insights)
    {
        var ws = package.Workbook.Worksheets.Add("Управленческие выводы");

        ws.Cells["A1"].Value = "Управленческие выводы";
        ws.Cells["A1"].Style.Font.Size = 18;
        ws.Cells["A1"].Style.Font.Bold = true;

        int row = 3;

        foreach (var insight in insights)
        {
            ws.Cells[row, 1].Value = insight.Title;
            ws.Cells[row, 1].Style.Font.Bold = true;
            ws.Cells[row, 1].Style.Font.Size = 14;

            ws.Cells[row, 2].Value = insight.Severity;

            ws.Cells[row, 2].Style.Font.Bold = true;
            ws.Cells[row, 2].Style.HorizontalAlignment =
                ExcelHorizontalAlignment.Center;

            switch (insight.Severity)
            {
                case "Высокий приоритет":
                    PaintRow(ws.Cells[row, 1, row, 2], "#FDECEC");
                    break;

                case "Особый контроль":
                    PaintRow(ws.Cells[row, 1, row, 2], "#FFF3D6");
                    break;

                case "Контроль":
                    PaintRow(ws.Cells[row, 1, row, 2], "#EEF8FF");
                    break;

                default:
                    PaintRow(ws.Cells[row, 1, row, 2], "#F4F4F4");
                    break;
            }

            row++;

            ws.Cells[row, 1].Value = insight.Description;

            ws.Cells[row, 1, row, 2].Merge = true;
            ws.Cells[row, 1].Style.WrapText = true;
            ws.Cells[row, 1].Style.VerticalAlignment =
                ExcelVerticalAlignment.Top;

            ApplyRowStyle(ws.Cells[row, 1, row, 2]);

            row += 2;
        }

        ws.Column(1).Width = 85;
        ws.Column(2).Width = 22;
    }
    
    private static void CreateAnalysisSheet(
        ExcelPackage package,
        IReadOnlyCollection<MatchCostAnalysis> matches,
        IReadOnlyCollection<EmployeeCostSummary> employees,
        IReadOnlyCollection<WorkDayCostExplanation> workDays)
    {
        var ws = package.Workbook.Worksheets.Add("Анализ");

        ws.View.ShowGridLines = false;

        ws.Cells["A1:D1"].Merge = true;
        ws.Cells["A1"].Value = "Основные показатели";
        ws.Cells["A1"].Style.Font.Size = 22;
        ws.Cells["A1"].Style.Font.Bold = true;

        int row = 3;

        AddAnalysisItem(
            ws,
            ref row,
            "Самый дорогой матч",
            matches.OrderByDescending(x => x.TotalSalary).FirstOrDefault()?.MatchTitle ?? "-");

        AddAnalysisItem(
            ws,
            ref row,
            "Стоимость этого матча",
            $"{matches.Max(x => x.TotalSalary):N0} ₽");

        AddAnalysisItem(
            ws,
            ref row,
            "Самый загруженный сотрудник",
            employees.OrderByDescending(x => x.TotalSalary).FirstOrDefault()?.EmployeeName ?? "-");

        AddAnalysisItem(
            ws,
            ref row,
            "Его затраты",
            $"{employees.Max(x => x.TotalSalary):N0} ₽");

        AddAnalysisItem(
            ws,
            ref row,
            "Средняя стоимость подготовки матча",
            $"{matches.Average(x => x.TotalSalary):N0} ₽");

        AddAnalysisItem(
            ws,
            ref row,
            "Среднее количество сотрудников",
            $"{matches.Average(x => x.UniqueEmployees):N1}");

        AddAnalysisItem(
            ws,
            ref row,
            "Средняя стоимость рабочего дня",
            $"{workDays.Average(x => x.SalaryAmount):N0} ₽");

        AddAnalysisItem(
            ws,
            ref row,
            "Всего сотрудников",
            employees.Count.ToString());

        AddAnalysisItem(
            ws,
            ref row,
            "Всего смен",
            workDays.Count.ToString());

        AddAnalysisItem(
            ws,
            ref row,
            "Неоплачиваемых смен",
            workDays.Count(x => !x.IncludeInSalary).ToString());

        double percent =
            workDays.Count == 0
                ? 0
                : workDays.Count(x => !x.IncludeInSalary) * 100.0 / workDays.Count;

        AddAnalysisItem(
            ws,
            ref row,
            "Доля неоплачиваемых смен",
            $"{percent:F1}%");
        
        row += 2;

        ws.Cells[row,1].Value = "ТОП сотрудников";
        ws.Cells[row,1].Style.Font.Bold = true;
        ws.Cells[row,1].Style.Font.Size = 16;

        row += 2;

        ws.Cells[row,1].Value = "Сотрудник";
        ws.Cells[row,2].Value = "Затраты";

        ApplyHeaderStyle(
            ws.Cells[row,1,row,2]);

        row++;

        foreach (var employee in employees
                     .OrderByDescending(x => x.TotalSalary)
                     .Take(5))
        {
            ws.Cells[row,1].Value =
                employee.EmployeeName;

            ws.Cells[row,2].Value =
                (double)employee.TotalSalary;

            ws.Cells[row,2].Style.Numberformat.Format =
                "#,##0 ₽";

            ApplyRowStyle(
                ws.Cells[row,1,row,2]);

            row++;
        }
        
        row += 2;

        ws.Cells[row,1].Value = "Самые дорогие матчи";

        ws.Cells[row,1].Style.Font.Bold = true;

        ws.Cells[row,1].Style.Font.Size = 16;

        row += 2;

        ws.Cells[row,1].Value = "Матч";
        ws.Cells[row,2].Value = "Стоимость";

        ApplyHeaderStyle(
            ws.Cells[row,1,row,2]);

        row++;

        foreach (var match in matches
                     .OrderByDescending(x => x.TotalSalary)
                     .Take(5))
        {
            ws.Cells[row,1].Value =
                match.MatchTitle;

            ws.Cells[row,2].Value =
                (double)match.TotalSalary;

            ws.Cells[row,2].Style.Numberformat.Format =
                "#,##0 ₽";

            ApplyRowStyle(
                ws.Cells[row,1,row,2]);

            row++;
        }

        ws.Column(1).Width = 40;
        ws.Column(2).Width = 25;
    }
    
    private static void AddAnalysisItem(
        ExcelWorksheet ws,
        ref int row,
        string title,
        string value)
    {
        ws.Cells[row,1].Value = title;

        ws.Cells[row,1].Style.Font.Bold = true;

        ws.Cells[row,2].Value = value;

        ws.Cells[row,2].Style.HorizontalAlignment =
            ExcelHorizontalAlignment.Right;

        ApplyRowStyle(
            ws.Cells[row,1,row,2]);

        row++;
    }

    private static void CreateConclusionSheet(
        ExcelPackage package,
        DateTime startDate,
        DateTime endDate,
        IReadOnlyCollection<MatchCostAnalysis> matches,
        IReadOnlyCollection<EmployeeCostSummary> employees,
        IReadOnlyCollection<WorkDayCostExplanation> workDays)
    {
        var ws = package.Workbook.Worksheets.Add("Заключение");

        ws.View.ShowGridLines = false;

        ws.Cells["A1"].Value = "Заключение";
        ws.Cells["A1"].Style.Font.Bold = true;
        ws.Cells["A1"].Style.Font.Size = 24;

        ws.Cells["A3"].Value =
            $"Отчет сформирован за период {startDate:dd.MM.yyyy} — {endDate:dd.MM.yyyy}.";

        int row = 5;

        WriteParagraph(
            ws,
            ref row,
            $"Всего проанализировано {matches.Count} матчей, " +
            $"{employees.Count} сотрудников и " +
            $"{workDays.Count} рабочих смен.");
        
        var expensiveMatch =
            matches.OrderByDescending(x => x.TotalSalary).FirstOrDefault();

        if (expensiveMatch != null)
        {
            WriteParagraph(
                ws,
                ref row,
                $"Наибольшие затраты пришлись на матч «{expensiveMatch.MatchTitle}». " +
                $"Стоимость подготовки составила {expensiveMatch.TotalSalary:N0} ₽.");
        }
        
        WriteParagraph(
            ws,
            ref row,
            $"Средняя стоимость подготовки одного матча составила " +
            $"{matches.Average(x=>x.TotalSalary):N0} ₽.");
        
        var employee =
            employees.OrderByDescending(x=>x.TotalSalary).First();

        WriteParagraph(
            ws,
            ref row,
            $"Наибольшая нагрузка наблюдалась у сотрудника " +
            $"{employee.EmployeeName}. " +
            $"Общие затраты составили {employee.TotalSalary:N0} ₽.");
        
        int pressure =
            matches.Count(x=>x.IsPressurePeriod);

        if (pressure > 0)
        {
            WriteParagraph(
                ws,
                ref row,
                $"Обнаружено {pressure} напряженных периодов подготовки, " +
                $"в которых интервалы между матчами были менее пяти рабочих дней.");
        }
        
        int breaks =
            matches.Count(x=>x.IsAfterLongBreak);

        if (breaks > 0)
        {
            WriteParagraph(
                ws,
                ref row,
                $"Зафиксировано {breaks} подготовительных окна после длительных перерывов. " +
                $"Для подобных периодов характерно увеличение объема поставок и подготовительных работ.");
        }
        
        int unpaid =
            workDays.Count(x=>!x.IncludeInSalary);

        if (unpaid > 0)
        {
            WriteParagraph(
                ws,
                ref row,
                $"За период зарегистрировано {unpaid} смен без начисления заработной платы. " +
                $"Их рекомендуется учитывать отдельно при анализе фактической загрузки сотрудников.");
        }
        
        WriteParagraph(
            ws,
            ref row,
            "Полученные данные позволяют оценить влияние календаря матчей " +
            "на загрузку персонала и затраты предприятия. " +
            "Отчет рекомендуется использовать при планировании графиков работы, " +
            "формировании бюджета и оценке эффективности подготовки к матчам.");

        ws.Column(1).Width = 120;
    }
    
    private static void WriteParagraph(
        ExcelWorksheet ws,
        ref int row,
        string text)
    {
        ws.Cells[row,1].Value = text;

        ws.Cells[row,1,row,6].Merge = true;

        ws.Cells[row,1].Style.WrapText = true;

        ws.Cells[row,1].Style.VerticalAlignment =
            ExcelVerticalAlignment.Top;

        ws.Cells[row,1].Style.Font.Size = 12;

        row += 3;
    }
}