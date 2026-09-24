using FlowEditor.Dialogs;
using FlowEditor.Configuration;
using FlowEditor.Models;
using Framework;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Windows;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;

namespace FlowEditor.ViewModels;

public abstract class ViewModelBase : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    protected void OnPropertyChanged([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

    protected bool Set<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return false;
        field = value;
        OnPropertyChanged(name);
        return true;
    }
}

public sealed class RelayCommand(Action<object?> execute, Predicate<object?>? canExecute = null) : ICommand
{
    public event EventHandler? CanExecuteChanged
    {
        add => CommandManager.RequerySuggested += value;
        remove => CommandManager.RequerySuggested -= value;
    }

    public bool CanExecute(object? parameter) => canExecute?.Invoke(parameter) ?? true;
    public void Execute(object? parameter) => execute(parameter);
}

public sealed class NodeViewModel : ViewModelBase
{
    public Guid Key { get; init; } = Guid.NewGuid();

    private int _id;
    public int Id
    {
        get => _id;
        internal set => Set(ref _id, value);
    }

    private string _name = "";
    public string Name
    {
        get => _name;
        set => Set(ref _name, value ?? "");
    }

    private string _runType = "";
    public string RunType
    {
        get => _runType;
        set => Set(ref _runType, value ?? "");
    }

    private FlowStepType _stepType = FlowStepType.Action;
    public FlowStepType StepType
    {
        get => _stepType;
        set
        {
            if (!Set(ref _stepType, value)) return;
            OnPropertyChanged(nameof(HeaderBrush));
            OnPropertyChanged(nameof(BodyBrush));
            OnPropertyChanged(nameof(ConfiguredColor));
            OnPropertyChanged(nameof(RunTypeLabel));
        }
    }

    private double _x;
    public double X { get => _x; set => Set(ref _x, value); }

    private double _y;
    public double Y { get => _y; set => Set(ref _y, value); }

    private double _width = 180;
    public double Width { get => _width; set => Set(ref _width, value); }

    private double _height = 72;
    public double Height { get => _height; set => Set(ref _height, value); }

    private bool _isSelected;
    public bool IsSelected { get => _isSelected; set => Set(ref _isSelected, value); }

    public Color EffectiveColor
    {
        get
        {
            try
            {
                if (ColorConverter.ConvertFromString(ConfiguredColor) is Color color) return color;
            }
            catch (FormatException) { }
            return StepType == FlowStepType.Flow
                ? Color.FromRgb(0x7C, 0x3A, 0xED)
                : Color.FromRgb(0x40, 0x9E, 0xFF);
        }
    }

    public string ConfiguredColor => EditorSettings.Current.GetNodeColor(StepType);
    public Brush HeaderBrush => new SolidColorBrush(EffectiveColor);
    public Brush BodyBrush => new SolidColorBrush(Lighten(EffectiveColor, 0.9));

    public string RunTypeLabel => StepType == FlowStepType.Flow ? "目标 Flow" : "RunType";
    public Point OutPortPoint => new(X + Width / 2, Y + Height);
    public Point InPortPoint => new(X + Width / 2, Y);

    public static NodeViewModel FromDef(NodeDef source) => new()
    {
        Id = source.Id,
        Name = source.Name,
        RunType = source.RunType,
        StepType = source.StepType,
        X = source.X ?? 0,
        Y = source.Y ?? 0
    };

    public NodeDef ToDef() => new()
    {
        Id = Id,
        Name = Name,
        RunType = RunType,
        StepType = StepType,
        X = X,
        Y = Y
    };

    private static Color Lighten(Color color, double amount) => Color.FromRgb(
        (byte)(color.R + (255 - color.R) * amount),
        (byte)(color.G + (255 - color.G) * amount),
        (byte)(color.B + (255 - color.B) * amount));
}

public sealed class ConnectionViewModel : ViewModelBase
{
    public Guid Key { get; init; } = Guid.NewGuid();
    public NodeViewModel Source { get; }
    public NodeViewModel Target { get; }
    public int FromStepId => Source.Id;
    public int NextStepId => Target.Id;

    private bool _isSelected;
    public bool IsSelected { get => _isSelected; set => Set(ref _isSelected, value); }

    private string _conditions = "";
    public string Conditions
    {
        get => _conditions;
        set => Set(ref _conditions, value ?? "");
    }

    private string _runType = "";
    public string RunType
    {
        get => _runType;
        set => Set(ref _runType, value ?? "");
    }

    private PathGeometry _geometry = new();
    public PathGeometry Geometry
    {
        get => _geometry;
        private set => Set(ref _geometry, value);
    }

    private double _labelX;
    public double LabelX { get => _labelX; private set => Set(ref _labelX, value); }

    private double _labelY;
    public double LabelY { get => _labelY; private set => Set(ref _labelY, value); }

    public ConnectionViewModel(NodeViewModel source, NodeViewModel target, string conditions, string runType)
    {
        Source = source;
        Target = target;
        _conditions = conditions;
        _runType = runType;
        source.PropertyChanged += OnNodeChanged;
        target.PropertyChanged += OnNodeChanged;
        Recalc();
    }

    public void Detach()
    {
        Source.PropertyChanged -= OnNodeChanged;
        Target.PropertyChanged -= OnNodeChanged;
    }

    public void RefreshTargetId() => OnPropertyChanged(nameof(NextStepId));

    private void OnNodeChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(NodeViewModel.Id))
        {
            if (ReferenceEquals(sender, Source)) OnPropertyChanged(nameof(FromStepId));
            if (ReferenceEquals(sender, Target)) RefreshTargetId();
        }
        if (e.PropertyName is nameof(NodeViewModel.X) or nameof(NodeViewModel.Y)
            or nameof(NodeViewModel.Width) or nameof(NodeViewModel.Height))
            Recalc();
    }

    private void Recalc()
    {
        var start = Source.OutPortPoint;
        var end = Target.InPortPoint;
        var offset = Math.Max(40, Math.Abs(end.Y - start.Y) * 0.5);
        var control1 = new Point(start.X, start.Y + offset);
        var control2 = new Point(end.X, end.Y - offset);
        var geometry = new PathGeometry();
        var figure = new PathFigure { StartPoint = start };
        figure.Segments.Add(new BezierSegment(control1, control2, end, true));
        geometry.Figures.Add(figure);

        var direction = end - control2;
        if (direction.Length > 0) direction.Normalize();
        geometry.Figures.Add(new PathFigure(end,
            [new LineSegment(end + Rotate(direction, 155) * 11, true)], false));
        geometry.Figures.Add(new PathFigure(end,
            [new LineSegment(end + Rotate(direction, -155) * 11, true)], false));
        Geometry = geometry;

        var middle = ((Vector)start + 3 * (Vector)control1 + 3 * (Vector)control2 + (Vector)end) / 8.0;
        LabelX = middle.X - 48;
        LabelY = middle.Y - 28;
    }

    private static Vector Rotate(Vector value, double degrees)
    {
        var radians = degrees * Math.PI / 180;
        return new Vector(
            value.X * Math.Cos(radians) - value.Y * Math.Sin(radians),
            value.X * Math.Sin(radians) + value.Y * Math.Cos(radians));
    }
}

public sealed class FlowEditorViewModel : ViewModelBase
{
    private readonly FlowDef _definition;
    private readonly Stack<EditAction> _undoStack = new();
    private readonly Stack<EditAction> _redoStack = new();

    public ObservableCollection<NodeViewModel> Nodes { get; } = [];
    public ObservableCollection<ConnectionViewModel> Connections { get; } = [];
    public ObservableCollection<ConnectionViewModel> SelectedOutgoingConnections { get; } = [];
    public IReadOnlyList<string> ConditionOptions { get; } = ["", "OK", "NO", "Ignored", "Completed"];
    public IReadOnlyList<FlowStepType> StepTypeOptions { get; } = [FlowStepType.Action, FlowStepType.Flow];

    public const double MinZoom = 0.25;
    public const double MaxZoom = 2.5;

    private double _zoom = 1;
    public double Zoom { get => _zoom; set => Set(ref _zoom, Math.Clamp(value, MinZoom, MaxZoom)); }

    private double _canvasWidth = 1600;
    public double CanvasWidth { get => _canvasWidth; set => Set(ref _canvasWidth, value); }

    private double _canvasHeight = 1200;
    public double CanvasHeight { get => _canvasHeight; set => Set(ref _canvasHeight, value); }

    private NodeViewModel? _selectedNode;
    public NodeViewModel? SelectedNode
    {
        get => _selectedNode;
        private set
        {
            if (!Set(ref _selectedNode, value)) return;
            RefreshSelectedConnections();
        }
    }

    private ConnectionViewModel? _selectedConnection;
    public ConnectionViewModel? SelectedConnection
    {
        get => _selectedConnection;
        private set
        {
            if (ReferenceEquals(_selectedConnection, value)) return;
            if (_selectedConnection != null) _selectedConnection.IsSelected = false;
            Set(ref _selectedConnection, value);
            if (_selectedConnection != null) _selectedConnection.IsSelected = true;
        }
    }

    public List<NodeViewModel> SelectedNodes => Nodes.Where(n => n.IsSelected).ToList();
    public int SelectedCount => Nodes.Count(n => n.IsSelected);
    public string StatusText => $"节点 {Nodes.Count} 个 · 连线 {Connections.Count} 条 · 选中 {SelectedCount} 个";
    public bool CanUndo => _undoStack.Count > 0;
    public bool CanRedo => _redoStack.Count > 0;
    public string UndoName => CanUndo ? _undoStack.Peek().Name : "";
    public string RedoName => CanRedo ? _redoStack.Peek().Name : "";

    public FlowEditorViewModel(FlowDef definition)
    {
        _definition = definition;
        Nodes.CollectionChanged += (_, _) => OnPropertyChanged(nameof(StatusText));
        Connections.CollectionChanged += (_, _) => OnPropertyChanged(nameof(StatusText));
        Build();
    }

    private void Build()
    {
        var map = new Dictionary<int, NodeViewModel>();
        foreach (var source in _definition.Nodes)
        {
            var node = NodeViewModel.FromDef(source);
            node.PropertyChanged += NodePropertyChanged;
            Nodes.Add(node);
            map.TryAdd(node.Id, node);
        }

        if (_definition.Nodes.Count == 0 || _definition.Nodes.Any(n => !n.X.HasValue || !n.Y.HasValue))
            AutoLayout();

        foreach (var source in _definition.Connections)
            if (map.TryGetValue(source.FromNodeId, out var from) && map.TryGetValue(source.ToNodeId, out var to))
                Connections.Add(new ConnectionViewModel(from, to, source.Conditions, source.RunType));

        RenumberNodes();
        RecalcCanvasSize();
    }

    private void AutoLayout()
    {
        var next = _definition.Connections
            .GroupBy(c => c.FromNodeId)
            .ToDictionary(g => g.Key, g => g.Select(c => c.ToNodeId).ToList());
        var depth = new Dictionary<int, int>();
        if (_definition.Nodes.FirstOrDefault() is { } first)
        {
            var queue = new Queue<(int Id, int Depth)>();
            depth[first.Id] = 0;
            queue.Enqueue((first.Id, 0));
            while (queue.Count > 0)
            {
                var (id, currentDepth) = queue.Dequeue();
                if (!next.TryGetValue(id, out var targets)) continue;
                foreach (var target in targets.Where(target => !depth.ContainsKey(target)))
                {
                    depth[target] = currentDepth + 1;
                    queue.Enqueue((target, currentDepth + 1));
                }
            }
        }

        var orphanColumn = (depth.Count == 0 ? 0 : depth.Values.Max()) + 1;
        var rows = new Dictionary<int, int>();
        foreach (var node in Nodes)
        {
            var column = depth.GetValueOrDefault(node.Id, orphanColumn);
            var row = rows.GetValueOrDefault(column);
            rows[column] = row + 1;
            node.X = 60 + column * 250;
            node.Y = 50 + row * 130;
        }
    }

    public void ClearSelection()
    {
        foreach (var node in Nodes) node.IsSelected = false;
        SelectedNode = null;
        SelectedConnection = null;
        NotifySelection();
    }

    public void SelectOnly(NodeViewModel? node)
    {
        SelectedConnection = null;
        foreach (var current in Nodes) current.IsSelected = ReferenceEquals(current, node);
        SelectedNode = node;
        NotifySelection();
    }

    public void ToggleSelect(NodeViewModel node)
    {
        SelectedConnection = null;
        node.IsSelected = !node.IsSelected;
        SelectedNode = node.IsSelected ? node : Nodes.LastOrDefault(n => n.IsSelected);
        NotifySelection();
    }

    public void SelectAll()
    {
        SelectedConnection = null;
        foreach (var node in Nodes) node.IsSelected = true;
        SelectedNode = Nodes.LastOrDefault();
        NotifySelection();
    }

    public void SelectInRect(Rect rectangle, bool additive)
    {
        SelectedConnection = null;
        if (!additive)
            foreach (var node in Nodes) node.IsSelected = false;

        NodeViewModel? last = null;
        foreach (var node in Nodes.Where(n => new Rect(n.X, n.Y, n.Width, n.Height).IntersectsWith(rectangle)))
        {
            node.IsSelected = true;
            last = node;
        }
        SelectedNode = last ?? Nodes.LastOrDefault(n => n.IsSelected);
        NotifySelection();
    }

    public void SelectConnection(ConnectionViewModel connection)
    {
        foreach (var node in Nodes) node.IsSelected = false;
        SelectedNode = null;
        SelectedConnection = connection;
        NotifySelection();
    }

    public NodeViewModel AddNode(Point position, FlowStepType stepType = FlowStepType.Action)
    {
        var node = new NodeViewModel
        {
            Id = Nodes.Count + 1,
            Name = stepType == FlowStepType.Flow ? "NewFlow" : "NewAction",
            StepType = stepType,
            X = Math.Max(0, position.X - 90),
            Y = Math.Max(0, position.Y - 36)
        };
        InsertNodeCore(node, Nodes.Count);
        Push(new AddNodeAction(NodeSnapshot.Of(node, Nodes.Count - 1)));
        SelectOnly(node);
        return node;
    }

    public void AddConnection(NodeViewModel source, NodeViewModel target)
    {
        if (Connections.Any(c => ReferenceEquals(c.Source, source) && ReferenceEquals(c.Target, target)))
            return;
        var connection = InsertConnectionCore(source.Key, target.Key, "", target.RunType);
        if (connection != null)
            Push(new AddConnectionAction(ConnSnapshot.Of(connection)));
    }

    public void RemoveNode(NodeViewModel node) => RemoveNodes([node]);

    public void RemoveNodes(IReadOnlyCollection<NodeViewModel> nodes)
    {
        if (nodes.Count == 0) return;
        var nodeSnapshots = nodes.Select(n => NodeSnapshot.Of(n, Nodes.IndexOf(n))).ToList();
        var connectionSnapshots = Connections
            .Where(c => nodes.Contains(c.Source) || nodes.Contains(c.Target))
            .Select(ConnSnapshot.Of)
            .ToList();
        RemoveNodesCore(nodes);
        Push(new DeleteNodesAction(nodeSnapshots, connectionSnapshots));
    }

    public void RemoveConnection(ConnectionViewModel connection)
    {
        var snapshot = ConnSnapshot.Of(connection);
        RemoveConnectionCore(connection);
        Push(new DeleteConnectionAction(snapshot));
    }

    public void RecordMove(IReadOnlyDictionary<NodeViewModel, Point> starts)
    {
        var moves = starts
            .Where(pair => Math.Abs(pair.Key.X - pair.Value.X) > 0.01
                        || Math.Abs(pair.Key.Y - pair.Value.Y) > 0.01)
            .Select(pair => new NodeMove(pair.Key.Key, pair.Value.X, pair.Value.Y, pair.Key.X, pair.Key.Y))
            .ToList();
        if (moves.Count > 0) Push(new MoveNodesAction(moves));
    }

    internal NodeViewModel? FindNode(Guid key) => Nodes.FirstOrDefault(n => n.Key == key);

    internal void InsertNodeCore(NodeViewModel node, int index)
    {
        if (FindNode(node.Key) != null) return;
        node.PropertyChanged += NodePropertyChanged;
        Nodes.Insert(Math.Clamp(index, 0, Nodes.Count), node);
        RenumberNodes();
        RecalcCanvasSize();
    }

    internal ConnectionViewModel? InsertConnectionCore(
        Guid sourceKey, Guid targetKey, string conditions, string runType, Guid? key = null)
    {
        var source = FindNode(sourceKey);
        var target = FindNode(targetKey);
        if (source == null || target == null) return null;
        if (Connections.Any(c => c.Source.Key == sourceKey && c.Target.Key == targetKey
                              && c.Conditions == conditions && c.RunType == runType))
            return null;
        var connection = new ConnectionViewModel(source, target, conditions, runType)
        {
            Key = key ?? Guid.NewGuid()
        };
        Connections.Add(connection);
        RefreshSelectedConnections();
        return connection;
    }

    internal void RemoveNodesCore(IEnumerable<NodeViewModel> nodes)
    {
        foreach (var node in nodes.OrderByDescending(Nodes.IndexOf).ToList())
        {
            foreach (var connection in Connections
                         .Where(c => ReferenceEquals(c.Source, node) || ReferenceEquals(c.Target, node)).ToList())
                RemoveConnectionCore(connection);
            node.PropertyChanged -= NodePropertyChanged;
            if (ReferenceEquals(SelectedNode, node)) SelectedNode = null;
            Nodes.Remove(node);
        }
        RenumberNodes();
        NotifySelection();
        RecalcCanvasSize();
    }

    internal void RemoveConnectionCore(ConnectionViewModel connection)
    {
        if (ReferenceEquals(SelectedConnection, connection)) SelectedConnection = null;
        connection.Detach();
        Connections.Remove(connection);
        RefreshSelectedConnections();
    }

    internal void SetNodePosition(Guid key, double x, double y)
    {
        if (FindNode(key) is not { } node) return;
        node.X = x;
        node.Y = y;
    }

    public void Undo()
    {
        if (_undoStack.Count == 0) return;
        var action = _undoStack.Pop();
        action.Undo(this);
        _redoStack.Push(action);
        RaiseUndoRedo();
    }

    public void Redo()
    {
        if (_redoStack.Count == 0) return;
        var action = _redoStack.Pop();
        action.Redo(this);
        _undoStack.Push(action);
        RaiseUndoRedo();
    }

    public void Flush()
    {
        _definition.Nodes = Nodes.Select(n => n.ToDef()).ToList();
        _definition.Connections = Connections.Select(c => new ConnectionDef
        {
            FromNodeId = c.Source.Id,
            ToNodeId = c.Target.Id,
            Conditions = c.Conditions,
            RunType = c.RunType
        }).ToList();
    }

    private void RenumberNodes()
    {
        for (var index = 0; index < Nodes.Count; index++)
            Nodes[index].Id = index + 1;
        foreach (var connection in Connections) connection.RefreshTargetId();
    }

    private void Push(EditAction action)
    {
        _undoStack.Push(action);
        _redoStack.Clear();
        RaiseUndoRedo();
    }

    private void RaiseUndoRedo()
    {
        OnPropertyChanged(nameof(CanUndo));
        OnPropertyChanged(nameof(CanRedo));
        OnPropertyChanged(nameof(UndoName));
        OnPropertyChanged(nameof(RedoName));
        CommandManager.InvalidateRequerySuggested();
    }

    private void NotifySelection()
    {
        OnPropertyChanged(nameof(SelectedCount));
        OnPropertyChanged(nameof(StatusText));
        CommandManager.InvalidateRequerySuggested();
    }

    private void RefreshSelectedConnections()
    {
        SelectedOutgoingConnections.Clear();
        if (SelectedNode == null) return;
        foreach (var connection in Connections.Where(c => ReferenceEquals(c.Source, SelectedNode)))
            SelectedOutgoingConnections.Add(connection);
    }

    private void NodePropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(NodeViewModel.X) or nameof(NodeViewModel.Y)
            or nameof(NodeViewModel.Width) or nameof(NodeViewModel.Height))
            RecalcCanvasSize();
    }

    private void RecalcCanvasSize()
    {
        var width = 1600d;
        var height = 1200d;
        foreach (var node in Nodes)
        {
            width = Math.Max(width, node.X + node.Width + 400);
            height = Math.Max(height, node.Y + node.Height + 300);
        }
        CanvasWidth = width;
        CanvasHeight = height;
    }
}

public sealed class MainViewModel : ViewModelBase
{
    public ObservableCollection<FlowDef> Flows { get; } = [];
    public ObservableCollection<string> TaskTypes { get; } = [];
    public ObservableCollection<string> FlowTargets { get; } = [];

    private FlowDef? _selectedFlow;
    public FlowDef? SelectedFlow
    {
        get => _selectedFlow;
        set
        {
            if (ReferenceEquals(_selectedFlow, value)) return;
            Editor?.Flush();
            Set(ref _selectedFlow, value);
            Editor = value == null ? null : new FlowEditorViewModel(value);
        }
    }

    private string _flowListName = "FlowList";
    public string FlowListName
    {
        get => _flowListName;
        private set => Set(ref _flowListName, string.IsNullOrWhiteSpace(value) ? "FlowList" : value);
    }

    private FlowEditorViewModel? _editor;
    public FlowEditorViewModel? Editor
    {
        get => _editor;
        private set
        {
            Set(ref _editor, value);
            CommandManager.InvalidateRequerySuggested();
        }
    }

    private string? _filePath;
    public string? FilePath
    {
        get => _filePath;
        set
        {
            Set(ref _filePath, value);
            OnPropertyChanged(nameof(Title));
        }
    }

    private string? _taskDllPath;
    public string? TaskDllPath
    {
        get => _taskDllPath;
        private set
        {
            Set(ref _taskDllPath, value);
            OnPropertyChanged(nameof(TaskDllStatus));
        }
    }

    public string Title => "Flow 编辑器" + (string.IsNullOrEmpty(FilePath) ? "" : $"  -  {FilePath}");
    public string TaskDllStatus => string.IsNullOrEmpty(TaskDllPath)
        ? "未加载任务 DLL"
        : $"任务 DLL：{Path.GetFileName(TaskDllPath)}";

    public ICommand OpenCommand { get; }
    public ICommand SaveCommand { get; }
    public ICommand SaveAsCommand { get; }
    public ICommand ExitCommand { get; }
    public ICommand AddFlowCommand { get; }
    public ICommand RemoveFlowCommand { get; }
    public ICommand DeleteNodeCommand { get; }
    public ICommand UndoCommand { get; }
    public ICommand RedoCommand { get; }
    public ICommand SelectAllCommand { get; }
    public ICommand ValidateCommand { get; }
    public ICommand LoadDllCommand { get; }

    public MainViewModel()
    {
        OpenCommand = new RelayCommand(_ => Open());
        SaveCommand = new RelayCommand(_ => Save(), _ => Flows.Count > 0);
        SaveAsCommand = new RelayCommand(_ => SaveAs(), _ => Flows.Count > 0);
        ExitCommand = new RelayCommand(_ => Application.Current.Shutdown());
        AddFlowCommand = new RelayCommand(_ => PromptAddFlow());
        RemoveFlowCommand = new RelayCommand(
            _ => { if (SelectedFlow is { } flow) RemoveFlow(flow); },
            _ => SelectedFlow != null);
        DeleteNodeCommand = new RelayCommand(
            _ => Editor?.RemoveNodes(Editor.SelectedNodes),
            _ => (Editor?.SelectedCount ?? 0) > 0);
        UndoCommand = new RelayCommand(_ => Editor?.Undo(), _ => Editor?.CanUndo == true);
        RedoCommand = new RelayCommand(_ => Editor?.Redo(), _ => Editor?.CanRedo == true);
        SelectAllCommand = new RelayCommand(
            _ => Editor?.SelectAll(),
            _ => Editor != null && Keyboard.FocusedElement is not TextBoxBase);
        LoadDllCommand = new RelayCommand(_ => LoadDll());
        ValidateCommand = new RelayCommand(_ => ShowValidation(), _ => Flows.Count > 0);
    }

    public bool NavigateToFlow(NodeViewModel node)
    {
        if (node.StepType != FlowStepType.Flow) return false;
        var target = Flows.FirstOrDefault(f => string.Equals(f.Name, node.RunType, StringComparison.OrdinalIgnoreCase));
        if (target == null && int.TryParse(node.RunType, out var id))
            target = Flows.FirstOrDefault(f => f.Id == id);
        if (target == null) return false;
        SelectedFlow = target;
        return true;
    }

    private void Open()
    {
        var dialog = new OpenFileDialog
        {
            Filter = "FlowList protobuf 文件 (*.mf)|*.mf|所有文件 (*.*)|*.*"
        };
        if (dialog.ShowDialog() != true) return;
        try
        {
            var flowList = FlowListCodec.Read(dialog.FileName);
            Editor = null;
            _selectedFlow = null;
            Flows.Clear();
            foreach (var flow in flowList.Flows) Flows.Add(flow);
            FlowListName = flowList.Name;
            FilePath = dialog.FileName;
            RefreshFlowTargets();
            SelectedFlow = Flows.FirstOrDefault();
        }
        catch (Exception exception)
        {
            MessageBox.Show($"打开文件失败：{exception.Message}", "错误",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void Save()
    {
        if (FilePath == null || !string.Equals(Path.GetExtension(FilePath), ".mf", StringComparison.OrdinalIgnoreCase))
            SaveAs();
        else WriteFile(FilePath);
    }

    private void SaveAs()
    {
        var dialog = new SaveFileDialog
        {
            Filter = "FlowList protobuf 文件 (*.mf)|*.mf",
            DefaultExt = ".mf",
            AddExtension = true,
            FileName = GetSafeFileName(FlowListName) + ".mf"
        };
        if (dialog.ShowDialog() != true) return;
        if (FilePath == null) FlowListName = Path.GetFileNameWithoutExtension(dialog.FileName);
        WriteFile(EnsureMfExtension(dialog.FileName));
    }

    private void WriteFile(string path)
    {
        path = EnsureMfExtension(path);
        try
        {
            Editor?.Flush();
            if (!ValidateAll(out var problems))
            {
                var result = MessageBox.Show(
                    "校验发现以下问题：\n\n" + problems + "\n是否仍然保存？",
                    "保存校验", MessageBoxButton.YesNo, MessageBoxImage.Warning);
                if (result != MessageBoxResult.Yes) return;
            }
            FlowListCodec.Write(path, FlowListName, Flows);
            FilePath = path;
        }
        catch (Exception exception)
        {
            MessageBox.Show($"保存失败：{exception.Message}", "错误",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void LoadDll()
    {
        var dialog = new OpenFileDialog
        {
            Title = "加载任务 DLL",
            Filter = "程序集 (*.dll)|*.dll|所有文件 (*.*)|*.*"
        };
        if (dialog.ShowDialog() != true) return;
        try
        {
            using var loader = new TaskDllLoader(dialog.FileName);
            var names = loader.GetITaskTypeNames(dialog.FileName);
            TaskTypes.Clear();
            foreach (var name in names) TaskTypes.Add(name);
            TaskDllPath = dialog.FileName;
            if (names.Count == 0)
                MessageBox.Show("DLL 加载成功，但未找到实现 ITask 接口的类。", "提示",
                    MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception exception)
        {
            MessageBox.Show($"加载 DLL 失败：{exception.Message}", "错误",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void ShowValidation()
    {
        Editor?.Flush();
        var valid = ValidateAll(out var problems);
        MessageBox.Show(valid ? "校验通过，未发现问题。" : "发现以下问题：\n\n" + problems,
            "校验配置", MessageBoxButton.OK,
            valid ? MessageBoxImage.Information : MessageBoxImage.Warning);
    }

    private bool ValidateAll(out string message)
    {
        var builder = new StringBuilder();
        foreach (var group in Flows.GroupBy(f => f.Id).Where(g => g.Count() > 1))
            builder.AppendLine($"· Flow Id 重复：{group.Key} ×{group.Count()}");

        foreach (var flow in Flows)
        {
            if (string.IsNullOrWhiteSpace(flow.Name))
                builder.AppendLine($"· [Flow {flow.Id}] Name 不能为空");
            foreach (var group in flow.Nodes.GroupBy(n => n.Id).Where(g => g.Count() > 1))
                builder.AppendLine($"· [{flow.Name}] 节点 Id 重复：{group.Key} ×{group.Count()}");
            for (var index = 0; index < flow.Nodes.Count; index++)
                if (flow.Nodes[index].Id != index + 1)
                    builder.AppendLine($"· [{flow.Name}] 节点 Id 必须从 1 连续递增");

            var ids = flow.Nodes.Select(n => n.Id).ToHashSet();
            foreach (var connection in flow.Connections)
            {
                if (!ids.Contains(connection.FromNodeId))
                    builder.AppendLine($"· [{flow.Name}] 连线起点不存在：{connection.FromNodeId}");
                if (!ids.Contains(connection.ToNodeId))
                    builder.AppendLine($"· [{flow.Name}] 连线终点不存在：{connection.ToNodeId}");
            }
            foreach (var group in flow.Connections
                         .GroupBy(c => (c.FromNodeId, c.ToNodeId, c.Conditions, c.RunType))
                         .Where(g => g.Count() > 1))
                builder.AppendLine($"· [{flow.Name}] 存在重复连线：{group.Key.FromNodeId} → {group.Key.ToNodeId}");

            foreach (var node in flow.Nodes.Where(n => n.StepType == FlowStepType.Flow))
                if (!Flows.Any(f => string.Equals(f.Name, node.RunType, StringComparison.OrdinalIgnoreCase)
                                 || f.Id.ToString() == node.RunType))
                    builder.AppendLine($"· [{flow.Name}] FLOW 节点 {node.Id} 的目标不存在：\"{node.RunType}\"");
        }

        message = builder.ToString();
        return builder.Length == 0;
    }

    public FlowDef CreateFlow(string name)
    {
        name = name.Trim();
        if (ValidateFlowName(name) is { } error) throw new ArgumentException(error, nameof(name));
        var id = Flows.Count == 0 ? 1 : Flows.Max(f => f.Id) + 1;
        var flow = new FlowDef { Id = id, Name = name };
        flow.Nodes.Add(new NodeDef
        {
            Id = 1,
            Name = "NewAction",
            StepType = FlowStepType.Action,
            X = 80,
            Y = 60
        });
        Flows.Add(flow);
        RefreshFlowTargets();
        SelectedFlow = flow;
        return flow;
    }

    public void RenameFlow(FlowDef flow)
    {
        var dialog = new FlowNameDialog("修改流程名称", flow.Name,
            name => ValidateFlowName(name, flow));
        if (Application.Current.MainWindow is { } owner) dialog.Owner = owner;
        if (dialog.ShowDialog() != true || dialog.FlowName == flow.Name) return;
        if (MessageBox.Show($"确定将流程“{flow.Name}”修改为“{dialog.FlowName}”吗？", "确认修改",
                MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
            return;

        Editor?.Flush();
        var oldName = flow.Name;
        flow.Name = dialog.FlowName;
        foreach (var node in Flows.SelectMany(f => f.Nodes)
                     .Where(n => n.StepType == FlowStepType.Flow
                              && string.Equals(n.RunType, oldName, StringComparison.OrdinalIgnoreCase)))
            node.RunType = dialog.FlowName;
        RefreshFlowTargets();
        CollectionViewSource.GetDefaultView(Flows).Refresh();
        Editor = _selectedFlow == null ? null : new FlowEditorViewModel(_selectedFlow);
    }

    public void RemoveFlow(FlowDef flow)
    {
        if (MessageBox.Show($"确定删除流程“{flow.Name}”吗？此操作无法撤销。", "确认删除",
                MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes)
            return;
        Editor?.Flush();
        var index = Flows.IndexOf(flow);
        if (index < 0) return;
        var removingSelected = ReferenceEquals(flow, SelectedFlow);
        Flows.Remove(flow);
        RefreshFlowTargets();
        if (removingSelected)
            SelectedFlow = Flows.Count == 0 ? null : Flows[Math.Min(index, Flows.Count - 1)];
    }

    private void PromptAddFlow()
    {
        var dialog = new FlowNameDialog("新建流程", "", name => ValidateFlowName(name));
        if (Application.Current.MainWindow is { } owner) dialog.Owner = owner;
        if (dialog.ShowDialog() == true) CreateFlow(dialog.FlowName);
    }

    private string? ValidateFlowName(string name, FlowDef? except = null)
    {
        if (string.IsNullOrWhiteSpace(name)) return "流程名称不能为空。";
        if (Flows.Any(f => !ReferenceEquals(f, except)
                        && string.Equals(f.Name, name, StringComparison.OrdinalIgnoreCase)))
            return "流程名称不能重复。";
        return null;
    }

    private void RefreshFlowTargets()
    {
        FlowTargets.Clear();
        foreach (var name in Flows.Select(f => f.Name).Where(name => !string.IsNullOrWhiteSpace(name)))
            FlowTargets.Add(name);
    }

    private static string GetSafeFileName(string name)
    {
        var invalid = Path.GetInvalidFileNameChars().ToHashSet();
        var result = string.Concat(name.Select(c => invalid.Contains(c) ? '_' : c));
        return string.IsNullOrWhiteSpace(result) ? "flow" : result;
    }

    internal static string EnsureMfExtension(string path) => Path.ChangeExtension(path, ".mf");
}
