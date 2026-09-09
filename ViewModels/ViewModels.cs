using FlowEditor.Models;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;

namespace FlowEditor.ViewModels
{
    public abstract class ViewModelBase : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        protected bool Set<T>(ref T field, T value, [CallerMemberName] string? name = null)
        {
            if (EqualityComparer<T>.Default.Equals(field, value)) return false;
            field = value; OnPropertyChanged(name); return true;
        }
    }

    public class RelayCommand : ICommand
    {
        private readonly Action<object?> _exec; private readonly Predicate<object?>? _can;
        public RelayCommand(Action<object?> exec, Predicate<object?>? can = null) { _exec = exec; _can = can; }
        public event EventHandler? CanExecuteChanged
        { add => CommandManager.RequerySuggested += value; remove => CommandManager.RequerySuggested -= value; }
        public bool CanExecute(object? p) => _can?.Invoke(p) ?? true;
        public void Execute(object? p) => _exec(p);
    }

    // ================= 节点 =================
    public class NodeViewModel : ViewModelBase
    {
        private string _id = "";
        public string Id { get => _id; set => Set(ref _id, value); }

        private string _nodeName = "";
        public string NodeName { get => _nodeName; set => Set(ref _nodeName, value); }

        private string _exeName = "";
        public string ExeName { get => _exeName; set => Set(ref _exeName, value); }

        private string _nodeType = "Func";
        public string NodeType
        {
            get => _nodeType;
            set
            {
                var normalized = string.Equals(value, "Flow", StringComparison.OrdinalIgnoreCase)
                    ? "SubFlow"
                    : value;
                if (Set(ref _nodeType, normalized))
                {
                    if (!CanSelectExeName) ExeName = "";
                    OnPropertyChanged(nameof(HeaderBrush));
                    OnPropertyChanged(nameof(BodyBrush));
                    OnPropertyChanged(nameof(ExeNameLabel));
                    OnPropertyChanged(nameof(CanSelectExeName));
                    OnPropertyChanged(nameof(CanChangeNodeKind));
                    OnPropertyChanged(nameof(InPortVisibility));
                    OnPropertyChanged(nameof(OutPortVisibility));
                }
            }
        }

        private double _x, _y;
        public double X { get => _x; set => Set(ref _x, value); }
        public double Y { get => _y; set => Set(ref _y, value); }

        private double _width = 180, _height = 100;
        public double Width { get => _width; set => Set(ref _width, value); }
        public double Height { get => _height; set => Set(ref _height, value); }

        private bool _isSelected;
        public bool IsSelected { get => _isSelected; set => Set(ref _isSelected, value); }

        private string _inputParametersText = "";
        public string InputParametersText { get => _inputParametersText; set => Set(ref _inputParametersText, value); }

        public Brush HeaderBrush => NodeType switch
        {
            "Start" => new SolidColorBrush(Color.FromRgb(0x67, 0xC2, 0x3A)),
            "End" => new SolidColorBrush(Color.FromRgb(0x90, 0x93, 0x99)),
            "SubFlow" => new SolidColorBrush(Color.FromRgb(0x7C, 0x3A, 0xED)),
            _ => new SolidColorBrush(Color.FromRgb(0x40, 0x9E, 0xFF)),
        };
        public Brush BodyBrush => NodeType switch
        {
            "Start" => new SolidColorBrush(Color.FromRgb(0xF0, 0xF9, 0xEB)),
            "End" => new SolidColorBrush(Color.FromRgb(0xF4, 0xF4, 0xF5)),
            "SubFlow" => new SolidColorBrush(Color.FromRgb(0xF5, 0xF0, 0xFF)),
            _ => new SolidColorBrush(Color.FromRgb(0xEC, 0xF5, 0xFF)),
        };
        public string ExeNameLabel => NodeType == "SubFlow" ? "目标 Flow" : "ExeName";
        public bool CanSelectExeName => NodeType is not ("Start" or "End");
        public bool CanChangeNodeKind => NodeType is "Func" or "SubFlow";
        public Visibility InPortVisibility => NodeType == "Start" ? Visibility.Collapsed : Visibility.Visible;
        public Visibility OutPortVisibility => NodeType == "End" ? Visibility.Collapsed : Visibility.Visible;

        public Point OutPortPoint => new(X + Width / 2, Y + Height);
        public Point InPortPoint => new(X + Width / 2, Y);

        public static NodeViewModel FromDef(NodeDef d) => new()
        {
            Id = d.Id ?? "",
            NodeName = d.NodeName ?? "",
            ExeName = d.ExeName ?? "",
            NodeType = string.IsNullOrEmpty(d.NodeType) ? "Func" : d.NodeType,
            InputParametersText = ToText(d.InputParameters)
        };

        public NodeDef ToDef() => new()
        {
            Id = Id,
            NodeName = NodeName,
            ExeName = CanSelectExeName ? ExeName : "",
            NodeType = NodeType,
            InputParameters = FromText(InputParametersText),
            X = X,
            Y = Y
        };

        private static string ToText(object? p) => p switch
        {
            null => "",
            JsonElement je when je.ValueKind == JsonValueKind.String => je.GetString() ?? "",
            JsonElement je => je.GetRawText(),
            string s => s,
            _ => p.ToString() ?? ""
        };

        private static object FromText(string t)
        {
            t = (t ?? "").Trim();
            if (t.Length == 0) return "";
            if (t.StartsWith("{") || t.StartsWith("["))
            {
                try { return JsonSerializer.Deserialize<JsonElement>(t); } catch { }
            }
            return t;
        }
    }

    // ================= 连线 =================
    public class ConnectionViewModel : ViewModelBase
    {
        public NodeViewModel Source { get; }
        public NodeViewModel Target { get; }

        private string _condition = "Yes";
        public string Condition { get => _condition; set => Set(ref _condition, value); }

        private PathGeometry _geometry = new();
        public PathGeometry Geometry { get => _geometry; private set => Set(ref _geometry, value); }

        private double _labelX, _labelY;
        public double LabelX { get => _labelX; private set => Set(ref _labelX, value); }
        public double LabelY { get => _labelY; private set => Set(ref _labelY, value); }

        public ConnectionViewModel(NodeViewModel s, NodeViewModel t, string condition)
        {
            Source = s; Target = t; _condition = condition;
            s.PropertyChanged += OnNodeChanged;
            t.PropertyChanged += OnNodeChanged;
            Recalc();
        }

        public void Detach()
        {
            Source.PropertyChanged -= OnNodeChanged;
            Target.PropertyChanged -= OnNodeChanged;
        }

        private void OnNodeChanged(object? o, PropertyChangedEventArgs e)
        {
            if (e.PropertyName is nameof(NodeViewModel.X) or nameof(NodeViewModel.Y)
                or nameof(NodeViewModel.Width) or nameof(NodeViewModel.Height))
                Recalc();
        }

        private void Recalc()
        {
            var p0 = Source.OutPortPoint;
            var p3 = Target.InPortPoint;
            double off = Math.Max(40, Math.Abs(p3.Y - p0.Y) * 0.5);
            var c1 = new Point(p0.X, p0.Y + off);
            var c2 = new Point(p3.X, p3.Y - off);

            var g = new PathGeometry();
            var fig = new PathFigure { StartPoint = p0 };
            fig.Segments.Add(new BezierSegment(c1, c2, p3, true));
            g.Figures.Add(fig);

            var dir = p3 - c2;
            if (dir.Length > 0) dir.Normalize();
            g.Figures.Add(new PathFigure(p3, new[] { new LineSegment(p3 + Rotate(dir, 155) * 11, true) }, false));
            g.Figures.Add(new PathFigure(p3, new[] { new LineSegment(p3 + Rotate(dir, -155) * 11, true) }, false));
            Geometry = g;

            var mid = ((Vector)p0 + 3 * (Vector)c1 + 3 * (Vector)c2 + (Vector)p3) / 8.0;
            LabelX = mid.X - 48;
            LabelY = mid.Y - 28;
        }

        private static Vector Rotate(Vector v, double deg)
        {
            double r = deg * Math.PI / 180;
            return new Vector(v.X * Math.Cos(r) - v.Y * Math.Sin(r),
                              v.X * Math.Sin(r) + v.Y * Math.Cos(r));
        }
    }

    // ================= 单个 Flow 的画布 =================
    public class FlowEditorViewModel : ViewModelBase
    {
        private readonly FlowDef _def;

        public ObservableCollection<NodeViewModel> Nodes { get; } = new();
        public ObservableCollection<ConnectionViewModel> Connections { get; } = new();

        public IReadOnlyList<string> ConditionOptions { get; } = new[] { "", "Yes", "No", "Ignored", "Completed" };
        public IReadOnlyList<string> NodeTypeOptions { get; } = new[] { "Func", "SubFlow" };

        // ----- 缩放 -----
        public const double MinZoom = 0.25, MaxZoom = 2.5;
        private double _zoom = 1.0;
        public double Zoom { get => _zoom; set => Set(ref _zoom, Math.Clamp(value, MinZoom, MaxZoom)); }

        // ----- 画布尺寸（随节点自动扩展） -----
        private double _canvasWidth = 1600;
        public double CanvasWidth { get => _canvasWidth; set => Set(ref _canvasWidth, value); }
        private double _canvasHeight = 1200;
        public double CanvasHeight { get => _canvasHeight; set => Set(ref _canvasHeight, value); }

        // ----- 选择 -----
        private NodeViewModel? _selectedNode;
        public NodeViewModel? SelectedNode { get => _selectedNode; private set => Set(ref _selectedNode, value); }
        public List<NodeViewModel> SelectedNodes => Nodes.Where(n => n.IsSelected).ToList();
        public int SelectedCount => Nodes.Count(n => n.IsSelected);

        public string StatusText => $"节点 {Nodes.Count} 个 · 连线 {Connections.Count} 条 · 选中 {SelectedCount} 个";

        // ----- 撤销/重做 -----
        private readonly Stack<EditAction> _undoStack = new();
        private readonly Stack<EditAction> _redoStack = new();
        public bool CanUndo => _undoStack.Count > 0;
        public bool CanRedo => _redoStack.Count > 0;
        public string UndoName => CanUndo ? _undoStack.Peek().Name : "";
        public string RedoName => CanRedo ? _redoStack.Peek().Name : "";

        public FlowEditorViewModel(FlowDef def)
        {
            _def = def;
            Nodes.CollectionChanged += (_, __) => OnPropertyChanged(nameof(StatusText));
            Connections.CollectionChanged += (_, __) => OnPropertyChanged(nameof(StatusText));
            Build();
        }

        private void Build()
        {
            var map = new Dictionary<string, NodeViewModel>();
            foreach (var d in _def.Nodes)
            {
                var n = NodeViewModel.FromDef(d);
                n.PropertyChanged += Node_PropertyChanged;
                Nodes.Add(n);
                if (!string.IsNullOrEmpty(n.Id)) map[n.Id] = n;
            }

            if (_def.Nodes.Count > 0 && _def.Nodes.All(d => d.X.HasValue && d.Y.HasValue))
                foreach (var n in Nodes)
                {
                    var d = _def.Nodes.First(x => x.Id == n.Id);
                    n.X = d.X!.Value; n.Y = d.Y!.Value;
                }
            else
                AutoLayout();

            foreach (var c in _def.Connections)
                if (map.TryGetValue(c.FromNodeId ?? "", out var s) && map.TryGetValue(c.ToNodeId ?? "", out var t))
                    Connections.Add(new ConnectionViewModel(s, t, c.Condition ?? ""));

            RecalcCanvasSize();
        }

        private void AutoLayout()
        {
            var next = _def.Connections
                .GroupBy(c => c.FromNodeId)
                .ToDictionary(g => g.Key, g => g.Select(x => x.ToNodeId).ToList());

            var depth = new Dictionary<string, int>();
            var startId = _def.Nodes.FirstOrDefault(n => n.NodeType == "Start")?.Id
                          ?? _def.Nodes.FirstOrDefault()?.Id;
            if (startId != null)
            {
                var q = new Queue<(string id, int d)>();
                depth[startId] = 0; q.Enqueue((startId, 0));
                while (q.Count > 0)
                {
                    var (id, d) = q.Dequeue();
                    if (!next.TryGetValue(id, out var tos)) continue;
                    foreach (var to in tos)
                        if (!depth.ContainsKey(to)) { depth[to] = d + 1; q.Enqueue((to, d + 1)); }
                }
            }

            int orphanCol = (depth.Count > 0 ? depth.Values.Max() : 0) + 1;
            var rowCount = new Dictionary<int, int>();
            foreach (var n in Nodes)
            {
                int col = depth.TryGetValue(n.Id, out var dd) ? dd : orphanCol;
                int row = rowCount.TryGetValue(col, out var r) ? r : 0;
                rowCount[col] = row + 1;
                n.X = 60 + col * 260;
                n.Y = 40 + row * 150;
            }
        }

        // ================= 选择操作 =================
        public void ClearSelection()
        {
            foreach (var n in Nodes) n.IsSelected = false;
            SelectedNode = null;
            NotifySelection();
        }

        public void SelectOnly(NodeViewModel? n)
        {
            foreach (var x in Nodes) x.IsSelected = ReferenceEquals(x, n);
            SelectedNode = n;
            NotifySelection();
        }

        public void ToggleSelect(NodeViewModel n)
        {
            n.IsSelected = !n.IsSelected;
            SelectedNode = n.IsSelected ? n : Nodes.LastOrDefault(x => x.IsSelected);
            NotifySelection();
        }

        public void SelectAll()
        {
            foreach (var n in Nodes) n.IsSelected = true;
            SelectedNode = Nodes.LastOrDefault();
            NotifySelection();
        }

        public void SelectInRect(Rect rect, bool additive)
        {
            if (!additive) foreach (var n in Nodes) n.IsSelected = false;
            NodeViewModel? last = null;
            foreach (var n in Nodes)
            {
                if (new Rect(n.X, n.Y, n.Width, n.Height).IntersectsWith(rect))
                { n.IsSelected = true; last = n; }
            }
            SelectedNode = last ?? Nodes.LastOrDefault(x => x.IsSelected);
            NotifySelection();
        }

        private void NotifySelection()
        {
            OnPropertyChanged(nameof(SelectedCount));
            OnPropertyChanged(nameof(StatusText));
            CommandManager.InvalidateRequerySuggested();
        }

        // ================= 编辑操作（记录撤销） =================
        public NodeViewModel AddNode(Point pos, string nodeType = "Func")
        {
            nodeType = nodeType == "SubFlow" ? "SubFlow" : "Func";
            var prefix = nodeType == "SubFlow" ? "SubFlow" : "Func";
            int i = 1; string id;
            do { id = $"{prefix}_{i++}"; } while (Nodes.Any(n => n.Id == id));
            var node = new NodeViewModel
            {
                Id = id,
                NodeName = nodeType == "SubFlow" ? "NewSubFlow" : "NewFunc",
                NodeType = nodeType,
                X = Math.Max(0, pos.X - 90),
                Y = Math.Max(0, pos.Y - 30)
            };
            InsertNodeCore(node, Nodes.Count);
            Push(new AddNodeAction(NodeSnapshot.Of(node, Nodes.Count - 1)));
            SelectOnly(node);
            return node;
        }

        public void AddConnection(NodeViewModel source, NodeViewModel target)
        {
            if (Connections.Any(c => ReferenceEquals(c.Source, source)
                                  && ReferenceEquals(c.Target, target) && c.Condition == "Yes"))
                return;
            InsertConnectionCore(source.Id, target.Id, "Yes");
            Push(new AddConnectionAction(new ConnSnapshot(source.Id, target.Id, "Yes")));
        }

        public void RemoveNode(NodeViewModel n) => RemoveNodes(new[] { n });

        public void RemoveNodes(IReadOnlyCollection<NodeViewModel> nodes)
        {
            if (nodes.Count == 0) return;
            var nodeSnaps = nodes.Select(n => NodeSnapshot.Of(n, Nodes.IndexOf(n))).ToList();
            var connSnaps = Connections
                .Where(c => nodes.Any(n => ReferenceEquals(c.Source, n) || ReferenceEquals(c.Target, n)))
                .Select(c => new ConnSnapshot(c.Source.Id, c.Target.Id, c.Condition))
                .Distinct()
                .ToList();
            RemoveNodesCore(nodes);
            Push(new DeleteNodesAction(nodeSnaps, connSnaps));
        }

        public void RemoveConnection(ConnectionViewModel c)
        {
            var snap = new ConnSnapshot(c.Source.Id, c.Target.Id, c.Condition);
            RemoveConnectionCore(c);
            Push(new DeleteConnectionAction(snap));
        }

        /// <summary>拖动结束时调用：记录位置变化为一步撤销</summary>
        public void RecordMove(IReadOnlyDictionary<NodeViewModel, Point> starts)
        {
            var moves = new List<NodeMove>();
            foreach (var (n, start) in starts)
                if (Math.Abs(n.X - start.X) > 0.01 || Math.Abs(n.Y - start.Y) > 0.01)
                    moves.Add(new NodeMove(n.Id, start.X, start.Y, n.X, n.Y));
            if (moves.Count > 0) Push(new MoveNodesAction(moves));
        }

        // ================= 内部操作（不记录撤销，供 Undo/Redo 使用） =================
        internal NodeViewModel? FindNode(string id) => Nodes.FirstOrDefault(n => n.Id == id);

        internal void InsertNodeCore(NodeViewModel n, int index)
        {
            if (FindNode(n.Id) != null) return;
            n.PropertyChanged += Node_PropertyChanged;
            Nodes.Insert(Math.Clamp(index, 0, Nodes.Count), n);
            RecalcCanvasSize();
        }

        internal void InsertConnectionCore(string fromId, string toId, string condition)
        {
            var s = FindNode(fromId); var t = FindNode(toId);
            if (s == null || t == null) return;
            if (Connections.Any(c => c.Source.Id == fromId && c.Target.Id == toId && c.Condition == condition))
                return;
            Connections.Add(new ConnectionViewModel(s, t, condition));
        }

        internal void RemoveNodesCore(IEnumerable<NodeViewModel> nodes)
        {
            foreach (var n in nodes.ToList())
            {
                foreach (var c in Connections.Where(c => ReferenceEquals(c.Source, n)
                                                      || ReferenceEquals(c.Target, n)).ToList())
                    RemoveConnectionCore(c);
                n.PropertyChanged -= Node_PropertyChanged;
                if (SelectedNode == n) SelectedNode = null;
                Nodes.Remove(n);
            }
            NotifySelection();
            RecalcCanvasSize();
        }

        internal void RemoveConnectionCore(ConnectionViewModel c)
        {
            c.Detach();
            Connections.Remove(c);
        }

        internal void SetNodePosition(string id, double x, double y)
        {
            var n = FindNode(id);
            if (n == null) return;
            n.X = x; n.Y = y;
        }

        // ================= Undo/Redo =================
        public void Undo()
        {
            if (_undoStack.Count == 0) return;
            var a = _undoStack.Pop();
            a.Undo(this);
            _redoStack.Push(a);
            RaiseUndoRedo();
        }

        public void Redo()
        {
            if (_redoStack.Count == 0) return;
            var a = _redoStack.Pop();
            a.Redo(this);
            _undoStack.Push(a);
            RaiseUndoRedo();
        }

        private void Push(EditAction a)
        {
            _undoStack.Push(a);
            _redoStack.Clear();
            RaiseUndoRedo();
        }

        private void RaiseUndoRedo()
        {
            OnPropertyChanged(nameof(CanUndo)); OnPropertyChanged(nameof(CanRedo));
            OnPropertyChanged(nameof(UndoName)); OnPropertyChanged(nameof(RedoName));
            CommandManager.InvalidateRequerySuggested();
        }

        // ================= 画布尺寸 =================
        private void Node_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName is nameof(NodeViewModel.X) or nameof(NodeViewModel.Y)
                or nameof(NodeViewModel.Width) or nameof(NodeViewModel.Height))
                RecalcCanvasSize();
        }

        private void RecalcCanvasSize()
        {
            double w = 1600, h = 1200;
            foreach (var n in Nodes)
            {
                w = Math.Max(w, n.X + n.Width + 400);
                h = Math.Max(h, n.Y + n.Height + 300);
            }
            CanvasWidth = w;
            CanvasHeight = h;
        }

        public void Flush()
        {
            _def.Nodes = Nodes.Select(n => n.ToDef()).ToList();
            _def.Connections = Connections.Select(c => new ConnectionDef
            {
                FromNodeId = c.Source.Id,
                ToNodeId = c.Target.Id,
                Condition = c.Condition
            }).ToList();
        }
    }

    // ================= 主窗口 =================
    public class MainViewModel : ViewModelBase
    {
        public ObservableCollection<FlowDef> Flows { get; } = new();

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
            set { Set(ref _filePath, value); OnPropertyChanged(nameof(Title)); }
        }
        public string Title => "Flow 编辑器" + (string.IsNullOrEmpty(_filePath) ? "" : $"  -  {_filePath}");

        // ----- 任务 DLL（供 ExeName 下拉选择 / 手写） -----
        public ObservableCollection<string> TaskTypes { get; } = new();
        public ObservableCollection<string> FlowIds { get; } = new();

        private string? _taskDllPath;
        public string? TaskDllPath
        {
            get => _taskDllPath;
            private set { Set(ref _taskDllPath, value); OnPropertyChanged(nameof(TaskDllStatus)); }
        }
        public string TaskDllStatus =>
            string.IsNullOrEmpty(_taskDllPath) ? "未加载任务 DLL" : $"任务 DLL：{Path.GetFileName(_taskDllPath)}";

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
            AddFlowCommand = new RelayCommand(_ => AddFlow());
            RemoveFlowCommand = new RelayCommand(_ => RemoveFlow(), _ => SelectedFlow != null);
            DeleteNodeCommand = new RelayCommand(
                _ => Editor?.RemoveNodes(Editor.SelectedNodes),
                _ => (Editor?.SelectedCount ?? 0) > 0);
            UndoCommand = new RelayCommand(_ => Editor?.Undo(), _ => Editor?.CanUndo == true);
            RedoCommand = new RelayCommand(_ => Editor?.Redo(), _ => Editor?.CanRedo == true);
            SelectAllCommand = new RelayCommand(_ => Editor?.SelectAll(),
                _ => Editor != null && Keyboard.FocusedElement is not TextBoxBase);
            LoadDllCommand = new RelayCommand(_ => LoadDll());
            ValidateCommand = new RelayCommand(_ =>
            {
                Editor?.Flush();
                bool ok = ValidateAll(out var problems);
                MessageBox.Show(
                    ok ? "校验通过，未发现问题。" : "发现以下问题：\n\n" + problems,
                    "校验配置", MessageBoxButton.OK,
                    ok ? MessageBoxImage.Information : MessageBoxImage.Warning);
            }, _ => Flows.Count > 0);
        }

        private void Open()
        {
            var dlg = new OpenFileDialog { Filter = "Flow 配置文件 (*.json)|*.json|所有文件 (*.*)|*.*" };
            if (dlg.ShowDialog() != true) return;
            try
            {
                var file = JsonSerializer.Deserialize<FlowFile>(File.ReadAllText(dlg.FileName)) ?? new FlowFile();
                Editor = null; _selectedFlow = null;
                Flows.Clear();
                foreach (var f in file.Flows) Flows.Add(f);
                RefreshFlowIds();
                FilePath = dlg.FileName;
                SelectedFlow = Flows.FirstOrDefault();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"打开文件失败：{ex.Message}", "错误",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void LoadDll()
        {
            var dlg = new OpenFileDialog
            {
                Title = "加载任务 DLL",
                Filter = "程序集 (*.dll)|*.dll|所有文件 (*.*)|*.*"
            };
            if (dlg.ShowDialog() != true) return;

            try
            {
                using var loader = new TaskDllLoader(dlg.FileName);
                var names = loader.GetITaskTypeNames(dlg.FileName);
                TaskTypes.Clear();
                foreach (var n in names) TaskTypes.Add(n);
                TaskDllPath = dlg.FileName;
                if (names.Count == 0)
                    MessageBox.Show("DLL 加载成功，但未找到实现 ITask 接口的类。", "提示",
                        MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"加载 DLL 失败：{ex.Message}", "错误",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void Save() { if (FilePath == null) SaveAs(); else WriteFile(FilePath); }

        private void SaveAs()
        {
            var dlg = new SaveFileDialog { Filter = "Flow 配置文件 (*.json)|*.json", FileName = "flows.json" };
            if (dlg.ShowDialog() != true) return;
            FilePath = dlg.FileName;
            WriteFile(FilePath);
        }

        private void WriteFile(string path)
        {
            try
            {
                Editor?.Flush();
                if (!ValidateAll(out var problems))
                {
                    var r = MessageBox.Show(
                        "校验发现以下问题：\n\n" + problems + "\n是否仍然保存？",
                        "保存校验", MessageBoxButton.YesNo, MessageBoxImage.Warning);
                    if (r != MessageBoxResult.Yes) return;
                }
                var json = JsonSerializer.Serialize(
                    new FlowFile { Flows = Flows.ToList() },
                    new JsonSerializerOptions
                    {
                        WriteIndented = true,
                        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
                    });
                File.WriteAllText(path, json);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"保存失败：{ex.Message}", "错误",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>校验所有流程：Id 重复/为空、连线悬空、重复连线、缺少 Start/End</summary>
        private bool ValidateAll(out string message)
        {
            var sb = new StringBuilder();

            foreach (var g in Flows.GroupBy(f => f.Id).Where(g => g.Count() > 1))
                sb.AppendLine($"· 流程 Id 重复：\"{g.Key}\" ×{g.Count()}");

            foreach (var f in Flows)
            {
                foreach (var g in f.Nodes.GroupBy(n => n.Id).Where(g => g.Count() > 1))
                    sb.AppendLine($"· [{f.Name}] 节点 Id 重复：\"{g.Key}\" ×{g.Count()}");
                foreach (var n in f.Nodes.Where(n => string.IsNullOrWhiteSpace(n.Id)))
                    sb.AppendLine($"· [{f.Name}] 存在 Id 为空的节点（NodeName={n.NodeName}）");

                var ids = f.Nodes.Select(n => n.Id).ToHashSet();
                foreach (var c in f.Connections)
                {
                    if (!ids.Contains(c.FromNodeId)) sb.AppendLine($"· [{f.Name}] 连线起点不存在：\"{c.FromNodeId}\"");
                    if (!ids.Contains(c.ToNodeId)) sb.AppendLine($"· [{f.Name}] 连线终点不存在：\"{c.ToNodeId}\"");
                }
                foreach (var g in f.Connections
                             .GroupBy(c => (c.FromNodeId, c.ToNodeId, c.Condition))
                             .Where(g => g.Count() > 1))
                    sb.AppendLine($"· [{f.Name}] 重复连线：{g.Key.FromNodeId} → {g.Key.ToNodeId}（条件 \"{g.Key.Condition}\"）×{g.Count()}");

                if (!f.Nodes.Any(n => n.NodeType == "Start")) sb.AppendLine($"· [{f.Name}] 缺少 Start 节点");
                if (!f.Nodes.Any(n => n.NodeType == "End")) sb.AppendLine($"· [{f.Name}] 缺少 End 节点");
            }

            message = sb.ToString();
            return sb.Length == 0;
        }

        private void AddFlow()
        {
            int i = 1; string id;
            do { id = $"Flow_{i++}"; } while (Flows.Any(f => f.Id == id));
            var f = new FlowDef { Id = id, Name = id };
            f.Nodes.Add(new NodeDef { Id = "start", NodeName = "Start", NodeType = "Start", InputParameters = "" });
            f.Nodes.Add(new NodeDef { Id = "end", NodeName = "End", NodeType = "End", InputParameters = "" });
            Flows.Add(f);
            RefreshFlowIds();
            SelectedFlow = f;
        }

        private void RemoveFlow()
        {
            if (SelectedFlow == null) return;
            if (MessageBox.Show($"确定删除流程 {SelectedFlow.Name}？", "确认",
                    MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;
            int idx = Flows.IndexOf(SelectedFlow);
            Flows.Remove(SelectedFlow);
            RefreshFlowIds();
            SelectedFlow = Flows.Count > 0 ? Flows[Math.Min(idx, Flows.Count - 1)] : null;
        }

        private void RefreshFlowIds()
        {
            FlowIds.Clear();
            foreach (var id in Flows.Select(f => f.Id).Where(id => !string.IsNullOrWhiteSpace(id)))
                FlowIds.Add(id);
        }
    }
}
