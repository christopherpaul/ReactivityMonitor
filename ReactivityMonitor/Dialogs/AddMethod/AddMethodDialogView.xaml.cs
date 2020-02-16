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

namespace ReactivityMonitor.Dialogs.AddMethod
{
    /// <summary>
    /// Interaction logic for AddMethodDialogView.xaml
    /// </summary>
    public partial class AddMethodDialogView : UserControl
    {
        public AddMethodDialogView()
        {
            InitializeComponent();
        }

        private void SearchBox_Loaded(object sender, RoutedEventArgs e)
        {
            ((TextBox)sender).Focus();
        }

        private void SearchBoxParent_Loaded(object sender, RoutedEventArgs e)
        {
            UIElement parent = (UIElement)sender;
            parent.AddHandler(KeyDownEvent, (KeyEventHandler)HandleListBoxNavigationKeys, true);
        }

        private void HandleListBoxNavigationKeys(object sender, KeyEventArgs e)
        {
            switch (e.Key)
            {
                case Key.Up:
                    MoveSelectedIndex(-1);
                    break;

                case Key.Down:
                    MoveSelectedIndex(1);
                    break;
            }

            void MoveSelectedIndex(int delta)
            {
                int index = MatchingMethodList.SelectedIndex;
                SetSelectedIndex(index < 0 ? 0 : index + delta);
            }

            void SetSelectedIndex(int index)
            {
                int count = MatchingMethodList.Items.Count;
                if (count <= 0)
                {
                    return;
                }

                index = Math.Min(count - 1, Math.Max(0, index));

                MatchingMethodList.SelectedIndex = index;
                MatchingMethodList.ScrollIntoView(MatchingMethodList.Items[index]);
            }
        }
    }
}
