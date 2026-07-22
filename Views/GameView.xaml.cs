using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using FourTen.Scripts;
using FourTen.Models;

namespace FourTen.Views;

public partial class GameView : UserControl
{
    private readonly Stage _stage;
    private readonly ProgressStore _progress;

    private readonly int[] _numbers;          // 현재 타일 순서
    private readonly char?[] _ops = new char?[3];
    private int _openBefore = -1;
    private int _closeAfter = -1;

    private string? _armed;                    // "(", ")", "+", "-", "*", "/"
    private int _selectedTile = -1;
    private bool _solved;

    // 드래그 상태 (팔레트 기호)
    private Point _dragStartPoint;
    private bool _palettePressed;
    private string? _pendingDragTag;
    private string? _dragSymbol;               // 현재 드래그 중인 기호(없으면 null)

    // 드래그 상태 (숫자 타일)
    private Point _tileDragStart;
    private bool _tilePressed;
    private int _pressedTileIndex = -1;
    private int _dragTileIndex = -1;           // 현재 드래그 중인 타일 인덱스(없으면 -1)

    // 드래그 상태 (연산자 칸에서 빼내기/옮기기)
    private Point _gapDragStart;
    private bool _gapPressed;
    private int _pressedGapIndex = -1;
    private int _dragFromGap = -1;             // 연산자를 끌어낸 원래 칸(없으면 -1)
    private int _lastDropGap = -1;             // 방금 드롭된 칸(밖에 버리면 -1)

    // 드래그 상태 (괄호 옮기기/빼내기)
    private Point _parenDragStart;
    private bool _parenPressed;
    private bool _pressedParenOpen;
    private int _dragParen;                    // 0=없음, 1=여는 괄호, 2=닫는 괄호 (드래그 중)
    private bool _parenDroppedOnTile;          // 괄호가 타일에 놓였는지(아니면 밖에 버린 것 → 제거)

    private const string DragFormat = "fourten-symbol";
    private const string TileFormat = "fourten-tile";

    public event Action? BackRequested;
    public event Action? NextRequested;

    private static readonly SolidColorBrush TextBrush = (SolidColorBrush)Application.Current.Resources["TextBrush"];
    private static readonly SolidColorBrush DimBrush = (SolidColorBrush)Application.Current.Resources["DimTextBrush"];
    private static readonly SolidColorBrush AccentBrush = (SolidColorBrush)Application.Current.Resources["AccentBrush"];

    // 드롭 대상 강조용 브러시
    private static readonly Brush DropIdle = Freeze(new SolidColorBrush(Color.FromArgb(0x2E, 0x5A, 0xC8, 0xE0)));
    private static readonly Brush DropHover = Freeze(new SolidColorBrush(Color.FromArgb(0x77, 0x5A, 0xC8, 0xE0)));
    private static readonly Brush TransparentBrush = Brushes.Transparent;

    private static Brush Freeze(SolidColorBrush b) { b.Freeze(); return b; }

    public GameView(Stage stage, ProgressStore progress)
    {
        InitializeComponent();
        _stage = stage;
        _progress = progress;
        _numbers = (int[])stage.Numbers.Clone();

        CounterText.Text = $"{stage.Index} / {StageRepository.Stages.Count}";
        NextButton.Visibility = stage.Index < StageRepository.Stages.Count
            ? Visibility.Visible : Visibility.Collapsed;

        HookPaletteDrag();
        BuildExprBar();
        UpdatePaletteHighlight();
        UpdateResult();
    }

    // ---------- 팔레트: 클릭(장전) + 드래그 시작 ----------
    private Button[] PaletteButtons => new[] { BtnOpen, BtnPlus, BtnMinus, BtnTimes, BtnDiv, BtnClose };

    private void HookPaletteDrag()
    {
        foreach (var btn in PaletteButtons)
        {
            btn.PreviewMouseLeftButtonDown += Palette_PreviewDown;
            btn.PreviewMouseMove += Palette_PreviewMove;
            btn.PreviewMouseLeftButtonUp += Palette_PreviewUp;
        }
    }

    private void Palette_PreviewDown(object sender, MouseButtonEventArgs e)
    {
        _palettePressed = true;
        _dragStartPoint = e.GetPosition(null);
        _pendingDragTag = (string)((Button)sender).Tag;
    }

    private void Palette_PreviewUp(object sender, MouseButtonEventArgs e) => _palettePressed = false;

    private void Palette_PreviewMove(object sender, MouseEventArgs e)
    {
        if (!_palettePressed || e.LeftButton != MouseButtonState.Pressed || _pendingDragTag is null)
            return;

        var pos = e.GetPosition(null);
        if (Math.Abs(pos.X - _dragStartPoint.X) < SystemParameters.MinimumHorizontalDragDistance &&
            Math.Abs(pos.Y - _dragStartPoint.Y) < SystemParameters.MinimumVerticalDragDistance)
            return;

        _palettePressed = false;
        string tag = _pendingDragTag;

        // 드래그 시작: 장전 해제하고 유효 위치 강조
        _armed = null;
        _dragSymbol = tag;
        BuildExprBar();
        UpdatePaletteHighlight();

        var data = new DataObject(DragFormat, tag);
        DragDrop.DoDragDrop((Button)sender, data, DragDropEffects.Move);

        // 드래그 종료(놓았거나 취소): 강조 해제 후 갱신
        _dragSymbol = null;
        Refresh();
    }

    private void Palette_Click(object sender, RoutedEventArgs e)
    {
        // 드래그가 아니라 순수 클릭일 때만 장전
        var tag = (string)((Button)sender).Tag;
        _armed = _armed == tag ? null : tag;
        _selectedTile = -1;
        BuildExprBar();
        UpdatePaletteHighlight();
    }

    private void UpdatePaletteHighlight()
    {
        (Button btn, string tag)[] map =
        {
            (BtnOpen, "("), (BtnPlus, "+"), (BtnMinus, "-"),
            (BtnTimes, "*"), (BtnDiv, "/"), (BtnClose, ")"),
        };
        foreach (var (btn, tag) in map)
        {
            bool on = _armed == tag || _dragSymbol == tag;
            btn.Background = on ? new SolidColorBrush(Color.FromRgb(0x1E, 0x4A, 0x5C)) : Brushes.Transparent;
            btn.Foreground = on ? AccentBrush : DimBrush;
        }
    }

    // ---------- 수식 바 ----------
    private static bool IsOperator(string? s) => s is "+" or "-" or "*" or "/";
    private static bool IsParen(string? s) => s is "(" or ")";

    private void BuildExprBar()
    {
        ExprBar.Children.Clear();
        for (int i = 0; i < _numbers.Length; i++)
        {
            if (_openBefore == i) ExprBar.Children.Add(ParenGlyph("(", isOpen: true));
            ExprBar.Children.Add(TileButton(i));
            if (_closeAfter == i) ExprBar.Children.Add(ParenGlyph(")", isOpen: false));
            if (i < 3) ExprBar.Children.Add(GapButton(i));
        }
    }

    private Button TileButton(int index)
    {
        bool selected = _selectedTile == index;
        bool parenTarget = IsParen(_dragSymbol);            // 괄호 드래그 중 → 타일이 드롭 대상
        bool swapTarget = _dragTileIndex >= 0 && _dragTileIndex != index; // 다른 타일 드래그 중 → 교환 대상
        bool highlight = parenTarget || swapTarget;
        bool isDragSource = _dragTileIndex == index;

        var inner = new Border
        {
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(10, 2, 10, 2),
            BorderThickness = new Thickness(highlight ? 1.4 : 0),
            BorderBrush = highlight ? AccentBrush : TransparentBrush,
            Background = selected
                ? new SolidColorBrush(Color.FromRgb(0x24, 0x40, 0x58))
                : (highlight ? DropIdle : TransparentBrush),
            Opacity = isDragSource ? 0.4 : 1.0,
            Child = new TextBlock
            {
                Text = _numbers[index].ToString(),
                FontSize = 46,
                FontWeight = FontWeights.Thin,
                Foreground = TextBrush,
            },
        };
        var btn = FlatButton(inner);
        btn.Click += (_, _) => OnTileClick(index);

        // 숫자 타일 드래그 시작(순서 교환)
        btn.PreviewMouseLeftButtonDown += (_, e) =>
        {
            _tilePressed = true;
            _tileDragStart = e.GetPosition(null);
            _pressedTileIndex = index;
        };
        btn.PreviewMouseLeftButtonUp += (_, _) => _tilePressed = false;
        btn.PreviewMouseMove += (_, e) => TryStartTileDrag(btn, e);

        // 드롭 대상: 괄호(팔레트) 또는 숫자 교환(다른 타일)
        btn.AllowDrop = true;
        btn.DragOver += (_, e) =>
        {
            e.Effects = CanDropOnTile(e, index) ? DragDropEffects.Move : DragDropEffects.None;
            e.Handled = true;
        };
        btn.DragEnter += (_, e) => { if (CanDropOnTile(e, index)) inner.Background = DropHover; };
        btn.DragLeave += (_, _) => { inner.Background = highlight ? DropIdle : TransparentBrush; };
        btn.Drop += (_, e) =>
        {
            string? sym = e.Data.GetData(DragFormat) as string;
            if (sym == "(") { _openBefore = index; _parenDroppedOnTile = true; }
            else if (sym == ")") { _closeAfter = index; _parenDroppedOnTile = true; }
            else if (e.Data.GetDataPresent(TileFormat))
            {
                int src = (int)e.Data.GetData(TileFormat);
                if (src != index)
                    (_numbers[src], _numbers[index]) = (_numbers[index], _numbers[src]);
            }
            e.Handled = true;
        };
        return btn;
    }

    private static bool CanDropOnTile(DragEventArgs e, int index)
    {
        if (IsParen(e.Data.GetData(DragFormat) as string)) return true;
        if (e.Data.GetDataPresent(TileFormat) && (int)e.Data.GetData(TileFormat) != index) return true;
        return false;
    }

    private void TryStartTileDrag(Button btn, MouseEventArgs e)
    {
        if (!_tilePressed || e.LeftButton != MouseButtonState.Pressed || _pressedTileIndex < 0)
            return;

        var pos = e.GetPosition(null);
        if (Math.Abs(pos.X - _tileDragStart.X) < SystemParameters.MinimumHorizontalDragDistance &&
            Math.Abs(pos.Y - _tileDragStart.Y) < SystemParameters.MinimumVerticalDragDistance)
            return;

        _tilePressed = false;
        int src = _pressedTileIndex;

        _armed = null;
        _selectedTile = -1;
        _dragTileIndex = src;
        BuildExprBar();

        var data = new DataObject(TileFormat, src);
        DragDrop.DoDragDrop(btn, data, DragDropEffects.Move);

        _dragTileIndex = -1;
        Refresh();
    }

    private Button GapButton(int index)
    {
        bool opTarget = IsOperator(_dragSymbol);    // 연산자 드래그 중이면 칸이 드롭 대상
        bool filled = _ops[index].HasValue;
        bool isDragSource = _dragFromGap == index;

        TextBlock label = filled
            ? new TextBlock { Text = Symbol(_ops[index]!.Value), FontSize = 32, FontWeight = FontWeights.Light, Foreground = AccentBrush }
            : new TextBlock { Text = "·", FontSize = 26, Foreground = new SolidColorBrush(Color.FromRgb(0x3A, 0x4A, 0x5C)) };

        var box = new Border
        {
            Width = 40,
            MinHeight = 56,
            CornerRadius = new CornerRadius(8),
            BorderThickness = new Thickness(opTarget ? 1.4 : 0),
            BorderBrush = opTarget ? AccentBrush : TransparentBrush,
            Background = opTarget ? DropIdle : TransparentBrush,
            Opacity = isDragSource ? 0.35 : 1.0,
            Child = new Border
            {
                Child = label,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
            },
        };

        var btn = FlatButton(box);
        btn.Click += (_, _) => OnGapClick(index);

        // 채워진 칸이면 드래그로 빼내기/옮기기 가능
        if (filled)
        {
            btn.PreviewMouseLeftButtonDown += (_, e) =>
            {
                _gapPressed = true;
                _gapDragStart = e.GetPosition(null);
                _pressedGapIndex = index;
            };
            btn.PreviewMouseLeftButtonUp += (_, _) => _gapPressed = false;
            btn.PreviewMouseMove += (_, e) => TryStartGapDrag(btn, index, e);
        }

        btn.AllowDrop = true;
        btn.DragOver += (_, e) =>
        {
            string? sym = e.Data.GetData(DragFormat) as string;
            e.Effects = IsOperator(sym) ? DragDropEffects.Move : DragDropEffects.None;
            e.Handled = true;
        };
        btn.DragEnter += (_, e) => { if (IsOperator(e.Data.GetData(DragFormat) as string)) box.Background = DropHover; };
        btn.DragLeave += (_, _) => { if (opTarget) box.Background = DropIdle; };
        btn.Drop += (_, e) =>
        {
            string? sym = e.Data.GetData(DragFormat) as string;
            if (IsOperator(sym)) _ops[index] = sym![0];
            _lastDropGap = index;   // 이 칸에 떨어졌음을 기록(제거/이동 판단용)
            e.Handled = true;
        };
        return btn;
    }

    private void TryStartGapDrag(Button btn, int index, MouseEventArgs e)
    {
        if (!_gapPressed || e.LeftButton != MouseButtonState.Pressed || _pressedGapIndex < 0)
            return;

        var pos = e.GetPosition(null);
        if (Math.Abs(pos.X - _gapDragStart.X) < SystemParameters.MinimumHorizontalDragDistance &&
            Math.Abs(pos.Y - _gapDragStart.Y) < SystemParameters.MinimumVerticalDragDistance)
            return;

        _gapPressed = false;
        if (!_ops[index].HasValue) return;
        char op = _ops[index]!.Value;

        _armed = null;
        _selectedTile = -1;
        _dragFromGap = index;
        _lastDropGap = -1;
        _dragSymbol = op.ToString();
        BuildExprBar();

        var data = new DataObject(DragFormat, op.ToString());
        DragDrop.DoDragDrop(btn, data, DragDropEffects.Move);

        // 같은 칸이 아니면(다른 칸으로 이동했거나 밖에 버렸으면) 원래 칸을 비운다
        if (_lastDropGap != index)
            _ops[index] = null;

        _dragSymbol = null;
        _dragFromGap = -1;
        _lastDropGap = -1;
        Refresh();
    }

    private Button ParenGlyph(string text, bool isOpen)
    {
        bool isDragSource = (_dragParen == 1 && isOpen) || (_dragParen == 2 && !isOpen);
        var btn = FlatButton(new Border
        {
            Padding = new Thickness(2, 0, 2, 0),
            Opacity = isDragSource ? 0.35 : 1.0,
            Child = new TextBlock
            {
                Text = text,
                FontSize = 40,
                FontWeight = FontWeights.Thin,
                Foreground = AccentBrush,
            },
        });
        // 클릭으로 제거
        btn.Click += (_, _) =>
        {
            if (isOpen) _openBefore = -1; else _closeAfter = -1;
            Refresh();
        };
        // 드래그로 옮기기/빼내기
        btn.PreviewMouseLeftButtonDown += (_, e) =>
        {
            _parenPressed = true;
            _parenDragStart = e.GetPosition(null);
            _pressedParenOpen = isOpen;
        };
        btn.PreviewMouseLeftButtonUp += (_, _) => _parenPressed = false;
        btn.PreviewMouseMove += (_, e) => TryStartParenDrag(btn, isOpen, e);
        return btn;
    }

    private void TryStartParenDrag(Button btn, bool isOpen, MouseEventArgs e)
    {
        if (!_parenPressed || e.LeftButton != MouseButtonState.Pressed)
            return;

        var pos = e.GetPosition(null);
        if (Math.Abs(pos.X - _parenDragStart.X) < SystemParameters.MinimumHorizontalDragDistance &&
            Math.Abs(pos.Y - _parenDragStart.Y) < SystemParameters.MinimumVerticalDragDistance)
            return;

        _parenPressed = false;
        string sym = isOpen ? "(" : ")";

        _armed = null;
        _selectedTile = -1;
        _dragParen = isOpen ? 1 : 2;
        _parenDroppedOnTile = false;
        _dragSymbol = sym;
        BuildExprBar();

        var data = new DataObject(DragFormat, sym);
        DragDrop.DoDragDrop(btn, data, DragDropEffects.Move);

        // 타일에 놓이지 않았으면(밖에 버림) 제거
        if (!_parenDroppedOnTile)
        {
            if (isOpen) _openBefore = -1; else _closeAfter = -1;
        }

        _dragSymbol = null;
        _dragParen = 0;
        _parenDroppedOnTile = false;
        Refresh();
    }

    // ---------- 클릭 처리 ----------
    private void OnTileClick(int i)
    {
        switch (_armed)
        {
            case "(":
                _openBefore = _openBefore == i ? -1 : i;
                _armed = null;
                break;
            case ")":
                _closeAfter = _closeAfter == i ? -1 : i;
                _armed = null;
                break;
            case null:
                // 숫자 순서 바꾸기: 탭 → 탭으로 두 타일 교환
                if (_selectedTile == -1)
                {
                    _selectedTile = i;
                }
                else
                {
                    if (_selectedTile != i)
                        (_numbers[_selectedTile], _numbers[i]) = (_numbers[i], _numbers[_selectedTile]);
                    _selectedTile = -1;
                }
                break;
            default:
                break; // 연산자 장전 상태에서 타일 클릭 → 무시
        }
        Refresh();
    }

    private void OnGapClick(int i)
    {
        if (IsOperator(_armed))
        {
            _ops[i] = _armed![0];
            _armed = null;
        }
        else if (_armed is null)
        {
            _ops[i] = null; // 빈 곳은 그대로, 채워진 곳은 삭제
        }
        Refresh();
    }

    private void Refresh()
    {
        BuildExprBar();
        UpdatePaletteHighlight();
        UpdateResult();
    }

    // ---------- 계산/결과 ----------
    private void UpdateResult()
    {
        bool complete = _ops[0].HasValue && _ops[1].HasValue && _ops[2].HasValue;
        if (!complete)
        {
            ResultText.Text = "?";
            ResultText.Foreground = TextBrush;
            return;
        }

        var tokens = Solver.BuildTokens(_numbers, _ops, _openBefore, _closeAfter);
        var value = ExpressionEvaluator.Evaluate(tokens);
        if (value is null)
        {
            ResultText.Text = "?";
            ResultText.Foreground = TextBrush;
            return;
        }

        ResultText.Text = NumberFormat.Format(value.Value);

        if (NumberFormat.IsTen(value.Value))
        {
            ResultText.Foreground = (SolidColorBrush)Application.Current.Resources["SuccessBrush"];
            OnSolved(tokens);
        }
        else
        {
            ResultText.Foreground = TextBrush;
        }
    }

    private void OnSolved(List<Token> tokens)
    {
        if (_solved) return;
        _solved = true;
        _progress.MarkCleared(_stage.Index);
        SuccessExpr.Text = ExpressionString(tokens) + " = 10";
        SuccessPanel.Visibility = Visibility.Visible;
    }

    private static string ExpressionString(List<Token> tokens)
    {
        var sb = new StringBuilder();
        foreach (var t in tokens)
        {
            sb.Append(t.Kind switch
            {
                TokenKind.Number => ((int)t.Number).ToString(),
                TokenKind.Operator => Symbol(t.Op),
                TokenKind.OpenParen => "(",
                TokenKind.CloseParen => ")",
                _ => ""
            });
            sb.Append(' ');
        }
        return sb.ToString().Trim();
    }

    // ---------- 하단/오버레이 ----------
    private void Reset_Click(object sender, RoutedEventArgs e)
    {
        Array.Copy(_stage.Numbers, _numbers, _numbers.Length);
        _ops[0] = _ops[1] = _ops[2] = null;
        _openBefore = _closeAfter = -1;
        _armed = null;
        _dragSymbol = null;
        _dragTileIndex = -1;
        _dragFromGap = -1;
        _lastDropGap = -1;
        _dragParen = 0;
        _parenDroppedOnTile = false;
        _selectedTile = -1;
        _solved = false;
        SuccessPanel.Visibility = Visibility.Collapsed;
        Refresh();
    }

    private void Menu_Click(object sender, RoutedEventArgs e) => BackRequested?.Invoke();

    private void Next_Click(object sender, RoutedEventArgs e) => NextRequested?.Invoke();

    private void Hint_Click(object sender, RoutedEventArgs e)
    {
        HintContent.Text = "위 버튼을 눌러 보세요";
        HintPanel.Visibility = Visibility.Visible;
    }

    private void HintNumbers_Click(object sender, RoutedEventArgs e)
        => HintContent.Text = _stage.Solution.NumberHint();

    private void HintOps_Click(object sender, RoutedEventArgs e)
        => HintContent.Text = _stage.Solution.OperatorHint();

    private void HintClose_Click(object sender, RoutedEventArgs e)
        => HintPanel.Visibility = Visibility.Collapsed;

    // ---------- 헬퍼 ----------
    private static string Symbol(char op) => op switch
    {
        '+' => "+", '-' => "−", '*' => "×", '/' => "÷", _ => op.ToString()
    };

    private static Button FlatButton(UIElement content)
    {
        return new Button
        {
            Content = content,
            Cursor = Cursors.Hand,
            Background = Brushes.Transparent,
            BorderThickness = new Thickness(0),
            Padding = new Thickness(0),
            Template = FlatTemplate,
        };
    }

    private static readonly ControlTemplate FlatTemplate = CreateFlatTemplate();

    private static ControlTemplate CreateFlatTemplate()
    {
        var template = new ControlTemplate(typeof(Button));
        var border = new FrameworkElementFactory(typeof(Border));
        border.SetValue(Border.BackgroundProperty, Brushes.Transparent);
        var presenter = new FrameworkElementFactory(typeof(ContentPresenter));
        presenter.SetValue(ContentPresenter.HorizontalAlignmentProperty, HorizontalAlignment.Center);
        presenter.SetValue(ContentPresenter.VerticalAlignmentProperty, VerticalAlignment.Center);
        border.AppendChild(presenter);
        template.VisualTree = border;
        return template;
    }
}
