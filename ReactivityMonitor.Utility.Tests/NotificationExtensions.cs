using Microsoft.Reactive.Testing;
using System;
using System.Collections.Generic;
using System.Reactive;
using System.Text;

namespace ReactivityMonitor.Utility.Tests
{
    internal static class NotificationExtensions
    {
        public static Recorded<Notification<T>> At<T>(this Notification<T> notification, long tick)
        {
            return new Recorded<Notification<T>>(tick, notification);
        }
    }
}
