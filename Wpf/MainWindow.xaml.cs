using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Animation;
using MaterialDesignThemes.Wpf;
using Wpf.Common;
using Wpf.ViewModels;

namespace Wpf
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private const double ExpandedWidth = 240;
        private const double CollapsedWidth = 72;

        /// <summary>Ниже этой ширины окна меню сворачивается автоматически.</summary>
        private const double AutoCollapseWidth = 1100;

        private bool? _isNarrow;

        public static readonly DependencyProperty IsSidebarCollapsedProperty =
            DependencyProperty.Register(
                nameof(IsSidebarCollapsed),
                typeof(bool),
                typeof(MainWindow),
                new PropertyMetadata(false));

        /// <summary>Состояние меню. XAML привязывается к нему, чтобы прятать подписи.</summary>
        public bool IsSidebarCollapsed
        {
            get => (bool)GetValue(IsSidebarCollapsedProperty);
            private set => SetValue(IsSidebarCollapsedProperty, value);
        }

        public MainWindow(MainViewModel vm)
        {
            InitializeComponent();

            DataContext = vm;

            SizeChanged += OnSizeChanged;
        }

        private void ToggleSidebar(object sender, RoutedEventArgs e)
        {
            SetSidebarCollapsed(!IsSidebarCollapsed);
        }

        /// <summary>
        /// Меню сворачивается/разворачивается само при пересечении контрольной ширины.
        /// Внутри одного диапазона ручной выбор пользователя не сбрасывается.
        /// </summary>
        private void OnSizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (!e.WidthChanged) return;

            var isNarrow = e.NewSize.Width < AutoCollapseWidth;

            if (_isNarrow == isNarrow) return;

            _isNarrow = isNarrow;

            SetSidebarCollapsed(isNarrow);
        }

        private void SetSidebarCollapsed(bool collapsed)
        {
            if (IsSidebarCollapsed == collapsed) return;

            IsSidebarCollapsed = collapsed;
            ToggleIcon.Kind = collapsed ? PackIconKind.Menu : PackIconKind.Backburger;

            var animation = new GridLengthAnimation
            {
                From = new GridLength(SidebarColumn.ActualWidth),
                To = new GridLength(collapsed ? CollapsedWidth : ExpandedWidth),
                Duration = new Duration(TimeSpan.FromMilliseconds(220)),
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseInOut }
            };

            SidebarColumn.BeginAnimation(ColumnDefinition.WidthProperty, animation);
        }
    }
}
