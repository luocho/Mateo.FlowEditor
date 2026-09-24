using System;
using System.Windows;
using System.Windows.Controls;

namespace FlowEditor.Dialogs;

public partial class FlowNameDialog : Window
{
    private readonly Func<string, string?> _validate;

    public string FlowName { get; private set; } = "";

    public FlowNameDialog(string title, string initialName, Func<string, string?> validate)
    {
        InitializeComponent();
        Title = title;
        _validate = validate;
        FlowNameBox.Text = initialName;
        FlowNameBox.SelectAll();
        Loaded += (_, _) => FlowNameBox.Focus();
        UpdateValidation();
    }

    private void FlowNameBox_TextChanged(object sender, TextChangedEventArgs e) => UpdateValidation();

    private void ConfirmButton_Click(object sender, RoutedEventArgs e)
    {
        var name = FlowNameBox.Text.Trim();
        if (_validate(name) is { } error)
        {
            ValidationText.Text = error;
            return;
        }
        FlowName = name;
        DialogResult = true;
    }

    private void UpdateValidation()
    {
        if (ValidationText == null || ConfirmButton == null) return;
        var error = _validate(FlowNameBox.Text.Trim());
        ValidationText.Text = error ?? "";
        ConfirmButton.IsEnabled = error == null;
    }
}
