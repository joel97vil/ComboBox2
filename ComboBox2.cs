using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace Controls
{
    public class ComboBox2 : ComboBox
    {
        // Full unfiltered collection (captured whenever ItemsSource changes)
        private IEnumerable _originalItems;

        // Last confirmed selection (for Escape/restore on cancel)
        private object _lastValidSelectedItem;

        // True while the user is actively typing to filter
        private bool _isFiltering;

        // Suppress OnSelectionChanged during internal ItemsSource swaps
        private bool _suppressSelectionChanged;

        // Suppress TextChanged during programmatic text updates
        private bool _suppressTextChanged;

        // True during arrow key navigation (prevents auto-commit)
        private bool _isKeyboardNavigating;

        // The user's current filter string (preserved during arrow nav)
        private string _currentFilterText = "";

        // Cached reference to PART_EditableTextBox
        private TextBox _textBox;

        // Clear selection button
        private Button _clearButton;

        // False until after the initial load cycle completes (prevents auto-open on form load)
        private bool _isReady;


        public ComboBox2()
        {
            IsEditable = true;
            IsTextSearchEnabled = false;
            StaysOpenOnEdit = true;

            ItemsPanel = new ItemsPanelTemplate(new FrameworkElementFactory(typeof(VirtualizingStackPanel)));

            Loaded += (s, ev) =>
            {
                // Defer _isReady until after the initial auto-focus cycle has completed
                Dispatcher.BeginInvoke(new Action(() => _isReady = true), DispatcherPriority.Input);
            };
        }


        #region Template & ItemsSource capture

        public override void OnApplyTemplate()
        {
            base.OnApplyTemplate();

            _textBox = GetTemplateChild("PART_EditableTextBox") as TextBox;
            if (_textBox != null)
            {
                _textBox.TextChanged += TextBox_TextChanged;
            }

            CreateClearButton();
        }

        protected override void OnItemsSourceChanged(IEnumerable oldValue, IEnumerable newValue)
        {
            base.OnItemsSourceChanged(oldValue, newValue);

            // Only capture when it's an external binding change, not our internal filter swap
            if (!_suppressSelectionChanged && newValue != null)
            {
                _originalItems = newValue;
            }
        }

        #endregion


        #region Focus & Dropdown

        protected override void OnGotKeyboardFocus(KeyboardFocusChangedEventArgs e)
        {
            base.OnGotKeyboardFocus(e);

            // Only react when the ComboBox itself gains focus (not internal TextBox re-focus)
            if (e.NewFocus == _textBox && e.OldFocus != _textBox)
            {
                // Skip auto-open during initial form load auto-focus
                if (!_isReady) return;

                _lastValidSelectedItem = SelectedItem;

                if (!IsDropDownOpen)
                {
                    IsDropDownOpen = true;
                }

                // Select all text so user can start typing to replace
                if (_textBox != null)
                {
                    _textBox.SelectAll();
                }
            }
        }

        protected override void OnDropDownClosed(EventArgs e)
        {
            base.OnDropDownClosed(e);

            if (_isFiltering)
            {
                // Dropdown closed without explicit confirmation -> cancel
                CancelEditing();
            }

            _isFiltering = false;
            _isKeyboardNavigating = false;
            _currentFilterText = "";

            // Restore full list
            RestoreOriginalItems();
        }

        #endregion


        #region Text filtering

        private void TextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_suppressTextChanged) return;
            if (_textBox == null) return;
            if (!_isReady) return;

            _isFiltering = true;
            _currentFilterText = _textBox.Text ?? "";

            int caretIndex = _textBox.CaretIndex;

            string filter = RemoveDiacritics(_currentFilterText.ToLowerInvariant());

            if (string.IsNullOrEmpty(filter))
            {
                SwapItemsSource(_originalItems);
            }
            else if (_originalItems != null)
            {
                var filtered = _originalItems.Cast<object>()
                    .Where(item =>
                    {
                        string displayValue = GetItemDisplayText(item);
                        return RemoveDiacritics(displayValue.ToLowerInvariant()).Contains(filter);
                    }).ToList();

                SwapItemsSource(filtered);
            }

            // Restore the user's text and caret position after ItemsSource swap
            SetTextSuppressed(_currentFilterText);
            if (_textBox.Text.Length >= caretIndex)
                _textBox.CaretIndex = caretIndex;

            if (!IsDropDownOpen && _originalItems != null)
            {
                _suppressTextChanged = true;
                IsDropDownOpen = true;
                _suppressTextChanged = false;
                SetTextSuppressed(_currentFilterText);
                if (_textBox.Text.Length >= caretIndex)
                    _textBox.CaretIndex = caretIndex;
            }
        }

        #endregion


        #region Keyboard handling

        protected override void OnPreviewKeyDown(KeyEventArgs e)
        {
            if (e.Key == Key.Down || e.Key == Key.Up)
            {
                HandleArrowKey(e);
                return;
            }

            if (e.Key == Key.Enter)
            {
                CommitSelection();
                IsDropDownOpen = false;
                e.Handled = true;
                return;
            }

            if (e.Key == Key.Tab)
            {
                CommitSelection();
                IsDropDownOpen = false;
                // Don't set e.Handled — let Tab propagate to move focus
                return;
            }

            if (e.Key == Key.Escape)
            {
                CancelEditing();
                IsDropDownOpen = false;
                e.Handled = true;
                return;
            }

            base.OnPreviewKeyDown(e);
        }

        private void HandleArrowKey(KeyEventArgs e)
        {
            if (!IsDropDownOpen) return;

            _isKeyboardNavigating = true;

            int count = Items.Count;
            if (count == 0)
            {
                e.Handled = true;
                return;
            }

            int newIndex = SelectedIndex;
            if (e.Key == Key.Down)
                newIndex = Math.Min(newIndex + 1, count - 1);
            else
                newIndex = Math.Max(newIndex - 1, 0);

            // Suppress selection side effects, then manually set the index
            _suppressSelectionChanged = true;
            SelectedIndex = newIndex;
            _suppressSelectionChanged = false;

            // Restore the filter text that WPF would overwrite
            SetTextSuppressed(_currentFilterText);
            if (_textBox != null && _textBox.Text.Length >= _currentFilterText.Length)
                _textBox.CaretIndex = _currentFilterText.Length;

            _isKeyboardNavigating = false;
            e.Handled = true;
        }

        #endregion


        #region Selection handling

        protected override void OnSelectionChanged(SelectionChangedEventArgs e)
        {
            if (_suppressSelectionChanged) return;

            base.OnSelectionChanged(e);

            if (_isFiltering && !_isKeyboardNavigating && SelectedItem != null)
            {
                // User clicked an item in the dropdown while filtering -> commit
                CommitSelection();
            }

            UpdateClearButtonVisibility();
        }

        private void CommitSelection()
        {
            _isFiltering = false;

            if (SelectedItem != null)
            {
                _lastValidSelectedItem = SelectedItem;
            }
            else if (_lastValidSelectedItem != null)
            {
                // Nothing selected in filtered list — restore previous
                RestoreOriginalItems();
                _suppressSelectionChanged = true;
                SelectedItem = _lastValidSelectedItem;
                _suppressSelectionChanged = false;
            }

            // Show the display text for the confirmed item
            string displayText = GetItemDisplayText(_lastValidSelectedItem);
            RestoreOriginalItems();
            SetTextSuppressed(displayText);

            _currentFilterText = "";
        }

        private void CancelEditing()
        {
            _isFiltering = false;

            RestoreOriginalItems();

            _suppressSelectionChanged = true;
            SelectedItem = _lastValidSelectedItem;
            _suppressSelectionChanged = false;

            string displayText = GetItemDisplayText(_lastValidSelectedItem);
            SetTextSuppressed(displayText);

            _currentFilterText = "";
        }

        #endregion


        #region Helper methods

        private void CreateClearButton()
        {
            if (_textBox == null) return;

            // Walk up from the TextBox to find the template root Grid.
            // In Aero the TextBox is a direct child of the Grid;
            // in Aero2 (Win 10) the TextBox sits inside a Border first.
            Grid parent = null;
            DependencyObject current = _textBox;
            for (int i = 0; i < 5; i++)
            {
                current = VisualTreeHelper.GetParent(current);
                if (current == null) break;
                if (current is Grid grid) { parent = grid; break; }
            }
            if (parent == null) return;

            _clearButton = new Button
            {
                Content = "X",
                Foreground = Brushes.Red,
                FontWeight = FontWeights.Bold,
                FontSize = 12,
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 3, 0),
                Padding = new Thickness(2, 0, 2, 0),
                Background = Brushes.White,
                BorderThickness = new Thickness(0),
                Focusable = false,
                IsTabStop = false,
                Cursor = Cursors.Hand,
                Visibility = Visibility.Collapsed
            };

            Panel.SetZIndex(_clearButton, 10);
            Grid.SetColumn(_clearButton, 0);
            _clearButton.PreviewMouseLeftButtonDown += ClearButton_MouseDown;

            parent.Children.Add(_clearButton);
            UpdateClearButtonVisibility();
        }

        private void ClearButton_MouseDown(object sender, MouseButtonEventArgs e)
        {
            // Handle at PreviewMouseLeftButtonDown to act before focus changes
            // open the dropdown or disrupt the click.
            e.Handled = true;

            _isFiltering = false;
            _currentFilterText = "";
            _lastValidSelectedItem = null;

            RestoreOriginalItems();

            _suppressTextChanged = true;
            SelectedItem = null;
            SelectedIndex = -1;
            _suppressTextChanged = false;

            SetTextSuppressed("");
            IsDropDownOpen = false;
            UpdateClearButtonVisibility();
        }

        private void UpdateClearButtonVisibility()
        {
            if (_clearButton == null) return;
            _clearButton.Visibility = SelectedItem != null ? Visibility.Visible : Visibility.Collapsed;
        }

        private void SwapItemsSource(IEnumerable source)
        {
            _suppressSelectionChanged = true;
            _suppressTextChanged = true;
            ItemsSource = source;
            _suppressTextChanged = false;
            _suppressSelectionChanged = false;
        }

        private void SetTextSuppressed(string text)
        {
            if (_textBox == null) return;

            _suppressTextChanged = true;
            _textBox.Text = text;
            // Also update ComboBox.Text so the ComboBox's internal state matches.
            // Without this, the ComboBox may asynchronously overwrite TextBox.Text
            // to match its own stale Text value (e.g. "" after an ItemsSource swap).
            Text = text;
            _suppressTextChanged = false;
        }

        private void RestoreOriginalItems()
        {
            if (_originalItems != null && ItemsSource != _originalItems)
            {
                SwapItemsSource(_originalItems);
            }
        }

        private string GetItemDisplayText(object item)
        {
            if (item == null) return "";

            if (!string.IsNullOrEmpty(DisplayMemberPath))
            {
                var prop = item.GetType().GetProperty(DisplayMemberPath);
                return prop?.GetValue(item, null)?.ToString() ?? "";
            }

            return item.ToString();
        }

        private string RemoveDiacritics(string text)
        {
            return new string(text
                .Normalize(NormalizationForm.FormD)
                .Where(c => CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
                .ToArray());
        }

        #endregion
    }
}
