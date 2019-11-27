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
                    return DecodeMethodCallInstrumentedEvent(reader);
                default:
                    return null;
            }
        }

        private static object DecodeModuleLoadEvent(BinaryReader reader)
        {
            var e = new ModuleLoadEvent();
            e.ModuleId = reader.ReadUInt64();
            e.ModulePath = ReadInt32LengthString(reader);
            return e;
        }

        private static object DecodeMethodCallInstrumentedEvent(BinaryReader reader)
        {
            var e = new MethodCallInstrumentedEvent();
            e.InstrumentationPointId = reader.ReadInt32();
            e.ModuleId = reader.ReadUInt64();
            e.FunctionToken = reader.ReadUInt32();
            e.InstructionOffset = reader.ReadInt32();
            e.OwningTypeName = ReadInt32LengthString(reader);
            e.CallingMethodName = ReadInt32LengthString(reader);
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
