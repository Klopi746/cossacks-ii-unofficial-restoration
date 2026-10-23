using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;

namespace CossacksRestoration {
    // Read-only enumeration in the same zero-based EnumDisplayMonitors order as DxWnd.
    public sealed class Display { public int index,x,y,width,height; public bool primary; public string device; }
    public static class DisplayLayout {
        [StructLayout(LayoutKind.Sequential)] struct Rect { public int left,top,right,bottom; }
        [StructLayout(LayoutKind.Sequential,CharSet=CharSet.Unicode)] struct Info {
            public int size; public Rect monitor,work; public uint flags;
            [MarshalAs(UnmanagedType.ByValTStr,SizeConst=32)] public string device;
        }
        delegate bool Callback(IntPtr monitor,IntPtr hdc,IntPtr rect,IntPtr data);
        [DllImport("user32.dll")] static extern bool EnumDisplayMonitors(IntPtr hdc,IntPtr clip,Callback callback,IntPtr data);
        [DllImport("user32.dll",CharSet=CharSet.Unicode)] static extern bool GetMonitorInfo(IntPtr monitor,ref Info info);
        public static Display[] Read() {
            List<Display> displays=new List<Display>(); bool failed=false;
            Callback callback=delegate(IntPtr monitor,IntPtr hdc,IntPtr rect,IntPtr data) {
                Info info=new Info {size=Marshal.SizeOf(typeof(Info))};
                if(!GetMonitorInfo(monitor,ref info)) {failed=true;return false;}
                displays.Add(new Display {index=displays.Count,x=info.monitor.left,y=info.monitor.top,width=info.monitor.right-info.monitor.left,height=info.monitor.bottom-info.monitor.top,primary=(info.flags&1)!=0,device=info.device});
                return true;
            };
            if(!EnumDisplayMonitors(IntPtr.Zero,IntPtr.Zero,callback,IntPtr.Zero)||failed) throw new System.ComponentModel.Win32Exception();
            if(displays.Count(d=>d.primary)!=1) throw new InvalidOperationException("Expected exactly one primary monitor.");
            return displays.ToArray();
        }
        public static Display Primary() { return Read().Single(d=>d.primary); }
    }
}
