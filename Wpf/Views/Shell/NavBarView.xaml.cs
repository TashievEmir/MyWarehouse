using System.Windows.Controls;

namespace Wpf.Views.Shell;

/// <summary>
/// Верхнее меню. Данные берёт из <see cref="ViewModels.MainViewModel"/>:
/// контекст наследуется от окна-хозяина, поэтому своего кода здесь нет.
/// </summary>
public partial class NavBarView : UserControl
{
    public NavBarView()
    {
        InitializeComponent();
    }
}
