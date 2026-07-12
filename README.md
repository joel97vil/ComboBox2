# ComboBox2

A WPF custom ComboBox control with built-in type-to-filter, auto-open dropdown, and keyboard navigation.

## Features

- **Type-to-filter**: Diacritic-insensitive substring filtering as you type.
- **Auto-open dropdown**: The dropdown opens automatically when the control receives focus.
- **Keyboard navigation**: Arrow keys navigate the filtered list while preserving your filter text.
- **Commit / Cancel**: Press **Enter** or **Tab** to confirm a selection. Press **Escape** to cancel and restore the previous value.
- **Clear button**: A built-in "X" button appears when an item is selected, allowing one-click clearing.
- **Virtualization**: Uses `VirtualizingStackPanel` for smooth performance with large item lists.

## .NET Framework Compatibility

ComboBox2 targets **.NET Framework 4.8** (`net48`, VS 2019+), the latest .NET Framework release with long-term Windows support. Your project must target .NET Framework 4.8 or later.

The build produces:

```
bin\Release\net48\ComboBox2.dll
```

> **Note:** ComboBox2 targets .NET Framework only. It is not compatible with .NET Core, .NET 5+, or .NET 6+ (which use a different WPF SDK).

## Getting started

### 1. Add the file directly (simplest)

Copy `ComboBox2.cs` into your WPF project. Make sure your project references the standard WPF assemblies (`PresentationCore`, `PresentationFramework`, `WindowsBase`, `System.Xaml`).

### 2. Add as a DLL reference

1. Build the ComboBox2 project (`dotnet build -c Release`).
2. In your WPF project, right-click **References** → **Add Reference** → **Browse**.
3. Navigate to `bin\Release\net48\ComboBox2.dll`.

### 3. Add as a project reference (same solution)

1. Add the `ComboBox2.csproj` to your solution.
2. In your WPF project, right-click **References** → **Add Reference** → **Projects** → select **ComboBox2**.

### 4. Use in XAML

Add the namespace:

```xml
<Window x:Class="MyApp.MainWindow"
        xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        xmlns:ctrl="clr-namespace:Controls;assembly=ComboBox2">
```

Use the control:

```xml
<ctrl:ComboBox2 Width="200"
                ItemsSource="{Binding MyItems}"
                SelectedItem="{Binding SelectedItem}"
                DisplayMemberPath="Name" />
```

It works as a drop-in replacement for the standard `ComboBox`. All standard ComboBox properties and bindings (`ItemsSource`, `SelectedItem`, `SelectedValue`, `DisplayMemberPath`, etc.) are fully supported.

### 5. Use in code-behind

```csharp
using Controls;

var combo = new ComboBox2();
combo.ItemsSource = myItemsList;
combo.DisplayMemberPath = "Name";
combo.SelectionChanged += (s, e) =>
{
    var selected = combo.SelectedItem;
    // handle selection
};
```

## Keyboard shortcuts

| Key | Action |
|---|---|
| **Enter** | Confirm the current selection and close the dropdown |
| **Tab** | Confirm the current selection, close the dropdown, and move focus to the next control |
| **Escape** | Cancel editing and restore the previous selection |
| **Up / Down** | Navigate the filtered list without losing your filter text |

## License

[MIT](LICENSE)
