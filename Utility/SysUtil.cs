using System;
using System.Collections.Generic;
using System.Linq;
using System.Management;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Interop;
using Microsoft.VisualBasic;

namespace Layouter.Utility
{
    public class SysUtil
    {
        public static IntPtr GetWindowHandle(Window win)
        {
            IntPtr hwnd = new WindowInteropHelper(win).Handle;
            return hwnd;
        }

        public static bool IsWindows11()
        {
            bool? flag = null;

            if (flag == null)
            {
                try
                {
                    var osVersion = Environment.OSVersion.Version;
                    if (osVersion.Major >= 10 && osVersion.Build >= 22000)
                    {
                        flag = true;
                    }
                    else
                    {
                        using (var searcher = new ManagementObjectSearcher("SELECT Caption FROM Win32_OperatingSystem"))
                        {
                            foreach (var os in searcher.Get())
                            {
                                string caption = os["Caption"].ToString();
                                flag = caption.Contains("Windows 11");
                                break;
                            }
                        }

                        if (flag == null)
                        {
                            flag = (osVersion.Major == 10 && osVersion.Build >= 22000);
                        }
                    }
                }
                catch
                {
                    flag = false;
                }
            }

            return flag.Value;
        }

    }
}
