using System.IO;
using UserControl = System.Windows.Controls.UserControl;

namespace FanShop.View;

public partial class FaqControl : UserControl
{
    public FaqControl()
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
}