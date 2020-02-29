using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace ReactivityMonitor.Screens.MonitoringConfigurationScreen
{
    /// <summary>
    /// Interaction logic for MonitoringConfigurationScreenView.xaml
    /// </summary>
    public partial class MonitoringConfigurationScreenView : UserControl
    {
        public MonitoringConfigurationScreenView()
        {
            InitializeComponent();
        }

        private void TreeView_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            var item = ((TreeView)sender).SelectedItem;
            ((MonitoringConfigurationScreenViewModel)DataContext).SelectedItem = item;
        }

        private void TreeView_ContextMenuOpening(object sender, ContextMenuEventArgs e)
        {
            if (e.Source is TreeViewItem item)
            {
                item.IsSelected = true;
            }
        }
    }
}
