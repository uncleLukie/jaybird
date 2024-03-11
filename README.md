# jaybird
a discord rich triple j/double j/unearthed cli player written in .NET 8 C#

![jaybird256](https://github.com/uncleLukie/jaybird/assets/22523084/3cbad2ec-72e2-46bf-a7af-761bc199c65c)

## screenie
![image](https://github.com/uncleLukie/jaybird/assets/22523084/d66192df-c4ec-44f1-a1df-c129d295babe)

## why
just wanted an unobtrusive, low memory alternative to listen to these radio stations that would also update my discord status.

## under the hood
- using [LibVLCSharp](https://www.nuget.org/packages/LibVLCSharp) to listen to the AAC+ streams available at [here](https://help.abc.net.au/hc/en-us/articles/4402927208079-Where-can-I-find-direct-stream-URLs-for-ABC-Radio-stations)
- listening to the api that ABC provides for free https://music.abcradio.net.au/api/v1/plays/search.json?station=triplej&limit=1
- parsing the json response with [Newtonsoft.Json](https://www.nuget.org/packages/Newtonsoft.Json)
- updating the discord status with [DiscordRichPresence](https://github.com/Lachee/discord-rpc-csharp)
- making the cli pretty with [Spectre.Console](https://www.nuget.org/packages/Spectre.Console)

## where .exe?
check [releases](https://github.com/uncleLukie/jaybird/releases)

## macOS or linux?!?
i've only built and tested this for win 11, but it would likely build for macOS and linux too, you'd have to install the related NuGet package for those platforms though.

## halp pls?
probs not many people will use app, but i'd love some pull requests or [issues](https://github.com/uncleLukie/jaybird/issues) <3
