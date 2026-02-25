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
