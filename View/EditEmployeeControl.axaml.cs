using Avalonia.Controls;
using Avalonia.Input;

namespace FanShop.View;

public partial class EditEmployeeControl : UserControl
{
    private bool _isFormattingDateOfBirth;

    public EditEmployeeControl()
    {
        InitializeComponent();
    }

    private void DateOfBirthTextBox_TextChanged(object? sender, TextChangedEventArgs e)
    {
        if (_isFormattingDateOfBirth || sender is not TextBox textBox)
        {
            return;
        }

        var digits = new string((textBox.Text ?? string.Empty).Where(char.IsDigit).Take(8).ToArray());
        var formatted = FormatDateOfBirth(digits);

        if (textBox.Text == formatted)
        {
            return;
        }

        _isFormattingDateOfBirth = true;
        textBox.Text = formatted;
        textBox.CaretIndex = formatted.Length;
        _isFormattingDateOfBirth = false;
    }

    private static string FormatDateOfBirth(string digits)
    {
        return digits.Length switch
        {
            <= 2 => digits,
            <= 4 => $"{digits[..2]}.{digits[2..]}",
            _ => $"{digits[..2]}.{digits[2..4]}.{digits[4..]}"
        };
    }

    private void InputTextBox_KeyDown(object? sender, KeyEventArgs e)
    {
        var direction = e.Key switch
        {
            Key.Down => 1,
            Key.Up => -1,
            _ => 0
        };

        if (direction == 0 || sender is not TextBox currentTextBox)
        {
            return;
        }

        var inputOrder = new[]
        {
            SurnameTextBox,
            FirstNameTextBox,
            LastNameTextBox,
            DateOfBirthTextBox,
            PlaceOfBirthTextBox,
            PassportTextBox
        };

        var currentIndex = Array.IndexOf(inputOrder, currentTextBox);
        var nextIndex = currentIndex + direction;
        if (currentIndex < 0 || nextIndex < 0 || nextIndex >= inputOrder.Length)
        {
            return;
        }

        var nextTextBox = inputOrder[nextIndex];
        nextTextBox.Focus();
        nextTextBox.CaretIndex = nextTextBox.Text?.Length ?? 0;
        e.Handled = true;
    }
}
