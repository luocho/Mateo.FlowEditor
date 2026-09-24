using FlowEditor.Dialogs;
using FlowEditor.ViewModels;
using FlowEditor.Models;
using Framework;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Threading;

namespace FlowEditor.Diagnostics;

internal static class UiSmokeTest
{
    public static int Run()
    {
        MainWindow? window = null;
        try
        {
            window = CreateFixture();
            window.Left = -32000;
            window.Top = -32000;
            window.ShowInTaskbar = false;
            window.ShowActivated = false;
            var main = (MainViewModel)window.DataContext;
            var editor = main.Editor ?? throw new InvalidOperationException("Active flow editor was not created.");
            var node = editor.Nodes.Single(n => n.Id == 2);

            window.Show();
            RefreshUi(window);

            var nameDialog = new FlowNameDialog("新建流程", "",
                name => string.IsNullOrWhiteSpace(name) ? "流程名称不能为空。" : null);
            Assert(!nameDialog.ConfirmButton.IsEnabled, "An empty Flow name can be confirmed.");
            nameDialog.FlowNameBox.Text = "CheckedFlow";
            Assert(nameDialog.ConfirmButton.IsEnabled, "A valid Flow name cannot be confirmed.");
            nameDialog.Close();

            var flowItem = FindVisualChildren<Border>(window).First(b =>
                b.DataContext is FlowDef && b.ContextMenu != null);
            Assert(flowItem.ContextMenu!.Items.Count == 2, "Flow context menu is missing rename or delete.");

            var connection = editor.Connections.Single();
            editor.SelectConnection(connection);
            RefreshUi(window);
            var conditionSelector = FindVisualChildren<ComboBox>(window).Single(c =>
                ReferenceEquals(c.DataContext, connection) && ReferenceEquals(c.ItemsSource, editor.ConditionOptions));
            conditionSelector.SelectedItem = "OK";
            RefreshUi(window);
            Assert(connection.Conditions == "OK", "The connection inspector did not update Conditions.");
            editor.SelectOnly(node);
            RefreshUi(window);

            var stepTypeSelector = FindVisualChildren<ComboBox>(window).Single(c =>
                ReferenceEquals(c.DataContext, node) && ReferenceEquals(c.ItemsSource, editor.StepTypeOptions));
            BindingOperations.ClearBinding(stepTypeSelector, Selector.SelectedItemProperty);
            stepTypeSelector.SelectedItem = FlowStepType.Flow;
            RefreshUi(window);

            Assert(node.StepType == FlowStepType.Flow, "Selecting FLOW did not update StepType.");
            Assert(node.HeaderBrush is SolidColorBrush { Color: var color }
                   && color == Color.FromRgb(0x7C, 0x3A, 0xED),
                "Selecting FLOW did not apply the purple node color.");

            var targetFlowSelector = FindVisualChildren<ComboBox>(window).Single(c =>
                ReferenceEquals(c.DataContext, node) && ReferenceEquals(c.ItemsSource, main.FlowTargets));
            targetFlowSelector.Text = "Flow_1";
            RefreshUi(window);
            Assert(node.RunType == "Flow_1", "Selecting the target Flow did not update RunType.");
            Assert(main.NavigateToFlow(node), "FLOW navigation did not find the target page.");
            Assert(main.SelectedFlow?.Name == "Flow_1", "FLOW navigation selected the wrong page.");

            var firstEditor = main.Editor ?? throw new InvalidOperationException("Target editor was not created.");
            firstEditor.AddNode(new Point(400, 260));
            firstEditor.AddNode(new Point(620, 260));
            firstEditor.RemoveNode(firstEditor.Nodes[1]);
            Assert(firstEditor.Nodes.Select(n => n.Id).SequenceEqual([1, 2]),
                "Deleting a middle node did not produce continuous IDs.");
            return 0;
        }
        catch (Exception exception)
        {
            File.WriteAllText(Path.Combine(Path.GetTempPath(), "FlowEditor.UiSmokeTest.failure.txt"), exception.ToString());
            return 1;
        }
        finally
        {
            window?.Close();
        }
    }

    private static MainWindow CreateFixture()
    {
        var window = new MainWindow();
        var main = (MainViewModel)window.DataContext;
        main.CreateFlow("Flow_1");
        main.CreateFlow("Flow_2");
        if (main.Editor is { } editor)
        {
            var node = editor.AddNode(new Point(400, 260));
            editor.AddConnection(editor.Nodes[0], node);
        }
        return window;
    }

    private static void RefreshUi(FrameworkElement element)
    {
        element.UpdateLayout();
        element.Dispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);
    }

    private static IEnumerable<T> FindVisualChildren<T>(DependencyObject parent) where T : DependencyObject
    {
        for (var index = 0; index < VisualTreeHelper.GetChildrenCount(parent); index++)
        {
            var child = VisualTreeHelper.GetChild(parent, index);
            if (child is T match) yield return match;
            foreach (var descendant in FindVisualChildren<T>(child)) yield return descendant;
        }
    }

    private static void Assert(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
