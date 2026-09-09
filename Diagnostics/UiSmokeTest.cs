using FlowEditor.ViewModels;
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
            var node = editor.Nodes.Single(n => n.Id == "Func_1");

            window.Show();
            RefreshUi(window);

            var nodeTypeSelector = FindVisualChildren<ComboBox>(window).Single(c =>
                ReferenceEquals(c.DataContext, node) && ReferenceEquals(c.ItemsSource, editor.NodeTypeOptions));
            BindingOperations.ClearBinding(nodeTypeSelector, Selector.SelectedItemProperty);
            nodeTypeSelector.SelectedItem = null;
            nodeTypeSelector.IsDropDownOpen = true;
            nodeTypeSelector.SelectedItem = "SubFlow";
            nodeTypeSelector.IsDropDownOpen = false;
            RefreshUi(window);

            Assert(node.NodeType == "SubFlow", "Selecting SubFlow did not update NodeType.");
            Assert(node.HeaderBrush is SolidColorBrush { Color: var color }
                   && color == Color.FromRgb(0x7C, 0x3A, 0xED),
                "Selecting SubFlow did not apply the purple node color.");

            var targetFlowSelector = FindVisualChildren<ComboBox>(window).Single(c =>
                ReferenceEquals(c.DataContext, node) && ReferenceEquals(c.ItemsSource, main.FlowIds));
            BindingOperations.ClearBinding(targetFlowSelector, Selector.SelectedItemProperty);
            targetFlowSelector.SelectedItem = null;
            targetFlowSelector.IsDropDownOpen = true;
            targetFlowSelector.SelectedItem = "Flow_1";
            targetFlowSelector.IsDropDownOpen = false;
            RefreshUi(window);

            Assert(node.ExeName == "Flow_1", "Selecting the target Flow did not update ExeName.");
            Assert(targetFlowSelector.Text == "Flow_1", "The target Flow selection is not visible.");

            node.ExeName = "";
            targetFlowSelector.IsDropDownOpen = true;
            targetFlowSelector.IsDropDownOpen = false;
            RefreshUi(window);
            Assert(node.ExeName == "Flow_1", "Closing the target Flow selector did not commit ExeName.");
            return 0;
        }
        catch (Exception ex)
        {
            File.WriteAllText(Path.Combine(Path.GetTempPath(), "FlowEditor.UiSmokeTest.failure.txt"), ex.ToString());
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
        main.AddFlowCommand.Execute(null);
        main.AddFlowCommand.Execute(null);
        main.Editor?.AddNode(new Point(400, 260), "Func");
        return window;
    }

    private static void RefreshUi(FrameworkElement element)
    {
        element.UpdateLayout();
        element.Dispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);
    }

    private static IEnumerable<T> FindVisualChildren<T>(DependencyObject parent) where T : DependencyObject
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);
            if (child is T match) yield return match;
            foreach (var descendant in FindVisualChildren<T>(child)) yield return descendant;
        }
    }

    private static void Assert(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
