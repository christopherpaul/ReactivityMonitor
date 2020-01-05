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

namespace ReactivityMonitor.Screens.MarbleDiagramScreen
{
    /// <summary>
    /// Interaction logic for MarbleDiagramScreenView.xaml
    /// </summary>
    public partial class MarbleDiagramScreenView : UserControl
    {
        public MarbleDiagramScreenView()
        {
            InitializeComponent();
        }

        private void ScrollViewer_Initialized(object sender, EventArgs e)
        {
            MarbleScroller = (ScrollViewer)sender;
        }

        public static readonly DependencyProperty MarbleScrollerProperty = DependencyProperty.Register(
            nameof(MarbleScroller),
            typeof(ScrollViewer),
            typeof(MarbleDiagramScreenView));

        public ScrollViewer MarbleScroller
        {
            get => (ScrollViewer)GetValue(MarbleScrollerProperty);
            set => SetValue(MarbleScrollerProperty, value);
        }
    }
}
