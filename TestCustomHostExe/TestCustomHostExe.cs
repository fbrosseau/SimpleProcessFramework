﻿using Spfx.Runtime.Server.Processes.HostProgram;

public class TestCustomHostExe
{
    public const string ExecutableName = nameof(TestCustomHostExe);

    public static bool WasMainCalled { get; set; }

    static void Main()
    {
        WasMainCalled = true;
        SpfxProgram.Main();
    }
}