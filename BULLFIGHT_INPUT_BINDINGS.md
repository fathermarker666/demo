# Bullfight Input Bindings

This file is the current binding reference for the demo build. It summarizes the bindings that are spread across:

- `Assets/Infima Games/Low Poly Shooter Pack - Free Sample/Input/IA_Player.inputactions`
- `Assets/Script/input/playercontrols.inputactions`
- hardcoded keys in `BullfightGameFlow.cs`, `BullfightPauseSettingsUI.cs`, `BullfightStartMenu.cs`, and `BullfightPlayerController.cs`

## Core FPS bindings

- `W / A / S / D`: movement
- `Mouse`: look
- `LeftShift`: run
- `Space`: reserved by `IA_Player` as `Jump`
- `R`: reload
- `H`: holster
- `T`: inspect
- `Q`: inventory next
- `Tab`: tutorial
- `Esc`: lock cursor
- `5`: time speed toggle
- `8`: time speed down
- `9`: time speed up

## Bullfight gameplay bindings

- `C`: hold cloth keyboard fallback
- `Space`: swing / capa keyboard fallback
- `F`: attack / banderillas
- `LeftCtrl`: evade / dash
- `G`: Phase Two calibration keyboard fallback
- `E`: Phase Two stab keyboard fallback
- `B`: ending skip

## UI bindings

- `Enter / NumpadEnter / Space`: start menu confirm
- `Esc`: pause toggle
- `Y`: pause menu quick action 1
- `B`: pause menu quick action 2
- `X`: pause menu quick action 3
- `A`: pause menu quick action 4
- `W / S`: pause menu vertical input
- `Up / Down`: pause menu vertical input

## Sensor bindings

- ultrasonic distance packet: Phase One hold cloth
- `SWING / CAPA / PHASE1_SWING`: Phase One swing sensor trigger
- `READY`: Phase Two calibration ready
- `THRUST / STAB`: Phase Two stab trigger
- `FORCE`, `POWER`, `THRUST`, CSV force packets: Phase Two force signal
- `PHASE2_CALIBRATION_START/STOP` and related aliases: Phase Two calibration hold state

## Conflict notes

- `Space` is intentionally context-gated:
  - start menu only uses it while the start menu is active
  - bullfight gameplay uses it for swing / capa
  - `IA_Player` still reserves it for `Jump`, so treat `Space` as a conflict-prone key if the base FPS jump path is restored later
- `7 / 8 / 9 / 0` are debug-related keys:
  - they must only be considered valid in Editor or Development Build
  - the demo path should keep `enableDebugShortcuts` disabled by default
- `B` is overloaded:
  - pause UI uses it inside the pause menu context
  - ending skip uses it only during ending playback
  - avoid assigning new global gameplay actions to `B`

## Recommended future rule

If a new binding is added, update this file in the same change so the project keeps one human-readable reference instead of drifting between `.inputactions` and script-local `KeyCode` values.
