using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;

namespace IRIS.UI.Views
{
    public partial class MonitorView : UserControl
    {
        public MonitorView()
        {
            InitializeComponent();
            LoadStaticData();
        }

        private void MenuButton_Click(object sender, RoutedEventArgs e)
        {
            var button = sender as Button;
            var border = FindParent<Border>(button);
            var popup = FindChild<Popup>(border, "ContextMenuPopup");
            if (popup != null)
            {
                popup.IsOpen = true;
            }
        }

        private void ViewScreen_Click(object sender, RoutedEventArgs e)
        {
            var button = sender as Button;
            var border = FindParent<Border>(button);
            var popup = FindChild<Popup>(border, "ContextMenuPopup");
            if (popup != null)
            {
                popup.IsOpen = false;
            }
            
            // TODO: Implement view screen functionality
            MessageBox.Show("View Screen functionality will be implemented later.", "Info", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void LockScreen_Click(object sender, RoutedEventArgs e)
        {
            var button = sender as Button;
            var border = FindParent<Border>(button);
            var popup = FindChild<Popup>(border, "ContextMenuPopup");
            if (popup != null)
            {
                popup.IsOpen = false;
            }
            
            // TODO: Implement lock screen functionality
            MessageBox.Show("Lock Screen functionality will be implemented later.", "Info", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private T FindParent<T>(DependencyObject child) where T : DependencyObject
        {
            DependencyObject parentObject = VisualTreeHelper.GetParent(child);
            if (parentObject == null) return null;
            T parent = parentObject as T;
            return parent ?? FindParent<T>(parentObject);
        }

        private T FindChild<T>(DependencyObject parent, string childName) where T : DependencyObject
        {
            if (parent == null) return null;
            
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i);
                
                if (child is T && (child as FrameworkElement)?.Name == childName)
                {
                    return (T)child;
                }
                
                T childOfChild = FindChild<T>(child, childName);
                if (childOfChild != null)
                    return childOfChild;
            }
            
            return null;
        }

        private void LoadStaticData()
        {
            var pcData = new ObservableCollection<PCDisplayModel>
            {
                new PCDisplayModel { Name = "LAB1-PC01", IP = "IP: 192.168.1.101", OS = "OS: Windows 11", CPU = "CPU: 0%", Network = "Network: 0.0 Mbps", RAM = "RAM: 0%", User = "", StatusColor = new SolidColorBrush(Color.FromRgb(239, 68, 68)) },
                new PCDisplayModel { Name = "LAB1-PC02", IP = "IP: 192.168.1.102", OS = "OS: Windows 11", CPU = "CPU: 0%", Network = "Network: 0.0 Mbps", RAM = "RAM: 0%", User = "", StatusColor = new SolidColorBrush(Color.FromRgb(239, 68, 68)) },
                new PCDisplayModel { Name = "LAB1-PC03", IP = "IP: 192.168.1.103", OS = "OS: Windows 11", CPU = "CPU: 0%", Network = "Network: 0.0 Mbps", RAM = "RAM: 0%", User = "", StatusColor = new SolidColorBrush(Color.FromRgb(239, 68, 68)) },
                new PCDisplayModel { Name = "LAB1-PC04", IP = "IP: 192.168.1.104", OS = "OS: Windows 11", CPU = "CPU: 0%", Network = "Network: 0.0 Mbps", RAM = "RAM: 0%", User = "", StatusColor = new SolidColorBrush(Color.FromRgb(239, 68, 68)) },
                
                new PCDisplayModel { Name = "LAB1-PC05", IP = "IP: 192.168.1.105", OS = "OS: Windows 11", CPU = "CPU: 65%", Network = "Network: 6.0 Mbps", RAM = "RAM: 43%", User = "User: student5", StatusColor = new SolidColorBrush(Color.FromRgb(245, 158, 11)) },
                new PCDisplayModel { Name = "LAB1-PC06", IP = "IP: 192.168.1.106", OS = "OS: Windows 11", CPU = "CPU: 87%", Network = "Network: 9.1 Mbps", RAM = "RAM: 73%", User = "User: student6", StatusColor = new SolidColorBrush(Color.FromRgb(245, 158, 11)) },
                new PCDisplayModel { Name = "LAB1-PC07", IP = "IP: 192.168.1.107", OS = "OS: Windows 11", CPU = "CPU: 27%", Network = "Network: 3.0 Mbps", RAM = "RAM: 0%", User = "User: student7", StatusColor = new SolidColorBrush(Color.FromRgb(245, 158, 11)) },
                new PCDisplayModel { Name = "LAB1-PC08", IP = "IP: 192.168.1.108", OS = "OS: Windows 11", CPU = "CPU: 0%", Network = "Network: 0.0 Mbps", RAM = "RAM: 57%", User = "", StatusColor = new SolidColorBrush(Color.FromRgb(16, 185, 129)) },
                
                new PCDisplayModel { Name = "LAB1-PC09", IP = "IP: 192.168.1.109", OS = "OS: Windows 11", CPU = "CPU: 69%", Network = "Network: 3.8 Mbps", RAM = "RAM: 34%", User = "User: student9", StatusColor = new SolidColorBrush(Color.FromRgb(16, 185, 129)) },
                new PCDisplayModel { Name = "LAB1-PC10", IP = "IP: 192.168.1.110", OS = "OS: Windows 11", CPU = "CPU: 9%", Network = "Network: 3.6 Mbps", RAM = "RAM: 26%", User = "User: student10", StatusColor = new SolidColorBrush(Color.FromRgb(16, 185, 129)) },
                new PCDisplayModel { Name = "LAB1-PC11", IP = "IP: 192.168.1.111", OS = "OS: Windows 11", CPU = "CPU: 35%", Network = "Network: 5.5 Mbps", RAM = "RAM: 1%", User = "User: student11", StatusColor = new SolidColorBrush(Color.FromRgb(16, 185, 129)) },
                new PCDisplayModel { Name = "LAB1-PC12", IP = "IP: 192.168.1.112", OS = "OS: Windows 11", CPU = "CPU: 11%", Network = "Network: 5.4 Mbps", RAM = "RAM: 93%", User = "User: student12", StatusColor = new SolidColorBrush(Color.FromRgb(16, 185, 129)) },
                
                new PCDisplayModel { Name = "LAB1-PC13", IP = "IP: 192.168.1.113", OS = "OS: Windows 11", CPU = "CPU: 40%", Network = "Network: 6.9 Mbps", RAM = "RAM: 64%", User = "User: student13", StatusColor = new SolidColorBrush(Color.FromRgb(16, 185, 129)) },
                new PCDisplayModel { Name = "LAB1-PC14", IP = "IP: 192.168.1.114", OS = "OS: Windows 11", CPU = "CPU: 79%", Network = "Network: 8.9 Mbps", RAM = "RAM: 68%", User = "User: student14", StatusColor = new SolidColorBrush(Color.FromRgb(16, 185, 129)) },
                new PCDisplayModel { Name = "LAB1-PC15", IP = "IP: 192.168.1.115", OS = "OS: Windows 11", CPU = "CPU: 95%", Network = "Network: 8.1 Mbps", RAM = "RAM: 67%", User = "User: student15", StatusColor = new SolidColorBrush(Color.FromRgb(16, 185, 129)) },
                new PCDisplayModel { Name = "LAB1-PC16", IP = "IP: 192.168.1.116", OS = "OS: Windows 11", CPU = "CPU: 68%", Network = "Network: 7.0 Mbps", RAM = "RAM: 43%", User = "User: student16", StatusColor = new SolidColorBrush(Color.FromRgb(16, 185, 129)) },
                
                new PCDisplayModel { Name = "LAB1-PC17", IP = "IP: 192.168.1.117", OS = "OS: Windows 11", CPU = "CPU: 50%", Network = "Network: 0.2 Mbps", RAM = "RAM: 31%", User = "User: student17", StatusColor = new SolidColorBrush(Color.FromRgb(16, 185, 129)) },
                new PCDisplayModel { Name = "LAB1-PC18", IP = "IP: 192.168.1.118", OS = "OS: Windows 11", CPU = "CPU: 99%", Network = "Network: 8.0 Mbps", RAM = "RAM: 1%", User = "User: student18", StatusColor = new SolidColorBrush(Color.FromRgb(16, 185, 129)) },
                new PCDisplayModel { Name = "LAB1-PC19", IP = "IP: 192.168.1.119", OS = "OS: Windows 11", CPU = "CPU: 11%", Network = "Network: 7.2 Mbps", RAM = "RAM: 36%", User = "User: student19", StatusColor = new SolidColorBrush(Color.FromRgb(16, 185, 129)) },
                new PCDisplayModel { Name = "LAB1-PC20", IP = "IP: 192.168.1.120", OS = "OS: Windows 11", CPU = "CPU: 71%", Network = "Network: 1.4 Mbps", RAM = "RAM: 7%", User = "User: student20", StatusColor = new SolidColorBrush(Color.FromRgb(16, 185, 129)) }
            };

            PCItemsControl.ItemsSource = pcData;
        }
    }

    public class PCDisplayModel
    {
        public string Name { get; set; }
        public string IP { get; set; }
        public string OS { get; set; }
        public string CPU { get; set; }
        public string Network { get; set; }
        public string RAM { get; set; }
        public string User { get; set; }
        public SolidColorBrush StatusColor { get; set; }
    }
}