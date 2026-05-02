using Avalonia.Controls;

namespace ZapretUI.Views;

public partial class MainWindow : Window
{
    private bool _isClosingForReal = false;

    public MainWindow()
    {
        InitializeComponent();
        Closing += MainWindow_Closing;
    }

    private void MainWindow_Closing(object? sender, WindowClosingEventArgs e)
    {
        // Если мы не выходим через меню трея, то просто скрываем окно
        if (!_isClosingForReal)
        {
            e.Cancel = true; // Отменяем закрытие
            this.Hide();     // Скрываем окно
        }
    }
}