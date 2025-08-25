using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using KeyEventArgs = System.Windows.Input.KeyEventArgs;
using ListBox = System.Windows.Controls.ListBox;
using TextBox = System.Windows.Controls.TextBox;

namespace FanShop.Controls
{
    public class AutoCompleteTextBox : TextBox
    {
        private Popup _suggestionsPopup;
        private ListBox _suggestionsList;

        public static readonly DependencyProperty SuggestionsProperty = DependencyProperty.Register(
            "Suggestions", typeof(IEnumerable<string>), typeof(AutoCompleteTextBox),
            new PropertyMetadata(null));

        public IEnumerable<string> Suggestions
        {
            get => (IEnumerable<string>)GetValue(SuggestionsProperty);
            set => SetValue(SuggestionsProperty, value);
        }

        public AutoCompleteTextBox()
        {
            _suggestionsPopup = new Popup
            {
                PlacementTarget = this,
                Placement = PlacementMode.Bottom,
                StaysOpen = false,
                IsOpen = false
            };

            _suggestionsList = new ListBox
            {
                MaxHeight = 150,
                Width = 300,
                BorderThickness = new Thickness(1),
                BorderBrush = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0xCC, 0xCC, 0xCC)),
                Background = System.Windows.Media.Brushes.White
            };

            _suggestionsList.SelectionChanged += SuggestionsList_SelectionChanged;
            _suggestionsList.PreviewMouseDown += SuggestionsList_PreviewMouseDown;

            _suggestionsPopup.Child = _suggestionsList;

            TextChanged += AutoCompleteTextBox_TextChanged;
            PreviewKeyDown += AutoCompleteTextBox_PreviewKeyDown;
            LostFocus += AutoCompleteTextBox_LostFocus;
        }

        private void AutoCompleteTextBox_LostFocus(object sender, RoutedEventArgs e)
        {
            Dispatcher.BeginInvoke(new System.Action(() =>
            {
                if (!_suggestionsList.IsKeyboardFocusWithin)
                {
                    _suggestionsPopup.IsOpen = false;
                }
            }), System.Windows.Threading.DispatcherPriority.Input);
        }

        private void SuggestionsList_PreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            var item = ItemsControl.ContainerFromElement(_suggestionsList, e.OriginalSource as DependencyObject) as ListBoxItem;
            if (item != null)
            {
                Text = item.Content.ToString();
                _suggestionsPopup.IsOpen = false;
                CaretIndex = Text.Length;
                Focus();
            }
        }

        private void SuggestionsList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_suggestionsList.SelectedItem != null)
            {
                Text = _suggestionsList.SelectedItem.ToString();
                CaretIndex = Text.Length;
            }
        }

        private void AutoCompleteTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            UpdateSuggestions();
        }

        private void AutoCompleteTextBox_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (_suggestionsPopup.IsOpen)
            {
                if (e.Key == Key.Down)
                {
                    if (_suggestionsList.SelectedIndex < _suggestionsList.Items.Count - 1)
                    {
                        _suggestionsList.SelectedIndex++;
                    }
                    else
                    {
                        _suggestionsList.SelectedIndex = 0; 
                    }
                    _suggestionsList.ScrollIntoView(_suggestionsList.SelectedItem);
                    e.Handled = true;
                }
                else if (e.Key == Key.Up)
                {
                    if (_suggestionsList.SelectedIndex > 0)
                    {
                        _suggestionsList.SelectedIndex--;
                    }
                    else
                    {
                        _suggestionsList.SelectedIndex = _suggestionsList.Items.Count - 1;
                    }
                    _suggestionsList.ScrollIntoView(_suggestionsList.SelectedItem);
                    e.Handled = true;
                }
                else if (e.Key == Key.Escape)
                {
                    _suggestionsPopup.IsOpen = false;
                    e.Handled = true;
                }
                else if (e.Key == Key.Enter || e.Key == Key.Tab)
                {
                    if (_suggestionsList.SelectedItem != null)
                    {
                        Text = _suggestionsList.SelectedItem.ToString();
                        _suggestionsPopup.IsOpen = false;
                        CaretIndex = Text.Length;
                    }
                    else if (_suggestionsList.Items.Count > 0)
                    {
                        Text = _suggestionsList.Items[0].ToString();
                        _suggestionsPopup.IsOpen = false;
                        CaretIndex = Text.Length;
                    }
                    
                    if (e.Key == Key.Enter)
                        e.Handled = true;
                }
            }
            else if (e.Key == Key.Down && !string.IsNullOrEmpty(Text) && Suggestions != null)
            {
                UpdateSuggestions();
                if (_suggestionsList.Items.Count > 0)
                {
                    _suggestionsPopup.IsOpen = true;
                    _suggestionsList.SelectedIndex = 0;
                    _suggestionsList.Focus();
                    e.Handled = true;
                }
            }
        }

        private void UpdateSuggestions()
        {
            if (string.IsNullOrEmpty(Text) || Suggestions == null)
            {
                _suggestionsPopup.IsOpen = false;
                return;
            }

            var filteredSuggestions = Suggestions
                .Where(s => s.Contains(Text, System.StringComparison.OrdinalIgnoreCase))
                .OrderBy(s => s.IndexOf(Text, System.StringComparison.OrdinalIgnoreCase))
                .Take(10)
                .ToList();

            if (filteredSuggestions.Count > 0)
            {
                _suggestionsList.ItemsSource = filteredSuggestions;
                _suggestionsPopup.IsOpen = true;
                if (_suggestionsList.Items.Count > 0)
                {
                    _suggestionsList.SelectedIndex = -1;
                }
            }
            else
            {
                _suggestionsPopup.IsOpen = false;
            }
        }
    }
}