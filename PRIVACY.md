# SmartSink Privacy Policy

Effective date: August 4, 2026

SmartSink is published by Ivan Potiienko (`xxanqw`). This policy explains what information SmartSink accesses, why it is needed, and what remains on your Windows device.

## Summary

SmartSink does not operate an online service and does not include advertising, analytics, telemetry, user accounts, or cloud synchronization. It does not sell, rent, or transmit personal information to the publisher or to third parties.

SmartSink uses Windows Bluetooth and audio APIs to let a compatible paired phone send audio to the current Windows default output. Windows receives, decodes, and plays the audio stream. SmartSink does not record, inspect, analyze, store, or transmit audio samples.

## Information accessed and stored locally

SmartSink accesses only the information needed to provide and troubleshoot its receiver features:

- The display name, Windows device identifier, availability, and connection state of compatible already-paired Bluetooth audio devices.
- Basic availability information for the default Windows Bluetooth adapter.
- Receiver preferences, window and notification-area preferences, automatic reconnect choices, and the Windows device identifier of the last selected phone.
- The current state of SmartSink's packaged Windows startup task.
- Local diagnostic logs containing timestamps, receiver lifecycle events, connection states, compatible device display names, and technical error details. Developer Debug builds may also log Windows device identifiers; Store Release builds do not write those Debug-only entries.

This information is stored in SmartSink's per-user application data on the Windows device. SmartSink does not upload it.

## Bluetooth audio

When you select **Route** or turn on **Accept phone audio**, SmartSink asks the existing Windows Bluetooth stack to create an A2DP audio connection. Windows routes the incoming audio to the current default output. SmartSink does not create a virtual audio device and does not implement or inspect Bluetooth codecs.

Selecting **Stop** disposes SmartSink's connection. It does not unpair the phone, disable Bluetooth, or modify Phone Link.

## Clipboard and external links

SmartSink writes text to the Windows clipboard only when you select **Copy status**. The copied text excludes Bluetooth addresses and Windows device identifiers.

Buttons for the author website, GitHub, and Windows Settings ask Windows to open the corresponding URI in the appropriate external application. SmartSink does not receive browsing data from those applications. Their own privacy terms apply.

## Capabilities

The package declares:

- `bluetooth`, to discover compatible already-paired devices and use the default Bluetooth adapter.
- `runFullTrust`, because SmartSink is a packaged WinUI 3 desktop application that provides a native Windows notification-area icon and desktop window integration. This does not request administrator elevation.

SmartSink does not request microphone, camera, location, contacts, broad file-system, radio-control, or Internet-client capabilities.

## Sharing and disclosure

SmartSink does not send locally stored information to the publisher, advertising networks, analytics providers, data brokers, or other third parties. Windows processes Bluetooth, audio, startup-task, package, and Settings operations as part of the operating system.

Information may be disclosed only if you choose to copy it and share it yourself, or if disclosure is required from the publisher by applicable law. Because the publisher does not receive SmartSink's local data, the publisher cannot access or disclose that local data.

## Retention and deletion

Settings and logs remain on the device until they are removed through Windows app reset or uninstall functionality, or until you manually delete the application's local data. Stopping audio reception removes the active app-owned Bluetooth audio connection but does not erase saved preferences or logs.

## Your controls

You can:

- Stop and release SmartSink's Bluetooth audio connection at any time.
- Manage pairing and Bluetooth through Windows Settings.
- Manage automatic startup through Windows Startup Apps or Task Manager.
- Review copied status text before sharing it.
- Reset or uninstall SmartSink through Windows Settings to remove its local application data.

## Security

SmartSink relies on Windows package isolation and per-user application storage for locally saved information. No method of software storage is completely secure, but SmartSink minimizes the information it stores and does not transmit that information over a network.

## Children's privacy

SmartSink is not directed at children and does not knowingly collect personal information from children. The app has no account registration, advertising, or online data collection.

## Changes to this policy

This policy may be updated when SmartSink's functionality or data practices change. The effective date at the top of this document will be updated when material changes are made.

## Contact

Questions about this policy can be submitted through the [SmartSink GitHub repository](https://github.com/xxanqw/SmartSink) or the [publisher website](https://xxanqw.pp.ua).
