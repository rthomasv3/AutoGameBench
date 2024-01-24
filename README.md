# AutoGameBench

This is a simple project to explore game automated testing and benchmarking.

The core idea is simple - hook into the rendering library to collect frame and performance statistics and use something like the SendInput() win32 function to control the game. This should allow basic automated testing and benchmarking.


## Prerequisites

A Windows PC with [.NET 8.0](https://dotnet.microsoft.com/en-us/download/dotnet/8.0) Runtime installed. To build you'll need the SDK.


## Build and Run

Pull down the source code and run the following command in the solution directory:

```
dotnet build -c Release
```

Then just run `AutoGameBench.exe`.


## Current State

Currently, the project is able to read your Steam libraries and display a list of games you can select from. Each game can have any number of automated jobs available for it in the `Automation\Jobs` directory. Jobs are associated with games by their Steam ID.

A job is broken down into the following steps:
1. Initialization
    * Runs any startup or initial tasks like loading a particular save.
    * Frame timings are not recorded during this step.
2. Run Actions
    * These are the main actions you want to perform for the run, like walking a certain path.
    * Frame timings start to be recorded just before the first action starts.
3. Cleanup
    * Any cleanup actions, like quitting the game safely.
    * Frame timings are not recorded during this step.


### Features:
1. Read your steam libraries.
2. Launch games.
3. Inject a native assembly into the game.
4. Load additional assemblies via a plugin pattern (including all dependencies).
5. Intercept DirectX 11 and 12 Present call to get performance info.
6. Collect sensor data during run (CPU temp, GPU temp, memory usage, and more).
7. Run basic actions in a game by simulating mouse and keyboard inputs.
8. Check for and recognize any text that appears in game (English only right now).
9. Take screenshots.


### Example

A very simple example job for Resident Evil 4 Remake is included. The script will load in your most recent save, then perform a quick turn, fire, reload, and exit the game.

**Example Results**

```
{
  "JobName": "RE4 POC",
  "GameId": "2050650",
  "StartTime": "2024-01-24T17:35:56.8645656-06:00",
  "InitializationCompleteTime": "2024-01-24T17:37:17.0808078-06:00",
  "ActionsCompleteTime": "2024-01-24T17:37:43.750674-06:00",
  "EndTime": "2024-01-24T17:38:01.1957647-06:00",
  "AverageFps": 69.2023572482173,
  "OnePercentLowFps": 62.940360177579386,
  "PointOnePercentLowFps": 60.346711975872026,
  "AverageCpuTemperature": 58.05299377441406,
  "AverageCpuLoad": 24.3011531829834,
  "AverageMemoryUsage": 17.337448120117188,
  "AverageGpuTemperature": 69.02173614501953,
  "AverageGpuHotSpotTemperature": 81.18274688720703,
  "AverageGpuMemoryUsage": 5493.86962890625,
  "Screenshots": [
    "Automation\\Screenshots\\RE4_POC_638417146371385281.png"
  ],
  "Success": true,
  "Error": null
}
```

**Full Output Log**:

```
Games:
0       Custom EXE
1:      DARK SOULST III
2:      Remnant II
3:      Resident Evil 4
4:      Sea of Stars
5:      Steamworks Common Redistributables
6:      The Last of UsT Part I
Select a game (enter number): 3

Available Jobs:
1:      Basic Example
2:      RE4 POC
Select a job (enter number): 2
Injected.
Log - Hook Entered.
Log - DirectXVersion: Direct3D12
Log - IDXGIFactory4 Created: True
Log - IDXGIAdapter1 Found: True
Log - ID3D12Device Loaded With Adapter: True
Log - ID3D12CommandQueue Created: True
Log - Got Window Handle: 133840
Log - IDXGISwapChain1 Created Using Temp Window: True
Log - IDXGISwapChain3 Found: True
Log - Got present address: 140736340576192
Log - DirectX12 Hook Applied.
Hook Complete.
Initializing job...
Delaying: autosave...
Empty page!!
...
Empty page!!
Delay Complete - Text Found.
Delaying: 3000...
Delay Complete.
KeyPress: RETURN
Delaying: 1000...
Delay Complete.
KeyPress: RETURN
KeyPress: RETURN
Delaying: continue...
Delay Complete - Text Found.
KeyPress: SPACE
Initialization Complete.
Starting actions...
Delaying: 5000...
Delay Complete.
KeyPress: VK_Q
MouseDown: right
MouseClick: left
MouseUp: right
KeyPress: VK_R
Delaying: 5000...
Delay Complete.
Actions Complete.
Starting cleanup...
KeyPress: ESCAPE
KeyPress: UP
KeyPress: RETURN
KeyPress: UP
KeyPress: RETURN
Delaying: 5000...
Delay Complete.
Cleanup Complete.
Saved: Automation\2050650_638416173197208521.json
Result:
{
  "JobName": "RE4 POC",
  "GameId": "2050650",
  "StartTime": "2024-01-24T17:35:56.8645656-06:00",
  "InitializationCompleteTime": "2024-01-24T17:37:17.0808078-06:00",
  "ActionsCompleteTime": "2024-01-24T17:37:43.750674-06:00",
  "EndTime": "2024-01-24T17:38:01.1957647-06:00",
  "AverageFps": 69.2023572482173,
  "OnePercentLowFps": 62.940360177579386,
  "PointOnePercentLowFps": 60.346711975872026,
  "AverageCpuTemperature": 58.05299377441406,
  "AverageCpuLoad": 24.3011531829834,
  "AverageMemoryUsage": 17.337448120117188,
  "AverageGpuTemperature": 69.02173614501953,
  "AverageGpuHotSpotTemperature": 81.18274688720703,
  "AverageGpuMemoryUsage": 5493.86962890625,
  "Screenshots": [
    "Automation\\Screenshots\\RE4_POC_638417146371385281.png"
  ],
  "Success": true,
  "Error": null
}
Log - DirectX12 Hook Removed.
Log - Hook Stopped.
Hook Stopped.
Complete. Press any key to exit.
```

## Problems
* It's written in .NET 8, but making this multiplatform may not be possible due to the way hooks work and other Win API calls.
* Will need to wrap any SendInput calls in an abstraction, then use something like `xdotool` with Linux running X11 and `uinput` for Wayland.
    * For Wayland, maybe `ydotool` or `dotool` as a higher level `xdotool` equivalent.
* All games are different. Writing generic automated tests will be very hard - this isn't like Selenium, we can't just get elements using CSS selectors. Finding text on images may be the only option without integrations into each engine.
* Need to figure out how to hook into more graphics libraries like Vulkan.


## Goals

1. Multiplatform would be great. Not sure if it's possible, but an automated testing framework that runs on Windows, Mac, and Linux would be a major time and money saver and would do wonders for game quality.
2. Performant enough to not impact results when benchmarking.
3. Wrap it all up in a nice UI with graphs and easy test management and creation.
4. Simple CLI for use with CI/CD.
