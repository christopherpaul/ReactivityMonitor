using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ReactivityMonitor
{
    internal abstract class Factory
    {
        private readonly IServiceProvider mServiceProvider;

        protected Factory(IServiceProvider serviceProvider)
        {
            mServiceProvider = serviceProvider;
        }

        protected T GetInstance<T>() => (T)mServiceProvider.GetService(typeof(T));

        protected object GetInstance(Type type) => mServiceProvider.GetService(type);
    }
}
