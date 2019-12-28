using ReactivityMonitor.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ReactivityMonitor.Screens.ObservablesScreen
{
    internal class ObservablesScreenItemFactory : Factory, IObservablesScreenItemFactory
    {
        public ObservablesScreenItemFactory(IServiceProvider serviceProvider) : base(serviceProvider) { }

        public ObservablesListItem CreateItem(IObservableInstance obs)
        {
            var item = GetInstance<ObservablesListItem>();
            item.ObservableInstance = obs;
            return item;
        }
    }
}
