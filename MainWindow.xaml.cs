using FlowEditor.ViewModels;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;

namespace FlowEditor
{
    public partial class MainWindow : Window
    {
        private readonly MainViewModel _vm = new();

        private enum DragMode { None, Move, Connect, RubberBand }
        private DragMode _dragMode = DragMode.None;
        private NodeViewModel? _connSource;
        private Point _dragMouseStart;
        private Dictionary<NodeViewModel, Point> _dragStarts = new();
        private Point _rubberStart;
        private bool _rubberAdditive;

        public MainWindow()
        {
            InitializeComponent();
            DataContext = _vm;
        }

        private static T? FindAncestor<T>(DependencyObject? d) where T : DependencyObject
        {
            while (d != null)
            {
                if (d is T t) return t;
                d = VisualTreeHelper.GetParent(d);
            }
            return null;
        }

        /// <summary>判断事件源是否位于某个节点内部（文本框点击冒泡时用于排除）</summary>
        private static bool IsInsideNode(DependencyObject? d)
        {
            while (d != null)
            {
                if (d is FrameworkElement fe && fe.DataContext is NodeViewModel) return true;
                d = VisualTreeHelper.GetParent(d);
            }
            return false;
        }

        private static Rect NormalizeRect(Point a, Point b) =>
            new(Math.Min(a.X, b.X), Math.Min(a.Y, b.Y),
                Math.Abs(a.X - b.X), Math.Abs(a.Y - b.Y));

        // ===== 节点上按下：选择（支持 Ctrl 多选）/ 开始整体移动 / 开始连线 =====
        private void Node_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (sender is not FrameworkElement fe || fe.DataContext is not NodeViewModel node
                || _vm.Editor == null) return;

            var src = e.OriginalSource as DependencyObject;
            if (FindAncestor<TextBoxBase>(src) != null || FindAncestor<ComboBox>(src) != null
                || FindAncestor<ButtonBase>(src) != null)
                return; // 在编辑文本，不进入拖拽

            bool ctrl = (Keyboard.Modifiers & ModifierKeys.Control) != 0;
            if (ctrl)
            {
                _vm.Editor.ToggleSelect(node);
                if (!node.IsSelected) { e.Handled = true; return; } // Ctrl 取消选中后不拖拽
            }
            else if (!node.IsSelected)
            {
                _vm.Editor.SelectOnly(node);
            }
            // 节点已在选中集内：保持现有多选，开始整体拖动

            if (FindAncestor<System.Windows.Shapes.Ellipse>(src)?.Name == "OutPort")
            {
                _dragMode = DragMode.Connect;
                _connSource = node;
                var p0 = node.OutPortPoint;
                TempLine.X1 = p0.X; TempLine.Y1 = p0.Y;
                TempLine.X2 = p0.X; TempLine.Y2 = p0.Y;
                TempLine.Visibility = Visibility.Visible;
            }
            else
            {
                _dragMode = DragMode.Move;
                _dragMouseStart = e.GetPosition(EditorArea);
                // 记录所有选中节点的起始位置，用于整体拖动 + 撤销
                _dragStarts = _vm.Editor.SelectedNodes
                    .ToDictionary(n => n, n => new Point(n.X, n.Y));
            }
            EditorArea.CaptureMouse();
            e.Handled = true;
        }

        // ===== 空白处按下：开始框选 =====
        private void EditorArea_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (_vm.Editor == null) return;
            if (IsInsideNode(e.OriginalSource as DependencyObject)) return; // 节点内部冒泡上来的忽略

            _rubberStart = e.GetPosition(EditorArea);
            _rubberAdditive = (Keyboard.Modifiers & ModifierKeys.Control) != 0;
            if (!_rubberAdditive) _vm.Editor.ClearSelection();

            _dragMode = DragMode.RubberBand;
            Canvas.SetLeft(SelectionRect, _rubberStart.X);
            Canvas.SetTop(SelectionRect, _rubberStart.Y);
            SelectionRect.Width = 0;
            SelectionRect.Height = 0;
            SelectionRect.Visibility = Visibility.Visible;

            EditorArea.CaptureMouse();
            e.Handled = true;
        }

        private void EditorArea_MouseMove(object sender, MouseEventArgs e)
        {
            var pos = e.GetPosition(EditorArea);
            switch (_dragMode)
            {
                case DragMode.Move:
                    double dx = pos.X - _dragMouseStart.X;
                    double dy = pos.Y - _dragMouseStart.Y;
                    foreach (var (n, start) in _dragStarts)
                    {
                        n.X = Math.Max(0, start.X + dx);
                        n.Y = Math.Max(0, start.Y + dy);
                    }
                    break;
                case DragMode.Connect:
                    TempLine.X2 = pos.X;
                    TempLine.Y2 = pos.Y;
                    break;
                case DragMode.RubberBand:
                    var r = NormalizeRect(_rubberStart, pos);
                    Canvas.SetLeft(SelectionRect, r.X);
                    Canvas.SetTop(SelectionRect, r.Y);
                    SelectionRect.Width = r.Width;
                    SelectionRect.Height = r.Height;
                    break;
            }
        }

        private void EditorArea_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            switch (_dragMode)
            {
                case DragMode.Move:
                    if (_vm.Editor != null && _dragStarts.Count > 0)
                        _vm.Editor.RecordMove(_dragStarts); // 合并为一步撤销
                    break;

                case DragMode.Connect when _connSource != null && _vm.Editor != null:
                    var pos = e.GetPosition(EditorArea);
                    var target = _vm.Editor.Nodes.FirstOrDefault(n =>
                        !ReferenceEquals(n, _connSource) &&
                        pos.X >= n.X - 8 && pos.X <= n.X + n.Width + 8 &&
                        pos.Y >= n.Y - 12 && pos.Y <= n.Y + n.Height + 12);
                    if (target != null)
                        _vm.Editor.AddConnection(_connSource, target);
                    break;

                case DragMode.RubberBand when _vm.Editor != null:
                    var sel = NormalizeRect(_rubberStart, e.GetPosition(EditorArea));
                    if (sel.Width >= 4 || sel.Height >= 4) // 小于 4px 视为单击（即取消选择）
                        _vm.Editor.SelectInRect(sel, _rubberAdditive);
                    break;
            }

            _dragMode = DragMode.None;
            _dragStarts.Clear();
            _connSource = null;
            TempLine.Visibility = Visibility.Collapsed;
            SelectionRect.Visibility = Visibility.Collapsed;
            if (EditorArea.IsMouseCaptured) EditorArea.ReleaseMouseCapture();
        }

        // ===== Ctrl + 滚轮：以鼠标位置为中心缩放 =====
        private void CanvasScroll_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            if ((Keyboard.Modifiers & ModifierKeys.Control) == 0 || _vm.Editor == null) return;

            double oldZoom = _vm.Editor.Zoom;
            double newZoom = Math.Clamp(oldZoom * (e.Delta > 0 ? 1.15 : 1 / 1.15),
                FlowEditorViewModel.MinZoom, FlowEditorViewModel.MaxZoom);
            if (Math.Abs(newZoom - oldZoom) < 0.001) { e.Handled = true; return; }

            var contentPos = e.GetPosition(EditorArea);   // 内容坐标（不随缩放变化）
            var viewPos = e.GetPosition(CanvasScroll);    // 视口坐标
            _vm.Editor.Zoom = newZoom;
            CanvasScroll.UpdateLayout();                  // 强制完成新布局再校正滚动条
            CanvasScroll.ScrollToHorizontalOffset(contentPos.X * newZoom - viewPos.X);
            CanvasScroll.ScrollToVerticalOffset(contentPos.Y * newZoom - viewPos.Y);
            e.Handled = true;
        }

        private void ZoomIn_Click(object sender, RoutedEventArgs e)
        { if (_vm.Editor != null) _vm.Editor.Zoom *= 1.2; }

        private void ZoomOut_Click(object sender, RoutedEventArgs e)
        { if (_vm.Editor != null) _vm.Editor.Zoom /= 1.2; }

        private void ZoomReset_Click(object sender, RoutedEventArgs e)
        { if (_vm.Editor != null) _vm.Editor.Zoom = 1.0; }

        // ===== 其余（与上一版相同） =====
        private void Node_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (sender is FrameworkElement fe && fe.DataContext is NodeViewModel n)
            {
                n.Width = fe.ActualWidth;
                n.Height = fe.ActualHeight;
            }
        }

        private void AddNode_Click(object sender, RoutedEventArgs e)
        {
            if (_vm.Editor == null)
            {
                MessageBox.Show("请先在左侧打开或新建一个 Flow。", "提示");
                return;
            }
            _vm.Editor.AddNode(Mouse.GetPosition(EditorArea));
        }

        private void DeleteNodeMenu_Click(object sender, RoutedEventArgs e)
        {
            if ((sender as FrameworkElement)?.DataContext is NodeViewModel n)
                _vm.Editor?.RemoveNode(n);
        }

        private void DeleteConnection_Click(object sender, RoutedEventArgs e)
        {
            if ((sender as FrameworkElement)?.DataContext is ConnectionViewModel c)
                _vm.Editor?.RemoveConnection(c);
        }
    }
}
