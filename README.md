# ComboBox2

A WPF custom ComboBox control with built-in type-to-filter, auto-open dropdown, and keyboard navigation.

## Features

- **Type-to-filter**: Diacritic-insensitive substring filtering as you type.
- **Auto-open dropdown**: The dropdown opens automatically when the control receives focus.
- **Keyboard navigation**: Arrow keys navigate the filtered list while preserving your filter text.
- **Commit / Cancel**: Press **Enter** or **Tab** to confirm a selection. Press **Escape** to cancel and restore the previous value.
- **Clear button**: A built-in "X" button appears when an item is selected, allowing one-click clearing.
- **Virtualization**: Uses `VirtualizingStackPanel` for smooth performance with large item lists.

## Getting started

### 1. Add the reference

In your WPF project, right-click **References** (or **Dependencies**) in Solution Explorer:

- **Project reference** (same solution): Add Reference → Projects → select `ComboBox2`.
- **DLL reference**: Add Reference → Browse → navigate to `bin\Release\ComboBox2.dll`.

### 2. Add the namespace in XAML

```xml
<Window x:Class="MyApp.MainWindow"
        xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        xmlns:ctrl="clr-namespace:Controls;assembly=ComboBox2">
```

### 3. Use the control

```xml
<ctrl:ComboBox2 Width="200"
                ItemsSource="{Binding MyItems}"
                SelectedItem="{Binding SelectedItem}"
                DisplayMemberPath="Name" />
```

It works as a drop-in replacement for the standard `ComboBox`. All standard ComboBox properties and bindings (`ItemsSource`, `SelectedItem`, `SelectedValue`, `DisplayMemberPath`, etc.) are fully supported.

## Keyboard shortcuts

| Key | Action |
|---|---|
| **Enter** | Confirm the current selection and close the dropdown |
| **Tab** | Confirm the current selection, close the dropdown, and move focus to the next control |
| **Escape** | Cancel editing and restore the previous selection |
| **Up / Down** | Navigate the filtered list without losing your filter text |

## License

[MIT](LICENSE)
