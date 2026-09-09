using FlowEditor.Diagnostics;
using System;
using System.Linq;
using System.Windows;

namespace FlowEditor
{
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);
            if (e.Args.Contains("--ui-smoke-test", StringComparer.OrdinalIgnoreCase))
            {
                Shutdown(UiSmokeTest.Run());
                return;
            }
            new MainWindow().Show();
        }
    }
}
