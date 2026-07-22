using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using FourTen.Models;

namespace FourTen.Views;

public partial class StageSelectView : UserControl
{
    private readonly ProgressStore _progress;

    public event Action<Stage>? StageChosen;

    public StageSelectView(ProgressStore progress)
    {
        InitializeComponent();
        _progress = progress;
        BuildGrid();
    }

    private void BuildGrid()
    {
        StagePanel.Children.Clear();
        int firstUncleared = _progress.FirstUnclearedStage();
        bool allCleared = _progress.Cleared.Count >= StageRepository.Stages.Count;

        foreach (var stage in StageRepository.Stages)
        {
            bool cleared = _progress.IsCleared(stage.Index);
            // 열림: 이미 깼거나 / 바로 다음 도전할 스테이지 / 전부 깬 경우
            bool unlocked = cleared || allCleared || stage.Index == firstUncleared;
            bool isCurrent = !cleared && stage.Index == firstUncleared;
            StagePanel.Children.Add(CreateCell(stage, cleared, unlocked, isCurrent));
        }
    }

    private Button CreateCell(Stage stage, bool cleared, bool unlocked, bool isCurrent)
    {
        // 색상 결정
        Color bg, borderColor, textColor;
        if (cleared)
        {
            bg = Color.FromRgb(0x1C, 0x3A, 0x2C);
            borderColor = ((SolidColorBrush)Application.Current.Resources["SuccessBrush"]).Color;
            textColor = Color.FromRgb(0xDC, 0xE6, 0xF0);
        }
        else if (unlocked)
        {
            bg = Color.FromRgb(0x14, 0x22, 0x33);
            borderColor = isCurrent ? Color.FromRgb(0x5A, 0xC8, 0xE0) : Color.FromRgb(0x2A, 0x44, 0x60);
            textColor = Color.FromRgb(0xDC, 0xE6, 0xF0);
        }
        else // 잠김
        {
            bg = Color.FromRgb(0x0E, 0x16, 0x20);
            borderColor = Color.FromRgb(0x1B, 0x28, 0x36);
            textColor = Color.FromRgb(0x39, 0x48, 0x59);
        }

        var stack = new StackPanel
        {
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
        };
        stack.Children.Add(new TextBlock
        {
            Text = unlocked ? stage.Index.ToString() : "🔒",
            FontSize = unlocked ? 22 : 15,
            FontWeight = FontWeights.Light,
            Foreground = new SolidColorBrush(textColor),
            HorizontalAlignment = HorizontalAlignment.Center,
        });
        if (cleared)
        {
            stack.Children.Add(new TextBlock
            {
                Text = "✓",
                FontSize = 12,
                Foreground = (SolidColorBrush)Application.Current.Resources["SuccessBrush"],
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, -3, 0, 0),
            });
        }

        var border = new Border
        {
            Width = 58,
            Height = 58,
            CornerRadius = new CornerRadius(14),
            Background = new SolidColorBrush(bg),
            BorderThickness = new Thickness(isCurrent ? 2 : 1.4),
            BorderBrush = new SolidColorBrush(borderColor),
            Child = stack,
        };

        var button = new Button
        {
            Width = 66,
            Height = 66,
            Margin = new Thickness(6),
            Cursor = unlocked ? System.Windows.Input.Cursors.Hand : System.Windows.Input.Cursors.Arrow,
            Background = Brushes.Transparent,
            BorderThickness = new Thickness(0),
            Content = border,
            IsEnabled = unlocked,
        };
        button.Template = BuildTransparentTemplate();
        if (unlocked)
            button.Click += (_, _) => StageChosen?.Invoke(stage);
        return button;
    }

    private static ControlTemplate BuildTransparentTemplate()
    {
        var template = new ControlTemplate(typeof(Button));
        var presenter = new FrameworkElementFactory(typeof(ContentPresenter));
        presenter.SetValue(ContentPresenter.HorizontalAlignmentProperty, HorizontalAlignment.Center);
        presenter.SetValue(ContentPresenter.VerticalAlignmentProperty, VerticalAlignment.Center);
        template.VisualTree = presenter;
        return template;
    }

    private void Continue_Click(object sender, RoutedEventArgs e)
    {
        int index = _progress.FirstUnclearedStage();
        var stage = StageRepository.Stages[index - 1];
        StageChosen?.Invoke(stage);
    }
}
