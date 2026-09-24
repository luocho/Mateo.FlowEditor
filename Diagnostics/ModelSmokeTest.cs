using FlowEditor.Models;
using FlowEditor.ViewModels;
using Framework;
using System;
using System.IO;
using System.Linq;
using System.Windows;

namespace FlowEditor.Diagnostics;

internal static class ModelSmokeTest
{
    public static int Run()
    {
        var flowPath = Path.Combine(Path.GetTempPath(), $"FlowEditor-{Guid.NewGuid():N}.mf");
        try
        {
            var main = new MainViewModel();
            Assert(MainViewModel.EnsureMfExtension("flow.data").EndsWith("flow.mf", StringComparison.Ordinal),
                "Save path extension was not normalized to .mf.");
            main.CreateFlow("Flow_1");
            main.CreateFlow("Flow_2");
            var rejectedEmptyName = false;
            try { main.CreateFlow(" "); }
            catch (ArgumentException) { rejectedEmptyName = true; }
            Assert(rejectedEmptyName, "An empty Flow name was accepted.");
            var editor = main.Editor ?? throw new InvalidOperationException("Editor was not created.");
            Assert(editor.Nodes.Select(n => n.Id).SequenceEqual([1]), "A new Flow must start with node ID 1.");

            var first = editor.Nodes[0];
            var middle = editor.AddNode(new Point(300, 100));
            var last = editor.AddNode(new Point(520, 100));
            editor.AddConnection(first, last);
            var connection = editor.Connections.Single();
            connection.Conditions = "OK";
            connection.RunType = "Flow_1";
            editor.SelectConnection(connection);
            Assert(ReferenceEquals(editor.SelectedConnection, connection) && connection.IsSelected,
                "Connection selection failed.");
            editor.RemoveNode(middle);
            Assert(editor.Nodes.Select(n => n.Id).SequenceEqual([1, 2]), "Node IDs were not renumbered.");
            Assert(editor.Connections.Single().NextStepId == 2, "NextStepId did not follow node renumbering.");
            editor.Undo();
            Assert(editor.Nodes.Select(n => n.Id).SequenceEqual([1, 2, 3]),
                "Undo did not restore continuous node IDs.");
            Assert(editor.Connections.Single().NextStepId == 3,
                "Undo did not restore the connection target ID.");
            editor.Redo();
            Assert(editor.Nodes.Select(n => n.Id).SequenceEqual([1, 2]),
                "Redo did not restore continuous node IDs.");

            last.StepType = FlowStepType.Flow;
            last.RunType = "Flow_1";
            Assert(last.ConfiguredColor == "#7C3AED", "FLOW node color was not read from appsettings.json.");
            Assert(main.NavigateToFlow(last), "FLOW navigation failed.");
            Assert(main.SelectedFlow?.Id == 1, "FLOW navigation selected the wrong page.");

            main.Editor?.Flush();
            var source = main.Flows[1];
            var protobufFlow = FlowListCodec.ToFlow(source);
            Assert(protobufFlow.Steps[1].StepLocation != null,
                "FlowStep.stepLocation was not written.");
            FlowListCodec.Write(flowPath, "TestFlowList", main.Flows);
            var rawFlowList = Framework.FlowList.Parser.ParseFrom(File.ReadAllBytes(flowPath));
            Assert(rawFlowList.Name == "TestFlowList" && rawFlowList.Flows.Count == 2,
                "Saved file is not a raw Framework.FlowList message.");
            var loadedFlowList = FlowListCodec.Read(flowPath);
            Assert(loadedFlowList.Name == "TestFlowList" && loadedFlowList.Flows.Count == 2,
                "FlowList round-trip lost a Flow.");
            var loadedFlow = loadedFlowList.Flows[1];
            Assert(loadedFlow.Nodes.Select(n => n.Id).SequenceEqual([1, 2]),
                "FlowList round-trip changed node IDs.");
            Assert(Math.Abs((loadedFlow.Nodes[1].X ?? 0) - last.X) < 0.01
                && Math.Abs((loadedFlow.Nodes[1].Y ?? 0) - last.Y) < 0.01,
                "FlowList round-trip lost StepLocation.");
            Assert(loadedFlow.Connections.Single().Conditions == "OK",
                "FlowList round-trip lost the connection condition.");
            return 0;
        }
        catch (Exception exception)
        {
            File.WriteAllText(Path.Combine(Path.GetTempPath(), "FlowEditor.ModelSmokeTest.failure.txt"), exception.ToString());
            return 1;
        }
        finally
        {
            File.Delete(flowPath);
        }
    }

    private static void Assert(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
