# SmartSink

SmartSink turns a Windows PC into a Bluetooth A2DP audio receiver. A compatible paired phone can route audio through the current Windows default output, including headphones connected to the PC.

## Features

- Uses the Windows Bluetooth stack through `Windows.Media.Audio.AudioPlaybackConnection`
- Discovers compatible already-paired phones
- Routes received audio to the current Windows default output
- Releases only SmartSink's A2DP connection when reception is stopped
- Native Windows notification-area icon and context menu
- Single-instance packaged WinUI 3 application
- Windows-managed startup task
- No virtual audio device, custom codec, Bluetooth protocol, or pairing implementation

## Requirements

- Windows 10 build 19041 or newer, or Windows 11
- Compatible Bluetooth hardware and driver with A2DP sink support
- A phone already paired through Windows Settings

## Build from the command line

```powershell
dotnet restore
dotnet build
dotnet build -c Release
```

Run the packaged development build:

```powershell
dotnet run
```

## Privacy

SmartSink has no telemetry, analytics, advertising, accounts, or cloud service. Settings and logs remain in per-user local app storage. Copied status information excludes Bluetooth addresses and device identifiers.

Read the complete [SmartSink Privacy Policy](PRIVACY.md).

## License

SmartSink is source-available, not open source. You may fork the source and edit, build, and run it privately on devices you own or control. You may not redistribute builds, publish packages or releases, submit the app to any store, use it commercially, or present a fork as an official SmartSink release.

Official publishing and distribution rights are reserved exclusively to xxanqw. See [LICENSE](LICENSE) for the complete terms.
