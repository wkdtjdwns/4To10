using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using FourTen.Models;
using FourTen.Views;

namespace FourTen;

public partial class MainWindow : Window
{
    private readonly ProgressStore _progress = new();

    // 콘텐츠(디자인) 기준 비율 420 : 760
    private const double AspectW = 420.0;
    private const double AspectH = 760.0;

    // 창 전체 크기와 클라이언트 영역 크기의 차이(테두리+제목표시줄), 픽셀 단위
    private int _chromeW;
    private int _chromeH;

    public MainWindow()
    {
        InitializeComponent();
        ShowStageSelect();
        SourceInitialized += OnSourceInitialized;
    }

    public void ShowStageSelect()
    {
        var view = new StageSelectView(_progress);
        view.StageChosen += stage => ShowGame(stage);
        Host.Content = view;
    }

    public void ShowGame(Stage stage)
    {
        var view = new GameView(stage, _progress);
        view.BackRequested += ShowStageSelect;
        view.NextRequested += () =>
        {
            int next = stage.Index + 1;
            if (next <= StageRepository.Stages.Count)
                ShowGame(StageRepository.Stages[next - 1]);
            else
                ShowStageSelect();
        };
        Host.Content = view;
    }

    // ---------- 창 비율 고정 ----------
    private void OnSourceInitialized(object? sender, EventArgs e)
    {
        var hwnd = new WindowInteropHelper(this).Handle;
        var source = HwndSource.FromHwnd(hwnd);
        source?.AddHook(WndProc);

        // 최대화 비활성화(최대화 시 모니터 비율로 채워져 검은 여백이 생기는 것을 방지).
        // 가장자리 드래그 리사이즈는 그대로 유지된다.
        long style = GetWindowLongPtr(hwnd, GWL_STYLE).ToInt64();
        SetWindowLongPtr(hwnd, GWL_STYLE, new IntPtr(style & ~WS_MAXIMIZEBOX));

        // 크롬(비클라이언트) 크기 계산
        GetWindowRect(hwnd, out RECT win);
        GetClientRect(hwnd, out RECT cli);
        _chromeW = (win.Right - win.Left) - (cli.Right - cli.Left);
        _chromeH = (win.Bottom - win.Top) - (cli.Bottom - cli.Top);

        // 시작 시 현재 너비에 맞춰 높이를 비율에 맞게 보정
        NormalizeToWidth(hwnd, win);
    }

    private void NormalizeToWidth(IntPtr hwnd, RECT win)
    {
        int winW = win.Right - win.Left;
        int clientW = winW - _chromeW;
        int clientH = (int)Math.Round(clientW * AspectH / AspectW);
        int winH = clientH + _chromeH;
        SetWindowPos(hwnd, IntPtr.Zero, win.Left, win.Top, winW, winH,
            SWP_NOZORDER | SWP_NOACTIVATE);
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        const int WM_SIZING = 0x0214;
        if (msg != WM_SIZING) return IntPtr.Zero;

        var rect = Marshal.PtrToStructure<RECT>(lParam);
        int edge = wParam.ToInt32();

        int winW = rect.Right - rect.Left;
        int winH = rect.Bottom - rect.Top;
        int clientW = winW - _chromeW;
        int clientH = winH - _chromeH;

        switch (edge)
        {
            case WMSZ_TOP:
            case WMSZ_BOTTOM:
                // 높이 변경 → 너비를 비율에 맞춤
                clientW = (int)Math.Round(clientH * AspectW / AspectH);
                rect.Right = rect.Left + clientW + _chromeW;
                break;
            default:
                // 너비 변경(좌/우/모서리) → 높이를 비율에 맞춤
                clientH = (int)Math.Round(clientW * AspectH / AspectW);
                int newWinH = clientH + _chromeH;
                if (edge is WMSZ_TOPLEFT or WMSZ_TOPRIGHT)
                    rect.Top = rect.Bottom - newWinH;
                else
                    rect.Bottom = rect.Top + newWinH;
                break;
        }

        Marshal.StructureToPtr(rect, lParam, false);
        handled = true;
        return new IntPtr(1);
    }

    private const int WMSZ_LEFT = 1, WMSZ_RIGHT = 2, WMSZ_TOP = 3,
        WMSZ_TOPLEFT = 4, WMSZ_TOPRIGHT = 5, WMSZ_BOTTOM = 6,
        WMSZ_BOTTOMLEFT = 7, WMSZ_BOTTOMRIGHT = 8;

    private const uint SWP_NOZORDER = 0x0004, SWP_NOACTIVATE = 0x0010;

    private const int GWL_STYLE = -16;
    private const long WS_MAXIMIZEBOX = 0x00010000;

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT { public int Left, Top, Right, Bottom; }

    [DllImport("user32.dll")] private static extern bool GetWindowRect(IntPtr hwnd, out RECT r);
    [DllImport("user32.dll")] private static extern bool GetClientRect(IntPtr hwnd, out RECT r);
    [DllImport("user32.dll")] private static extern bool SetWindowPos(
        IntPtr hwnd, IntPtr after, int x, int y, int cx, int cy, uint flags);
    [DllImport("user32.dll")] private static extern IntPtr GetWindowLongPtr(IntPtr hwnd, int index);
    [DllImport("user32.dll")] private static extern IntPtr SetWindowLongPtr(IntPtr hwnd, int index, IntPtr value);
}
