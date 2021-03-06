﻿using Microsoft.Win32.SafeHandles;
using Spfx.Utilities.Runtime;
using System;
using System.Runtime.InteropServices;
using System.Threading;

namespace Spfx.Utilities
{
    internal static class SafeHandleUtilities
    {
        private class ZeroSafeHandle : NoDisposeSafeHandle
        {
            public override bool IsInvalid => true;

            protected override bool ReleaseHandle()
            {
                return true;
            }
        }

        public static SafeHandle NullSafeHandle { get; } = new ZeroSafeHandle();

        public static string SerializeHandle(SafeHandle safeHandle)
        {
            return SerializeHandle(safeHandle.DangerousGetHandle());
        }

        public static string SerializeHandle(IntPtr intPtr)
        {
            return intPtr.ToInt64().ToString();
        }

        public static IntPtr DeserializeRawHandleFromString(string str)
        {
            return new IntPtr(long.Parse(str));
        }

        public static SafeHandle DeserializeHandleFromString(string str)
        {
            var intptr = DeserializeRawHandleFromString(str);
            return CreateSafeHandleFromIntPtr(intptr);
        }

        public static SafeHandle CreateSafeHandleFromIntPtr(IntPtr intptr, bool ownsHandle = true)
        {
            return HostFeaturesHelper.IsWindows
               ? new SafeWaitHandle(intptr, ownsHandle)
               : throw new NotImplementedException();
        }

        public static WaitHandle CreateWaitHandleFromString(string str)
        {
            return CreateWaitHandle(DeserializeHandleFromString(str));
        }

        public static WaitHandle CreateWaitHandle(SafeHandle handle)
        {
            return new SimpleWaitHandle((SafeWaitHandle)handle);
        }

        private class SimpleWaitHandle : WaitHandle
        {
            public SimpleWaitHandle(SafeWaitHandle h)
            {
                SafeWaitHandle = h;
            }
        }
    }
}
