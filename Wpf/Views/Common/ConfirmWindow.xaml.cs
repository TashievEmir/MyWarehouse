using System.Windows;
using System.Windows.Media;

namespace Wpf.Views.Common
{
    /// <summary>
    /// Окно подтверждения в оформлении приложения.
    ///
    /// Системный MessageBox сюда не годится: он не знает о теме и берёт
    /// надписи кнопок из языка Windows — у пользователя с английской системой
    /// посреди русского интерфейса появлялись Yes и No.
    /// </summary>
    public partial class ConfirmWindow : Window
    {
        public static readonly DependencyProperty TitleTextProperty =
            DependencyProperty.Register(nameof(TitleText), typeof(string), typeof(ConfirmWindow));

        public static readonly DependencyProperty MessageProperty =
            DependencyProperty.Register(nameof(Message), typeof(string), typeof(ConfirmWindow));

        public static readonly DependencyProperty ConfirmTextProperty =
            DependencyProperty.Register(nameof(ConfirmText), typeof(string), typeof(ConfirmWindow));

        public static readonly DependencyProperty CancelTextProperty =
            DependencyProperty.Register(nameof(CancelText), typeof(string), typeof(ConfirmWindow));

        public static readonly DependencyProperty IconProperty =
            DependencyProperty.Register(nameof(Icon), typeof(object), typeof(ConfirmWindow));

        public static readonly DependencyProperty AccentBrushProperty =
            DependencyProperty.Register(nameof(AccentBrush), typeof(Brush), typeof(ConfirmWindow));

        public static readonly DependencyProperty AccentSoftBrushProperty =
            DependencyProperty.Register(nameof(AccentSoftBrush), typeof(Brush), typeof(ConfirmWindow));

        /// <summary>Заголовок вопроса. Своё имя: Window.Title уже занят системой.</summary>
        public string TitleText
        {
            get => (string)GetValue(TitleTextProperty);
            set => SetValue(TitleTextProperty, value);
        }

        public string Message
        {
            get => (string)GetValue(MessageProperty);
            set => SetValue(MessageProperty, value);
        }

        public string ConfirmText
        {
            get => (string)GetValue(ConfirmTextProperty);
            set => SetValue(ConfirmTextProperty, value);
        }

        public string CancelText
        {
            get => (string)GetValue(CancelTextProperty);
            set => SetValue(CancelTextProperty, value);
        }

        public object Icon
        {
            get => GetValue(IconProperty);
            set => SetValue(IconProperty, value);
        }

        public Brush AccentBrush
        {
            get => (Brush)GetValue(AccentBrushProperty);
            set => SetValue(AccentBrushProperty, value);
        }

        public Brush AccentSoftBrush
        {
            get => (Brush)GetValue(AccentSoftBrushProperty);
            set => SetValue(AccentSoftBrushProperty, value);
        }

        public ConfirmWindow()
        {
            InitializeComponent();
        }

        /// <summary>
        /// Растягиваем окно по хозяину, чтобы затемнение накрыло именно его,
        /// а не весь экран. Если хозяина нет — показываем по центру экрана.
        /// </summary>
        public void CoverOwner()
        {
            if (Owner is null || Owner.WindowState == WindowState.Minimized)
            {
                WindowStartupLocation = WindowStartupLocation.CenterScreen;

                Width = 560;
                Height = 320;

                return;
            }

            WindowStartupLocation = WindowStartupLocation.Manual;

            if (Owner.WindowState == WindowState.Maximized)
            {
                // У развёрнутого окна Left и Top не отражают реального положения
                var screen = SystemParameters.WorkArea;

                Left = screen.Left;
                Top = screen.Top;
                Width = screen.Width;
                Height = screen.Height;

                return;
            }

            Left = Owner.Left;
            Top = Owner.Top;
            Width = Owner.ActualWidth;
            Height = Owner.ActualHeight;
        }

        private void OnConfirm(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
        }

        private void OnCancel(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }
    }
}
