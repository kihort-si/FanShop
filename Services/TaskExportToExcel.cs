using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows;
using Microsoft.EntityFrameworkCore;
using OfficeOpenXml;
using OfficeOpenXml.Style;
using MessageBox = System.Windows.MessageBox;
using SaveFileDialog = Microsoft.Win32.SaveFileDialog;

namespace FanShop.Services;

public static class TaskExportToExcel
{
    public static void ExportToExcel(DateTime StartDate, DateTime EndDate)
    {
        try
        {
            ExcelPackage.License.SetNonCommercialOrganization("Зенит-Трейд"); 
    
            using var context = new AppDbContext();
    
            var tasks = context.DayTasks
                .Where(t => t.Date >= StartDate && t.Date <= EndDate)
                .Include(t => t.Category)
                .OrderBy(t => t.Date)
                .ThenBy(t => t.StartHour * 60 + t.StartMinute)
                .ToList();
    
            if (!tasks.Any())
            {
                MessageBox.Show("Нет данных для экспорта.", "Информация", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            
            string fileName = StartDate == EndDate
                ? $"Задачи_{StartDate:dd.MM.yyyy}.xlsx"
                : $"Задачи_{StartDate:dd.MM.yyyy}-{EndDate:dd.MM.yyyy}.xlsx";
    
            var saveFileDialog = new SaveFileDialog
            {
                Filter = "Excel файлы (*.xlsx)|*.xlsx",
                DefaultExt = ".xlsx",
                FileName = fileName
            };
    
            if (saveFileDialog.ShowDialog() != true)
                return;
    
            using var package = new ExcelPackage();
    
            var tasksByDate = tasks.GroupBy(t => t.Date.Date).ToList();

            foreach (var dayGroup in tasksByDate)
            {
                var date = dayGroup.Key;
                var worksheet = package.Workbook.Worksheets.Add(date.ToString("dd.MM.yyyy"));

                worksheet.Cells[1, 1].Value = "Начало";
                worksheet.Cells[1, 2].Value = "Конец";
                worksheet.Cells[1, 3].Value = "Название";
                worksheet.Cells[1, 4].Value = "Описание";
                worksheet.Cells[1, 5].Value = "Категория";
                worksheet.Cells[1, 6].Value = "Время";

                using (var range = worksheet.Cells[1, 1, 1, 6])
                {
                    range.Style.Font.Bold = true;
                    range.Style.Fill.PatternType = ExcelFillStyle.Solid;
                    range.Style.Fill.BackgroundColor.SetColor(Color.FromArgb(0, 158, 225));
                    range.Style.Font.Color.SetColor(Color.White);
                }

                int row = 2;
                foreach (var task in dayGroup)
                {
                    var startTime = new DateTime(date.Year, date.Month, date.Day, task.StartHour, task.StartMinute, 0);
                    var endTime = new DateTime(date.Year, date.Month, date.Day, task.EndHour, task.EndMinute, 0);

                    worksheet.Cells[row, 1].Value = startTime;
                    worksheet.Cells[row, 2].Value = endTime;
                    worksheet.Cells[row, 3].Value = task.Title;
                    worksheet.Cells[row, 4].Value = task.Comment;
                    worksheet.Cells[row, 5].Value = task.Category?.Name ?? "";

                    if (task.Category != null && !string.IsNullOrEmpty(task.Category.Color))
                    {
                        var colorHex = task.Category.Color.TrimStart('#');
                        if (colorHex.Length == 6)
                        {
                            int r = int.Parse(colorHex.Substring(0, 2), NumberStyles.HexNumber);
                            int g = int.Parse(colorHex.Substring(2, 2), NumberStyles.HexNumber);
                            int b = int.Parse(colorHex.Substring(4, 2), NumberStyles.HexNumber);

                            worksheet.Cells[row, 5].Style.Fill.PatternType = ExcelFillStyle.Solid;
                            worksheet.Cells[row, 5].Style.Fill.BackgroundColor.SetColor(Color.FromArgb(r, g, b));
                        }
                    }

                    var duration = endTime - startTime;
                    worksheet.Cells[row, 6].Value = duration;

                    worksheet.Cells[row, 1].Style.Numberformat.Format = "hh:mm";
                    worksheet.Cells[row, 2].Style.Numberformat.Format = "hh:mm";
                    worksheet.Cells[row, 6].Style.Numberformat.Format = "[h]:mm";

                    row++;
                }

                worksheet.Cells.AutoFitColumns();
            }

    
            if (tasksByDate.Count > 1)
            {
                var summarySheet = package.Workbook.Worksheets.Add("Сводная таблица");
    
                summarySheet.Cells[1, 1].Value = "Дата";
                summarySheet.Cells[1, 2].Value = "Начало";
                summarySheet.Cells[1, 3].Value = "Конец";
                summarySheet.Cells[1, 4].Value = "Название";
                summarySheet.Cells[1, 5].Value = "Описание";
                summarySheet.Cells[1, 6].Value = "Категория";
                summarySheet.Cells[1, 7].Value = "Время";
    
                using (var range = summarySheet.Cells[1, 1, 1, 7])
                {
                    range.Style.Font.Bold = true;
                    range.Style.Fill.PatternType = ExcelFillStyle.Solid;
                    range.Style.Fill.BackgroundColor.SetColor(Color.FromArgb(0, 158, 225));
                    range.Style.Font.Color.SetColor(Color.White);
                }
    
                int row = 2;
                foreach (var task in tasks)
                {
                    var startTime = new DateTime(task.Date.Year, task.Date.Month, task.Date.Day, task.StartHour, task.StartMinute, 0);
                    var endTime = new DateTime(task.Date.Year, task.Date.Month, task.Date.Day, task.EndHour, task.EndMinute, 0);
                    
                    summarySheet.Cells[row, 1].Value = task.Date.ToString("dd.MM.yyyy");
                    summarySheet.Cells[row, 2].Value = startTime;
                    summarySheet.Cells[row, 3].Value = endTime;
                    summarySheet.Cells[row, 4].Value = task.Title;
                    summarySheet.Cells[row, 5].Value = task.Comment;
                    summarySheet.Cells[row, 6].Value = task.Category?.Name ?? "";
    
                    if (task.Category != null && !string.IsNullOrEmpty(task.Category.Color))
                    {
                        var colorHex = task.Category.Color.TrimStart('#');
                        if (colorHex.Length == 6)
                        {
                            int r = int.Parse(colorHex.Substring(0, 2), NumberStyles.HexNumber);
                            int g = int.Parse(colorHex.Substring(2, 2), NumberStyles.HexNumber);
                            int b = int.Parse(colorHex.Substring(4, 2), NumberStyles.HexNumber);
    
                            summarySheet.Cells[row, 6].Style.Fill.PatternType = ExcelFillStyle.Solid;
                            summarySheet.Cells[row, 6].Style.Fill.BackgroundColor.SetColor(Color.FromArgb(r, g, b));
                        }
                    }
                    
                    var duration = endTime - startTime;
                    summarySheet.Cells[row, 7].Value = duration;
    
                    summarySheet.Cells[row, 2].Style.Numberformat.Format = "hh:mm";  // Начало
                    summarySheet.Cells[row, 3].Style.Numberformat.Format = "hh:mm";  // Конец
                    summarySheet.Cells[row, 7].Style.Numberformat.Format = "[h]:mm"; // Время

                    row++;
                }
    
                summarySheet.Cells.AutoFitColumns();
            }
    
            package.SaveAs(new FileInfo(saveFileDialog.FileName));
    
            MessageBox.Show("Экспорт успешно выполнен!", "Успех", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Ошибка при экспорте: {ex.Message}");
            MessageBox.Show($"Ошибка при экспорте: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
}