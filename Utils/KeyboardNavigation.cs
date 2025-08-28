using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using KeyEventArgs = System.Windows.Input.KeyEventArgs;

namespace FanShop.Utils;

public static class KeyboardNavigation
{
    public static readonly DependencyProperty EnableArrowNavigationProperty =
        DependencyProperty.RegisterAttached(
            "EnableArrowNavigation",
            typeof(bool),
            typeof(KeyboardNavigation),
            new PropertyMetadata(false, OnEnableArrowNavigationChanged));

    public static bool GetEnableArrowNavigation(DependencyObject obj)
    {
        return (bool)obj.GetValue(EnableArrowNavigationProperty);
    }

    public static void SetEnableArrowNavigation(DependencyObject obj, bool value)
    {
        obj.SetValue(EnableArrowNavigationProperty, value);
    }

    private static void OnEnableArrowNavigationChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is UIElement element)
        {
            if ((bool)e.NewValue)
            {
                element.PreviewKeyDown += Element_PreviewKeyDown;
            }
            else
            {
                element.PreviewKeyDown -= Element_PreviewKeyDown;
            }
        }
    }

    private static void Element_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        UIElement element = (UIElement)sender;

        if (e.Key == Key.Up)
        {
            element.MoveFocus(new TraversalRequest(FocusNavigationDirection.Previous));
            e.Handled = true;
        }
        else if (e.Key == Key.Down)
        {
            element.MoveFocus(new TraversalRequest(FocusNavigationDirection.Next));
            e.Handled = true;
        }
    }
    
    public static readonly DependencyProperty EnableArrowFocusNavigationProperty =
        DependencyProperty.RegisterAttached(
            "EnableArrowFocusNavigation",
            typeof(bool),
            typeof(KeyboardNavigation),
            new PropertyMetadata(false, OnEnableArrowFocusNavigationChanged));

    public static void SetEnableArrowFocusNavigation(UIElement element, bool value) =>
        element.SetValue(EnableArrowFocusNavigationProperty, value);

    public static bool GetEnableArrowFocusNavigation(UIElement element) =>
        (bool)element.GetValue(EnableArrowFocusNavigationProperty);

    private static void OnEnableArrowFocusNavigationChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is DatePicker datePicker && (bool)e.NewValue)
        {
            datePicker.Loaded += (_, _) =>
            {
                var textBox = GetChildOfType<DatePickerTextBox>(datePicker);
                if (textBox != null)
                {
                    textBox.PreviewKeyDown += (s, args) =>
                    {
                        if (args.Key == Key.Down)
                        {
                            args.Handled = true;

                            var request = new TraversalRequest(FocusNavigationDirection.Next);
                            var elementWithFocus = Keyboard.FocusedElement as UIElement;
                            elementWithFocus?.MoveFocus(request);
                        }
                        else if (args.Key == Key.Up)
                        {
                            args.Handled = true;

                            var request = new TraversalRequest(FocusNavigationDirection.Previous);
                            var elementWithFocus = Keyboard.FocusedElement as UIElement;
                            elementWithFocus?.MoveFocus(request);
                        }
                    };
                }
            };
        }
    }

    private static T GetChildOfType<T>(DependencyObject depObj) where T : DependencyObject
    {
        if (depObj == null) return null;

        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(depObj); i++)
        {
            var child = VisualTreeHelper.GetChild(depObj, i);
            if (child is T t) return t;

            var result = GetChildOfType<T>(child);
            if (result != null) return result;
        }
        return null;
    }
}