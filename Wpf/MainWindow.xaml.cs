using System.Windows;
using Wpf.ViewModels;

namespace Wpf
{
    /// <summary>
    /// Оболочка приложения: верхнее меню, заголовок страницы и её содержимое.
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow(MainViewModel vm)
        {
            InitializeComponent();

            // Формат дат в DatePicker берётся из Language окна
            Language = Localization.Loc.Instance.XmlLanguage;

            DataContext = vm;
        }
    }
}
