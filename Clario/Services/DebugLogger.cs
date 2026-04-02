using System;
using System.Diagnostics;

namespace Clario.Services;

public static class DebugLogger
{
    [Conditional("DEBUG")]
    public static void Log(object message) => Console.WriteLine(message);
}