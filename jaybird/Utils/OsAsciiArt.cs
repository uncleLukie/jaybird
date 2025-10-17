namespace jaybird.Utils;

using System.Runtime.InteropServices;
using Spectre.Console;

public static class OsAsciiArt
{
    public static string GetOsAsciiArt()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return GetWindowsAsciiArt();
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            return GetMacOsAsciiArt();
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            return GetLinuxAsciiArt();
        }

        return GetGenericAsciiArt();
    }

    public static Color GetOsColor()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return Color.Blue;
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            return Color.Grey;
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            return Color.Yellow;
        }

        return Color.White;
    }

    private static string GetWindowsAsciiArt()
    {
        return @"
        ████████  ████████
        ████████  ████████
        ████████  ████████
        ████████  ████████

        ████████  ████████
        ████████  ████████
        ████████  ████████
        ████████  ████████
";
    }

    private static string GetMacOsAsciiArt()
    {
        return @"
                    'c.
                 ,xNMM.
               .OMMMMo
               OMMM0,
     .;loddo:' loolloddol;.
   cKMMMMMMMMMMNWMMMMMMMMMM0:
 .KMMMMMMMMMMMMMMMMMMMMMMMWd.
 XMMMMMMMMMMMMMMMMMMMMMMMX.
;MMMMMMMMMMMMMMMMMMMMMMMM:
:MMMMMMMMMMMMMMMMMMMMMMMM:
.MMMMMMMMMMMMMMMMMMMMMMMMX.
 kMMMMMMMMMMMMMMMMMMMMMMMMWd.
 .XMMMMMMMMMMMMMMMMMMMMMMMMMMk
  .XMMMMMMMMMMMMMMMMMMMMMMMMK.
    kMMMMMMMMMMMMMMMMMMMMMMd
     ;KMMMMMMMWXXWMMMMMMMk.
       .cooc,.    .,coo:.
";
    }

    private static string GetLinuxAsciiArt()
    {
        return @"
            .-/+oossssoo+/-.
        `:+ssssssssssssssssss+:`
      -+ssssssssssssssssssyyssss+-
    .ossssssssssssssssss+.    .sssso.
   /ssssssssssshdmmNNmmyNMMMMhssssss/
  +ssssssssshmydMMMMMMMNddddyssssssss+
 /sssssssshNMMMyhhyyyyhmNMMMNhssssssss/
.ssssssssdMMMNhsssssssssshNMMMdssssssss.
+sssshhhyNMMNyssssssssssssyNMMMysssssss+
ossyNMMMNyMMhsssssssssssssshmmmhssssssso
ossyNMMMNyMMhsssssssssssssshmmmhssssssso
+sssshhhyNMMNyssssssssssssyNMMMysssssss+
.ssssssssdMMMNhsssssssssshNMMMdssssssss.
 /sssssssshNMMMyhhyyyyhdNMMMNhssssssss/
  +sssssssssdmydMMMMMMMMddddyssssssss+
   /ssssssssssshdmNNNNmyNMMMMhssssss/
    .ossssssssssssssssss+.    .sssso.
      -+sssssssssssssssssssyssss+-
        `:+ssssssssssssssssss+:`
            .-/+oossssoo+/-.
";
    }

    private static string GetGenericAsciiArt()
    {
        return @"
    ╔═══════════════╗
    ║    SYSTEM     ║
    ║               ║
    ║   ┌───────┐   ║
    ║   │       │   ║
    ║   │  OS   │   ║
    ║   │       │   ║
    ║   └───────┘   ║
    ║               ║
    ╚═══════════════╝
";
    }
}
