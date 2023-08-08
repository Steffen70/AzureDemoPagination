﻿using System.Diagnostics;
#pragma warning disable CA1822

namespace SP6LogicDemo;

public class Logic
{
    public string GetProgramName()
    {
        var processName = Process.GetCurrentProcess().ProcessName;

        return processName == "SPPaginatedGridControl" ? "Client" : "Server";
    }
}