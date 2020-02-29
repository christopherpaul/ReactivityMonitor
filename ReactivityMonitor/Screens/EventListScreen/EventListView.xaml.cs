using ReactivityMonitor.Definitions;
using ReactivityMonitor.Services;
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

namespace ReactivityMonitor.Screens.EventListScreen
{
    /// <summary>
    /// Interaction logic for EventListView.xaml
    /// </summary>
    public partial class EventListView : UserControl
    {
        public EventListView()
        {
            InitializeComponent();
        }

        private void DataGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            Func<Selection, Selection> changer = selection =>
            {
                foreach (var item in e.RemovedItems.OfType<EventItem>())
                {
                    selection = selection.RemoveEvent(item);
                }

                foreach (var item in e.AddedItems.OfType<EventItem>())
                {
                    selection = selection.AddEvent(item);
                }

                return selection;
            };

            Commands.ChangeSelectedEventItems.Execute(changer);
        }
    }
}
