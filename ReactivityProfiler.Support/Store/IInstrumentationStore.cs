namespace ReactivityProfiler.Support.Store
{
    internal interface IInstrumentationStore
    {
        object GetEvent(int index);
        int GetEventCount();
    }
}