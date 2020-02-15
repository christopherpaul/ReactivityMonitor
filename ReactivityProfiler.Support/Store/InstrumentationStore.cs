using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;

namespace ReactivityProfiler.Support.Store
{
    internal sealed class InstrumentationStore : IInstrumentationStore
    {
        public int GetEventCount()
        {
            return NativeMethods.GetStoreEventCount();
        }

        public object GetEvent(int index)
        {
            byte[] rawEvent = NativeMethods.ReadStoreEvent(index);
            return Decode(rawEvent);
        }

        private static object Decode(byte[] rawEvent)
        {
            var reader = new BinaryReader(new MemoryStream(rawEvent), Encoding.Unicode);
            uint eventTypeId = reader.ReadUInt32();

            switch (eventTypeId)
            {
                case 0:
                    return DecodeModuleLoadEvent(reader);
                case 1:
                    return DecodeMethodEvent(reader);
                case 2:
                    return DecodeMethodCallInstrumentedEvent(reader);
                case 3:
                    return DecodeMethodInstrumentationDoneEvent(reader);
                default:
                    return null;
            }
        }

        private static object DecodeMethodInstrumentationDoneEvent(BinaryReader reader)
        {
            var e = new MethodDoneEvent();
            e.InstrumentedMethodId = reader.ReadInt32();
            return e;
        }

        private static object DecodeMethodEvent(BinaryReader reader)
        {
            var e = new MethodInfoEvent();
            e.InstrumentedMethodId = reader.ReadInt32();
            e.ModuleId = reader.ReadUInt64();
            e.FunctionToken = reader.ReadUInt32();
            e.OwningTypeName = ReadInt32LengthString(reader);
            e.Name = ReadInt32LengthString(reader);
            return e;
        }

        private static object DecodeModuleLoadEvent(BinaryReader reader)
        {
            var e = new ModuleLoadEvent();
            e.ModuleId = reader.ReadUInt64();
            e.ModulePath = ReadInt32LengthString(reader);
            e.AssemblyName = ReadInt32LengthString(reader);
            return e;
        }

        private static object DecodeMethodCallInstrumentedEvent(BinaryReader reader)
        {
            var e = new MethodCallInstrumentedEvent();
            e.InstrumentationPointId = reader.ReadInt32();
            e.InstrumentedMethodId = reader.ReadInt32();
            e.InstructionOffset = reader.ReadInt32();
            e.CalledMethodName = ReadInt32LengthString(reader);
            return e;
        }

        private static string ReadInt32LengthString(BinaryReader reader)
        {
            int length = reader.ReadInt32();
            char[] chars = reader.ReadChars(length);
            return new string(chars);
        }
    }
}
