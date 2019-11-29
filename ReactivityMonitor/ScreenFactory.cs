﻿using ReactivityMonitor.Screens;
using ReactivityMonitor.Screens.ConnectionScreen;
using ReactivityMonitor.Screens.HomeScreen;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ReactivityMonitor
{
    internal sealed class ScreenFactory : IScreenFactory
    {
        private readonly IServiceProvider mServiceProvider;

        public ScreenFactory(IServiceProvider serviceProvider)
        {
            mServiceProvider = serviceProvider;
        }

        public IConnectionScreen CreateConnectionScreen() => GetInstance<IConnectionScreen>();

        public IHomeScreen CreateHomeScreen() => GetInstance<IHomeScreen>();

        private T GetInstance<T>() => (T)mServiceProvider.GetService(typeof(T));
    }
}