using DynamicData;
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

namespace ReactivityMonitor.Screens.ObservablesScreen
{
    /// <summary>
    /// Interaction logic for ObservablesListView.xaml
    /// </summary>
    public partial class ObservablesListView : UserControl
    {
        public ObservablesListView()
        {
            InitializeComponent();
        }

        private void DataGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var changeSet = new ChangeSet<ObservablesListItem>();
            changeSet.Add(new Change<ObservablesListItem>(ListChangeReason.RemoveRange, e.RemovedItems.Cast<ObservablesListItem>()));
            changeSet.Add(new Change<ObservablesListItem>(ListChangeReason.AddRange, e.AddedItems.Cast<ObservablesListItem>()));
            (DataContext as ObservablesListViewModel)?.OnSelectedItemsChanged(changeSet);
        }
    }
}
