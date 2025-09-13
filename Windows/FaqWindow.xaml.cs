using System.IO;
using System.Windows;
using System.Windows.Input;
using FanShop.Utils;

namespace FanShop.Windows;

public partial class FaqWindow : Window
{
    public FaqWindow()
    {
        InitializeComponent();
        
        string docPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "FanShop", "болванка.docx");
        
        HelpPathTextBlock.Text =
            "Для изменения шаблона пропуска:\n" +
            "1. Откройте меню \"Настройки\"\n" +
            "2. Измените доступные параметры шаблона пропуска\n" +
            "3. Нажмите \"Сохранить\" для применения изменений\n\n" +
            $"Примечание: Для более глубокой кастомизации шаблона пропуска можно отредактировать файл \"болванка.docx\", находящийся по пути: {docPath}.";

    }
    
    private void Border_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.LeftButton == MouseButtonState.Pressed)
        {
            DragMove();
        }
    }
    
    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        Close();
        OpenWindowsController.Unregister(this);
    }
}