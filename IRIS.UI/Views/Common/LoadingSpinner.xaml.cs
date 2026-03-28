using System.Windows;
using System.Windows.Controls;

namespace IRIS.UI.Views.Common
{
    public partial class LoadingSpinner : UserControl
    {
        public static readonly DependencyProperty IsActiveProperty =
            DependencyProperty.Register(nameof(IsActive), typeof(bool), typeof(LoadingSpinner),
                new PropertyMetadata(false, OnIsActiveChanged));

        public static readonly DependencyProperty TextProperty =
            DependencyProperty.Register(nameof(Text), typeof(string), typeof(LoadingSpinner),
                new PropertyMetadata("Loading...", OnTextChanged));

        public bool IsActive
        {
            get => (bool)GetValue(IsActiveProperty);
            set => SetValue(IsActiveProperty, value);
        }

        public string Text
        {
            get => (string)GetValue(TextProperty);
            set => SetValue(TextProperty, value);
        }

        public LoadingSpinner()
        {
            InitializeComponent();
            UpdateVisibility();
        }

        private static void OnIsActiveChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is LoadingSpinner spinner)
                spinner.UpdateVisibility();
        }

        private static void OnTextChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is LoadingSpinner spinner)
                spinner.LoadingTextBlock.Text = (string)e.NewValue;
        }

        private void UpdateVisibility()
        {
            OverlayBorder.Visibility = IsActive ? Visibility.Visible : Visibility.Collapsed;
        }
    }
}
