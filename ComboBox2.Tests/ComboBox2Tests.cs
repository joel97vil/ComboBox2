using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Windows.Controls;
using Controls;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ComboBox2.Tests
{
    [TestClass]
    public class ComboBox2Tests
    {
        #region Helpers

        private static void RunOnSTA(Action action)
        {
            Exception caught = null;
            var thread = new Thread(() =>
            {
                try { action(); }
                catch (Exception ex) { caught = ex; }
            });
            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
            thread.Join();
            if (caught != null)
                throw caught;
        }

        private static void SetField(object obj, string name, object value)
        {
            var field = obj.GetType().GetField(name,
                BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsNotNull(field, "Field '{0}' not found", name);
            field.SetValue(obj, value);
        }

        private static T GetField<T>(object obj, string name)
        {
            var field = obj.GetType().GetField(name,
                BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsNotNull(field, "Field '{0}' not found", name);
            return (T)field.GetValue(obj);
        }

        private static object InvokeMethod(object obj, string name, params object[] args)
        {
            var method = obj.GetType().GetMethod(name,
                BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsNotNull(method, "Method '{0}' not found", name);
            return method.Invoke(obj, args);
        }

        /// <summary>
        /// Creates a ComboBox2 with private fields pre-configured for testing
        /// (bypasses the need for a visual tree / template application).
        /// </summary>
        private static Controls.ComboBox2 CreateTestCombo(IList items, object selectedItem = null)
        {
            var combo = new Controls.ComboBox2();

            // Inject a standalone TextBox so SetTextSuppressed works
            SetField(combo, "_textBox", new TextBox());
            SetField(combo, "_isReady", true);

            combo.ItemsSource = items;
            SetField(combo, "_originalItems", items);

            if (selectedItem != null)
            {
                combo.SelectedItem = selectedItem;
                SetField(combo, "_lastValidSelectedItem", selectedItem);
            }

            return combo;
        }

        private class TestItem
        {
            public int Id { get; set; }
            public string Name { get; set; }
            public override string ToString() { return Name; }
        }

        #endregion


        #region First selection (no prior SelectedItem)

        [TestMethod]
        public void DropDownClosed_WithNoPriorSelection_PreservesNewSelection()
        {
            // Reproduces: user opens dropdown, clicks an item when nothing was selected before.
            // WPF updates TextBox after selection, which must NOT trigger filtering/cancel.
            RunOnSTA(() =>
            {
                var items = new List<string> { "Apple", "Banana", "Cherry" };
                var combo = CreateTestCombo(items);
                SetField(combo, "_lastValidSelectedItem", null);

                // Simulate: user clicks "Banana" → WPF sets SelectedItem
                combo.SelectedItem = "Banana";

                // OnSelectionChanged should have suppressed TextChanged during base call,
                // so _isFiltering must still be false.
                bool isFiltering = GetField<bool>(combo, "_isFiltering");
                Assert.IsFalse(isFiltering,
                    "_isFiltering should remain false after selection-driven text update");

                // Now simulate dropdown closing
                // _isFiltering is false → CancelEditing should NOT be called
                // → SelectedItem should remain "Banana"
                InvokeMethod(combo, "OnDropDownClosed", EventArgs.Empty);

                Assert.AreEqual("Banana", combo.SelectedItem,
                    "SelectedItem should be preserved after dropdown closes");
            });
        }

        #endregion


        #region CommitSelection — SelectedItem preserved

        [TestMethod]
        public void CommitSelection_PreservesSelectedItem()
        {
            RunOnSTA(() =>
            {
                var items = new List<string> { "Apple", "Banana", "Cherry" };
                var combo = CreateTestCombo(items, "Banana");

                // Simulate filtering: use SwapItemsSource so _originalItems is NOT overwritten
                var filtered = new List<string> { "Banana" };
                InvokeMethod(combo, "SwapItemsSource", filtered);
                SetField(combo, "_isFiltering", true);

                InvokeMethod(combo, "CommitSelection");

                Assert.AreEqual("Banana", combo.SelectedItem,
                    "SelectedItem should be preserved after CommitSelection");
                Assert.AreSame(items, combo.ItemsSource,
                    "ItemsSource should be restored to original list");
            });
        }

        [TestMethod]
        public void CommitSelection_RestoresLastValid_WhenSelectedItemNull()
        {
            RunOnSTA(() =>
            {
                var items = new List<string> { "Apple", "Banana", "Cherry" };
                var combo = CreateTestCombo(items, "Banana");

                // Simulate filtering where SelectedItem became null
                var filtered = new List<string> { "Cherry" };
                InvokeMethod(combo, "SwapItemsSource", filtered);
                combo.SelectedItem = null;
                SetField(combo, "_isFiltering", true);

                InvokeMethod(combo, "CommitSelection");

                Assert.AreEqual("Banana", combo.SelectedItem,
                    "SelectedItem should be restored from _lastValidSelectedItem");
            });
        }

        [TestMethod]
        public void CommitSelection_SetsDisplayText()
        {
            RunOnSTA(() =>
            {
                var items = new List<string> { "Apple", "Banana", "Cherry" };
                var combo = CreateTestCombo(items, "Cherry");

                SetField(combo, "_isFiltering", true);

                InvokeMethod(combo, "CommitSelection");

                Assert.AreEqual("Cherry", combo.Text,
                    "Text should match selected item display text");
            });
        }

        #endregion


        #region CancelEditing — SelectedItem restored

        [TestMethod]
        public void CancelEditing_RestoresSelectedItem()
        {
            RunOnSTA(() =>
            {
                var items = new List<string> { "Apple", "Banana", "Cherry" };
                var combo = CreateTestCombo(items, "Apple");

                // Simulate filtering
                var filtered = new List<string> { "Cherry" };
                InvokeMethod(combo, "SwapItemsSource", filtered);
                SetField(combo, "_isFiltering", true);

                InvokeMethod(combo, "CancelEditing");

                Assert.AreEqual("Apple", combo.SelectedItem,
                    "SelectedItem should be restored to last valid item");
                Assert.AreSame(items, combo.ItemsSource,
                    "ItemsSource should be restored to original list");
            });
        }

        [TestMethod]
        public void CancelEditing_RestoresDisplayText()
        {
            RunOnSTA(() =>
            {
                var items = new List<string> { "Apple", "Banana", "Cherry" };
                var combo = CreateTestCombo(items, "Apple");

                SetField(combo, "_isFiltering", true);

                InvokeMethod(combo, "CancelEditing");

                Assert.AreEqual("Apple", combo.Text,
                    "Text should match restored item");
            });
        }

        [TestMethod]
        public void CancelEditing_NullLastValid_ClearsSelection()
        {
            RunOnSTA(() =>
            {
                var items = new List<string> { "Apple", "Banana", "Cherry" };
                var combo = CreateTestCombo(items);

                SetField(combo, "_isFiltering", true);
                SetField(combo, "_lastValidSelectedItem", null);

                InvokeMethod(combo, "CancelEditing");

                Assert.IsNull(combo.SelectedItem,
                    "SelectedItem should remain null when no previous selection");
                Assert.AreEqual("", combo.Text,
                    "Text should be empty when no previous selection");
            });
        }

        #endregion


        #region DisplayMemberPath / SelectedValuePath

        [TestMethod]
        public void CommitSelection_WithDisplayMemberPath_UsesProperty()
        {
            RunOnSTA(() =>
            {
                var items = new List<TestItem>
                {
                    new TestItem { Id = 1, Name = "Alpha" },
                    new TestItem { Id = 2, Name = "Beta" },
                    new TestItem { Id = 3, Name = "Gamma" }
                };

                var combo = CreateTestCombo(items, items[1]);
                combo.DisplayMemberPath = "Name";

                SetField(combo, "_isFiltering", true);

                InvokeMethod(combo, "CommitSelection");

                Assert.AreEqual(items[1], combo.SelectedItem);
                Assert.AreEqual("Beta", combo.Text,
                    "Text should use DisplayMemberPath property value");
            });
        }

        [TestMethod]
        public void CommitSelection_WithSelectedValuePath_PreservesValue()
        {
            RunOnSTA(() =>
            {
                var items = new List<TestItem>
                {
                    new TestItem { Id = 1, Name = "Alpha" },
                    new TestItem { Id = 2, Name = "Beta" },
                    new TestItem { Id = 3, Name = "Gamma" }
                };

                var combo = CreateTestCombo(items, items[2]);
                combo.SelectedValuePath = "Id";

                // Simulate filter + commit
                SetField(combo, "_isFiltering", true);
                InvokeMethod(combo, "CommitSelection");

                Assert.AreEqual(items[2], combo.SelectedItem);
                Assert.AreEqual(3, combo.SelectedValue,
                    "SelectedValue should be preserved after commit");
            });
        }

        [TestMethod]
        public void CancelEditing_WithDisplayMemberPath_RestoresCorrectly()
        {
            RunOnSTA(() =>
            {
                var items = new List<TestItem>
                {
                    new TestItem { Id = 1, Name = "Alpha" },
                    new TestItem { Id = 2, Name = "Beta" },
                    new TestItem { Id = 3, Name = "Gamma" }
                };

                var combo = CreateTestCombo(items, items[0]);
                combo.DisplayMemberPath = "Name";

                // Simulate filtering
                var filtered = new List<TestItem> { items[2] };
                InvokeMethod(combo, "SwapItemsSource", filtered);
                SetField(combo, "_isFiltering", true);

                InvokeMethod(combo, "CancelEditing");

                Assert.AreEqual(items[0], combo.SelectedItem);
                Assert.AreEqual("Alpha", combo.Text,
                    "Text should use DisplayMemberPath for restored item");
            });
        }

        #endregion


        #region Clear button visibility on programmatic selection

        /// <summary>
        /// Pumps the dispatcher so queued BeginInvoke callbacks (e.g. _isReady) run.
        /// </summary>
        private static void DoEvents()
        {
            var frame = new System.Windows.Threading.DispatcherFrame();
            System.Windows.Threading.Dispatcher.CurrentDispatcher.BeginInvoke(
                System.Windows.Threading.DispatcherPriority.Background,
                new Action(() => frame.Continue = false));
            System.Windows.Threading.Dispatcher.PushFrame(frame);
        }

        [TestMethod]
        public void ClearButton_VisibleOnLoad_WhenSelectionSetInCodeBehind()
        {
            RunOnSTA(() =>
            {
                var items = new List<string> { "Apple", "Banana", "Cherry" };
                var combo = new Controls.ComboBox2();
                combo.ItemsSource = items;

                // Back code sets selection BEFORE the control is loaded/templated
                combo.SelectedItem = "Banana";

                var window = new System.Windows.Window
                {
                    Content = combo,
                    Width = 200,
                    Height = 100,
                    Left = -10000,
                    Top = -10000,
                    ShowInTaskbar = false,
                    ShowActivated = false,
                    WindowStyle = System.Windows.WindowStyle.None
                };

                try
                {
                    window.Show(); // applies template, raises Loaded
                    DoEvents();

                    var clearButton = GetField<Button>(combo, "_clearButton");
                    Assert.IsNotNull(clearButton, "Clear button should be created on template apply");
                    Assert.AreEqual(System.Windows.Visibility.Visible, clearButton.Visibility,
                        "Clear button should be visible when selection was set in code-behind");
                }
                finally
                {
                    window.Close();
                }
            });
        }

        private static void AssertClearButtonVisibleAfterLoad(Action<Controls.ComboBox2> setupBeforeShow,
            Action<Controls.ComboBox2> setupAfterShow = null)
        {
            RunOnSTA(() =>
            {
                var combo = new Controls.ComboBox2();
                setupBeforeShow(combo);

                var window = new System.Windows.Window
                {
                    Content = combo,
                    Width = 200,
                    Height = 100,
                    Left = -10000,
                    Top = -10000,
                    ShowInTaskbar = false,
                    ShowActivated = false,
                    WindowStyle = System.Windows.WindowStyle.None
                };

                try
                {
                    window.Show();
                    DoEvents();
                    if (setupAfterShow != null)
                    {
                        setupAfterShow(combo);
                        DoEvents();
                    }

                    Assert.IsNotNull(combo.SelectedItem, "Precondition: selection should have resolved");
                    var clearButton = GetField<Button>(combo, "_clearButton");
                    Assert.IsNotNull(clearButton, "Clear button should be created on template apply");
                    Assert.AreEqual(System.Windows.Visibility.Visible, clearButton.Visibility,
                        "Clear button should be visible for programmatic selection");
                }
                finally
                {
                    window.Close();
                }
            });
        }

        [TestMethod]
        public void ClearButton_VisibleOnLoad_SelectedValueWithPaths()
        {
            var items = new List<TestItem>
            {
                new TestItem { Id = 1, Name = "Alpha" },
                new TestItem { Id = 2, Name = "Beta" }
            };
            AssertClearButtonVisibleAfterLoad(combo =>
            {
                combo.DisplayMemberPath = "Name";
                combo.SelectedValuePath = "Id";
                combo.ItemsSource = items;
                combo.SelectedValue = 2;
            });
        }

        [TestMethod]
        public void ClearButton_VisibleOnLoad_SelectedValueBeforeItemsSource()
        {
            var items = new List<TestItem>
            {
                new TestItem { Id = 1, Name = "Alpha" },
                new TestItem { Id = 2, Name = "Beta" }
            };
            AssertClearButtonVisibleAfterLoad(combo =>
            {
                combo.DisplayMemberPath = "Name";
                combo.SelectedValuePath = "Id";
                combo.SelectedValue = 2;
                combo.ItemsSource = items;
            });
        }

        [TestMethod]
        public void ClearButton_VisibleAfterTabShown_WhenSelectionSetWhileTabHidden()
        {
            RunOnSTA(() =>
            {
                var items = new List<string> { "Apple", "Banana", "Cherry" };
                var combo = new Controls.ComboBox2();
                combo.ItemsSource = items;

                var tabs = new TabControl();
                tabs.Items.Add(new TabItem { Header = "First", Content = new TextBlock() });
                tabs.Items.Add(new TabItem { Header = "Second", Content = combo });
                tabs.SelectedIndex = 0;

                var window = new System.Windows.Window
                {
                    Content = tabs,
                    Width = 300,
                    Height = 200,
                    Left = -10000,
                    Top = -10000,
                    ShowInTaskbar = false,
                    ShowActivated = false,
                    WindowStyle = System.Windows.WindowStyle.None
                };

                try
                {
                    window.Show();
                    DoEvents();

                    // Back code sets selection while the combo's tab was never shown
                    combo.SelectedItem = "Banana";
                    DoEvents();

                    // User switches to the tab containing the combo
                    tabs.SelectedIndex = 1;
                    DoEvents();

                    var clearButton = GetField<Button>(combo, "_clearButton");
                    Assert.IsNotNull(clearButton, "Clear button should be created once the tab is shown");
                    Assert.AreEqual(System.Windows.Visibility.Visible, clearButton.Visibility,
                        "Clear button should be visible: selection was set while the tab was hidden");
                }
                finally
                {
                    window.Close();
                }
            });
        }

        [TestMethod]
        public void ClearButton_Visible_SelectionSetAfterLoad()
        {
            var items = new List<TestItem>
            {
                new TestItem { Id = 1, Name = "Alpha" },
                new TestItem { Id = 2, Name = "Beta" }
            };
            AssertClearButtonVisibleAfterLoad(
                combo =>
                {
                    combo.DisplayMemberPath = "Name";
                    combo.SelectedValuePath = "Id";
                },
                combo =>
                {
                    combo.ItemsSource = items;
                    combo.SelectedValue = 2;
                });
        }

        #endregion


        #region Auto-highlight single filtered result

        [TestMethod]
        public void Filtering_SingleResult_AutoHighlightsItem()
        {
            RunOnSTA(() =>
            {
                var items = new List<string> { "Apple", "Banana", "Cherry" };
                var combo = CreateTestCombo(items);
                combo.IsDropDownOpen = true;

                var textBox = GetField<TextBox>(combo, "_textBox");
                textBox.Text = "ban";
                InvokeMethod(combo, "TextBox_TextChanged", textBox,
                    new TextChangedEventArgs(TextBox.TextChangedEvent, UndoAction.None));

                Assert.AreEqual("Banana", combo.SelectedItem,
                    "Single filtered result should be auto-highlighted");
                Assert.AreEqual("ban", textBox.Text,
                    "Filter text should be preserved after auto-highlight");
            });
        }

        [TestMethod]
        public void Filtering_MultipleResults_NoAutoHighlight()
        {
            RunOnSTA(() =>
            {
                var items = new List<string> { "Apple", "Banana", "Cherry" };
                var combo = CreateTestCombo(items);
                combo.IsDropDownOpen = true;

                var textBox = GetField<TextBox>(combo, "_textBox");
                textBox.Text = "a";
                InvokeMethod(combo, "TextBox_TextChanged", textBox,
                    new TextChangedEventArgs(TextBox.TextChangedEvent, UndoAction.None));

                Assert.AreEqual(2, combo.Items.Count,
                    "Filter 'a' should match Apple and Banana");
                Assert.IsNull(combo.SelectedItem,
                    "No auto-highlight when filter yields multiple results");
            });
        }

        #endregion


        #region Utility methods

        [TestMethod]
        public void RemoveDiacritics_StripsAccents()
        {
            RunOnSTA(() =>
            {
                var combo = new Controls.ComboBox2();

                var result1 = (string)InvokeMethod(combo, "RemoveDiacritics", "cafe");
                Assert.AreEqual("cafe", result1);

                var result2 = (string)InvokeMethod(combo, "RemoveDiacritics", "caf\u00e9");
                Assert.AreEqual("cafe", result2);

                var result3 = (string)InvokeMethod(combo, "RemoveDiacritics", "Z\u00fcrcher");
                Assert.AreEqual("Zurcher", result3);

                var result4 = (string)InvokeMethod(combo, "RemoveDiacritics", "");
                Assert.AreEqual("", result4);
            });
        }

        [TestMethod]
        public void RestoreOriginalItems_NoOp_WhenAlreadyOriginal()
        {
            RunOnSTA(() =>
            {
                var items = new List<string> { "Apple", "Banana" };
                var combo = CreateTestCombo(items, "Apple");

                // ItemsSource already equals _originalItems — should be a no-op
                InvokeMethod(combo, "RestoreOriginalItems");

                Assert.AreSame(items, combo.ItemsSource);
                Assert.AreEqual("Apple", combo.SelectedItem,
                    "SelectedItem should not be disturbed");
            });
        }

        #endregion
    }
}
