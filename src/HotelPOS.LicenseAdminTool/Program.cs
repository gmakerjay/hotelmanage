using System;
using System.Windows.Forms;

namespace HotelPOS.LicenseAdminTool;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        ApplicationConfiguration.Initialize();
        Application.Run(new AdminMainForm());
    }
}
