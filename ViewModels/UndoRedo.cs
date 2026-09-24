using Framework;
using System;
using System.Collections.Generic;
using System.Linq;

namespace FlowEditor.ViewModels;

public sealed record NodeSnapshot(
    Guid Key,
    string Name,
    string RunType,
    FlowStepType StepType,
    double X,
    double Y,
    int Index)
{
    public static NodeSnapshot Of(NodeViewModel node, int index) =>
        new(node.Key, node.Name, node.RunType, node.StepType, node.X, node.Y, index);

    public NodeViewModel ToViewModel() => new()
    {
        Key = Key,
        Name = Name,
        RunType = RunType,
        StepType = StepType,
        X = X,
        Y = Y
    };
}

public sealed record ConnSnapshot(
    Guid Key,
    Guid SourceKey,
    Guid TargetKey,
    string Conditions,
    string RunType)
{
    public static ConnSnapshot Of(ConnectionViewModel connection) => new(
        connection.Key,
        connection.Source.Key,
        connection.Target.Key,
        connection.Conditions,
        connection.RunType);
}

public sealed record NodeMove(Guid Key, double OldX, double OldY, double NewX, double NewY);

public abstract class EditAction
{
    public abstract string Name { get; }
    internal abstract void Undo(FlowEditorViewModel viewModel);
    internal abstract void Redo(FlowEditorViewModel viewModel);
}

public sealed class AddNodeAction(NodeSnapshot node) : EditAction
{
    public override string Name => $"添加节点 {node.Index + 1}";

    internal override void Undo(FlowEditorViewModel viewModel)
    {
        if (viewModel.FindNode(node.Key) is { } current)
            viewModel.RemoveNodesCore([current]);
    }

    internal override void Redo(FlowEditorViewModel viewModel) =>
        viewModel.InsertNodeCore(node.ToViewModel(), node.Index);
}

public sealed class DeleteNodesAction(
    List<NodeSnapshot> nodes,
    List<ConnSnapshot> connections) : EditAction
{
    public override string Name => nodes.Count == 1
        ? $"删除节点 {nodes[0].Index + 1}"
        : $"删除 {nodes.Count} 个节点";

    internal override void Undo(FlowEditorViewModel viewModel)
    {
        foreach (var node in nodes.OrderBy(n => n.Index))
            viewModel.InsertNodeCore(node.ToViewModel(), node.Index);
        foreach (var connection in connections)
            viewModel.InsertConnectionCore(
                connection.SourceKey,
                connection.TargetKey,
                connection.Conditions,
                connection.RunType,
                connection.Key);
    }

    internal override void Redo(FlowEditorViewModel viewModel) =>
        viewModel.RemoveNodesCore(nodes
            .Select(n => viewModel.FindNode(n.Key))
            .Where(n => n != null)
            .Cast<NodeViewModel>());
}

public sealed class AddConnectionAction(ConnSnapshot connection) : EditAction
{
    public override string Name => "添加连线";

    internal override void Undo(FlowEditorViewModel viewModel)
    {
        if (viewModel.Connections.FirstOrDefault(c => c.Key == connection.Key) is { } current)
            viewModel.RemoveConnectionCore(current);
    }

    internal override void Redo(FlowEditorViewModel viewModel) =>
        viewModel.InsertConnectionCore(
            connection.SourceKey,
            connection.TargetKey,
            connection.Conditions,
            connection.RunType,
            connection.Key);
}

public sealed class DeleteConnectionAction(ConnSnapshot connection) : EditAction
{
    public override string Name => "删除连线";

    internal override void Undo(FlowEditorViewModel viewModel) =>
        viewModel.InsertConnectionCore(
            connection.SourceKey,
            connection.TargetKey,
            connection.Conditions,
            connection.RunType,
            connection.Key);

    internal override void Redo(FlowEditorViewModel viewModel)
    {
        if (viewModel.Connections.FirstOrDefault(c => c.Key == connection.Key) is { } current)
            viewModel.RemoveConnectionCore(current);
    }
}

public sealed class MoveNodesAction(List<NodeMove> moves) : EditAction
{
    public override string Name => moves.Count == 1 ? "移动节点" : $"移动 {moves.Count} 个节点";

    internal override void Undo(FlowEditorViewModel viewModel)
    {
        foreach (var move in moves)
            viewModel.SetNodePosition(move.Key, move.OldX, move.OldY);
    }

    internal override void Redo(FlowEditorViewModel viewModel)
    {
        foreach (var move in moves)
            viewModel.SetNodePosition(move.Key, move.NewX, move.NewY);
    }
}
