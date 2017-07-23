/*---------------------------------------------------------------------------------------------
*  Copyright (c) Nicolas Jinchereau. All rights reserved.
*  Licensed under the MIT License. See License.txt in the project root for license information.
*--------------------------------------------------------------------------------------------*/

using System.Windows;

namespace ShowdownSoftware
{
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            if(e.Args.Length > 0 && e.Args[0] == "/register")
            {
                Util.RegisterApplication(false);
                Shutdown();
            }

            base.OnStartup(e);
        }
    }
}
