using System;
using System.Collections.Generic;
using System.Text;

namespace DirectorRework
{
    internal interface IHookProvider
    {
        bool HooksEnabled { get; set; }

        void OnSettingChanged(object sender, EventArgs args);

        void SetHooks();

        void UnsetHooks();
    }
}
