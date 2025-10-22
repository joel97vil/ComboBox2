using System;
using System.Collections;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Windows.Controls;
using System.Windows;
using System.Windows.Input;

namespace Controls
{
    public class ComboBox2 : ComboBox
    {
        //public bool AllowClear = false;

        private IEnumerable _originalItems;
        private object _lastValidSelectedItem;
        private bool _isTyping = false;
        //private bool _isFiltering = false;
        private bool _closingDueToSelection = false;
        private bool _isEditing = false;
        private string _typedBuffer = "";


        public ComboBox2()
        {
            IsEditable = true;
            IsTextSearchEnabled = false;
            //IsSynchronizedWithCurrentItem = false;

            ItemsPanel = new ItemsPanelTemplate(new FrameworkElementFactory(typeof(VirtualizingStackPanel)));

            Loaded += ComboBox2_Loaded;
            DropDownOpened += ComboBox2_DropDownOpened;
            DropDownClosed += ComboBox2_DropDownClosed;
            SelectionChanged += ComboBox2_SelectionChanged;
            GotKeyboardFocus += ComboBox2_GotKeyboardFocus;
            PreviewKeyUp += ComboBox2_PreviewKeyUp;
            AddHandler(TextBox.PreviewTextInputEvent, new TextCompositionEventHandler(TextBox_PreviewTextInput));
        }


        #region SECCIÓ DE EVENTS PER A COMBOBOX2
        private void ComboBox2_Loaded(object sender, RoutedEventArgs e)
        {
            _originalItems = ItemsSource;
            _lastValidSelectedItem = SelectedItem;
        }

        private void ComboBox2_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            _closingDueToSelection = true;
            _isEditing = false;

            if (!_isTyping && SelectedItem != null)
            {
                _lastValidSelectedItem = SelectedItem;

                // Perdre focus del TextBox intern
                TextBox tb = GetTemplateChild("PART_EditableTextBox") as TextBox;
                if (tb != null)
                {
                    // Envia focus cap al següent control
                    Dispatcher.BeginInvoke(new Action(() =>
                    {
                        FrameworkElement parent = GISWPF.Class.Utilitats.FindVisualParent<FrameworkElement>(tb);
                        //parent.Focus();
                    }), System.Windows.Threading.DispatcherPriority.ApplicationIdle);
                }

                // Netegem el text manualment i evitem conflictes
                Text = "";

                // Obtenim el text de l’item seleccionat
                if (SelectedItem != null)
                {
                    Text = GetItemDisplayText(SelectedItem); // Assignem text seleccionat
                }
            }

            _isTyping = false;
        }

        private void ComboBox2_DropDownOpened(object sender, EventArgs e)
        {
            Dispatcher.BeginInvoke(new Action(() =>
            {
                if (!_isEditing)
                {
                    ItemsSource = _originalItems;
                    //_isEditing = true;
                    Focus();
                }
            }), System.Windows.Threading.DispatcherPriority.Input);
        }

        private void ComboBox2_DropDownClosed(object sender, EventArgs e)
        {
            _isEditing = false;

            // Si no s'ha seleccionat res nou, restaura la selecció anterior
            if (!_isTyping && SelectedItem == null && _lastValidSelectedItem != null)
            {
                SelectedItem = _lastValidSelectedItem;
            }

            // Reinicia valors
            //_isFiltering = true;
            if (_closingDueToSelection)
            {
                // No netegem el text, perquè ha estat una selecció
                _closingDueToSelection = false;
            }
            else if (_lastValidSelectedItem == null && !_closingDueToSelection)
            {
                // Sí que netegem el filtre, perquè ha tancat manualment o amb focus i no tenia valor anteriorment
                Text = string.Empty;
            }

            _closingDueToSelection = false;
            ItemsSource = _originalItems;
            _typedBuffer = "";
            _isTyping = false;
        }

        private void ComboBox2_GotKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
        {
            if (e.OldFocus == null || !(e.OldFocus is TextBox))
            {
                // Comportament al focus per tabulació
                ItemsSource = _originalItems;
                if (!IsDropDownOpen)
                {
                    DropDownOpened -= ComboBox2_DropDownOpened;
                    IsDropDownOpen = true;
                    DropDownOpened += ComboBox2_DropDownOpened;
                }
            }
        }

        private void ComboBox2_PreviewKeyUp(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Up || e.Key == Key.Down)
            {
                // En cas de premer fletxes dalt o baix, saltar event
                return;
            }
            else if (e.Key == Key.Enter || e.Key == Key.Tab)
            {
                //TODO: En cas de premer ENTER o TAB, confirmar element seleccionat o hover (en cas de tenir-ne)
                return;
            }
            else if (e.Key == Key.Escape)
            {
                //TODO: En cas de premer ESC recuperar valor anterior (en cas de tenir-lo)
                return;
            }

            _isEditing = true;

            string filter = RemoveDiacritics(Text?.ToLowerInvariant() ?? "");

            if (string.IsNullOrWhiteSpace(filter))
            {
                ItemsSource = _originalItems;
                return;
            }

            var filtered = _originalItems.Cast<object>()
                .Where(item =>
                {
                    string displayValue = GetItemDisplayText(item);
                    return RemoveDiacritics(displayValue.ToLowerInvariant()).Contains(filter);
                }).ToList();

            ItemsSource = filtered;
            if (filtered != null && filtered.Any())
            {
                if (!IsDropDownOpen)
                {
                    DropDownOpened -= ComboBox2_DropDownOpened;
                    IsDropDownOpen = true;
                    DropDownOpened += ComboBox2_DropDownOpened;
                    //SelectedIndex = 0;
                }
            }
        }

        private void TextBox_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            if (!_isEditing)
            {
                TextBox tb = GetTemplateChild("PART_EditableTextBox") as TextBox;
                if (tb != null)
                {
                    _typedBuffer = e.Text;
                    Text = _typedBuffer;
                    tb.CaretIndex = _typedBuffer.Length;
                }

                e.Handled = true;
                _isEditing = true;
            }
        }
        #endregion


        #region SECCIÓ DE EVENTS SOBREESCRITS DEL COMBOBOX
        protected override void OnSelectionChanged(SelectionChangedEventArgs e)
        {
            if (!_isEditing) base.OnSelectionChanged(e); // només si no estàs escrivint
        }
        #endregion


        #region SECCIÓ DE FUNCIONS AUXILIARS
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
