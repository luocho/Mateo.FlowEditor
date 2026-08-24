using System.Collections.Generic;
using System.Linq;

namespace FlowEditor.ViewModels
{
    // ===== 快照 =====
    public record NodeSnapshot(string Id, string NodeName, string ExeName, string NodeType,
                               string InputParameters, double X, double Y, int Index)
    {
        public static NodeSnapshot Of(NodeViewModel n, int index) =>
            new(n.Id, n.NodeName, n.ExeName, n.NodeType, n.InputParametersText, n.X, n.Y, index);

        public NodeViewModel ToViewModel() => new()
        {
            Id = Id,
            NodeName = NodeName,
            ExeName = ExeName,
            NodeType = NodeType,
            InputParametersText = InputParameters,
            X = X,
            Y = Y
        };
    }

    public record ConnSnapshot(string FromId, string ToId, string Condition);
    public record NodeMove(string Id, double OldX, double OldY, double NewX, double NewY);

    // ===== 命令基类 =====
    public abstract class EditAction
    {
        public abstract string Name { get; }
        internal abstract void Undo(FlowEditorViewModel vm);
        internal abstract void Redo(FlowEditorViewModel vm);
    }

    public class AddNodeAction : EditAction
    {
        private readonly NodeSnapshot _n;
        public AddNodeAction(NodeSnapshot n) { _n = n; }
        public override string Name => $"添加节点 {_n.Id}";
        internal override void Undo(FlowEditorViewModel vm)
        {
            var node = vm.FindNode(_n.Id);
            if (node != null) vm.RemoveNodesCore(new[] { node });
        }
        internal override void Redo(FlowEditorViewModel vm) => vm.InsertNodeCore(_n.ToViewModel(), _n.Index);
    }

    public class DeleteNodesAction : EditAction
    {
        private readonly List<NodeSnapshot> _nodes;
        private readonly List<ConnSnapshot> _conns;
        public DeleteNodesAction(List<NodeSnapshot> nodes, List<ConnSnapshot> conns) { _nodes = nodes; _conns = conns; }
        public override string Name => _nodes.Count == 1 ? $"删除节点 {_nodes[0].Id}" : $"删除 {_nodes.Count} 个节点";
        internal override void Undo(FlowEditorViewModel vm)
        {
            foreach (var s in _nodes.OrderBy(x => x.Index))
                vm.InsertNodeCore(s.ToViewModel(), s.Index);
            foreach (var c in _conns)
                vm.InsertConnectionCore(c.FromId, c.ToId, c.Condition);
        }
        internal override void Redo(FlowEditorViewModel vm)
        {
            var nodes = _nodes.Select(s => vm.FindNode(s.Id))
                              .Where(n => n != null).Cast<NodeViewModel>().ToList();
            vm.RemoveNodesCore(nodes);
        }
    }

    public class AddConnectionAction : EditAction
    {
        private readonly ConnSnapshot _c;
        public AddConnectionAction(ConnSnapshot c) { _c = c; }
        public override string Name => $"添加连线 {_c.FromId}→{_c.ToId}";
        internal override void Undo(FlowEditorViewModel vm)
        {
            var c = vm.Connections.FirstOrDefault(x => x.Source.Id == _c.FromId
                && x.Target.Id == _c.ToId && x.Condition == _c.Condition);
            if (c != null) vm.RemoveConnectionCore(c);
        }
        internal override void Redo(FlowEditorViewModel vm) => vm.InsertConnectionCore(_c.FromId, _c.ToId, _c.Condition);
    }

    public class DeleteConnectionAction : EditAction
    {
        private readonly ConnSnapshot _c;
        public DeleteConnectionAction(ConnSnapshot c) { _c = c; }
        public override string Name => $"删除连线 {_c.FromId}→{_c.ToId}";
        internal override void Undo(FlowEditorViewModel vm) => vm.InsertConnectionCore(_c.FromId, _c.ToId, _c.Condition);
        internal override void Redo(FlowEditorViewModel vm)
        {
            var c = vm.Connections.FirstOrDefault(x => x.Source.Id == _c.FromId
                && x.Target.Id == _c.ToId && x.Condition == _c.Condition);
            if (c != null) vm.RemoveConnectionCore(c);
        }
    }

    public class MoveNodesAction : EditAction
    {
        private readonly List<NodeMove> _moves;
        public MoveNodesAction(List<NodeMove> moves) { _moves = moves; }
        public override string Name => _moves.Count == 1 ? $"移动节点 {_moves[0].Id}" : $"移动 {_moves.Count} 个节点";
        internal override void Undo(FlowEditorViewModel vm)
        { foreach (var m in _moves) vm.SetNodePosition(m.Id, m.OldX, m.OldY); }
        internal override void Redo(FlowEditorViewModel vm)
        { foreach (var m in _moves) vm.SetNodePosition(m.Id, m.NewX, m.NewY); }
    }
}