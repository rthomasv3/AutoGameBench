# AutoGameBench

This is a simple project to explore game automated testing and benchmarking.

The core idea is simple - hook into the rendering library to collect frame and performance statistics and use something like the SendInput() win32 function to control the game. This should allow basic automated testing and benchmarking.


## Prerequisites

A Windows PC with the [.NET 8.0 Runtime](https://dotnet.microsoft.com/en-us/download/dotnet/8.0) installed.


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
6. Run basic actions in a game by simulating mouse and keyboard inputs.


### Example

A very simple example job for Resident Evil 4 Remake is included. The script will load in your most recent save, then perform a quick turn, fire, reload, and exit the game.

**Example Results**

```
{
  "JobName": "RE4 POC",
  "GameId": "2050650",
  "StartTime": "2024-01-21T15:48:38.8246086-06:00",
  "InitializationCompleteTime": "2024-01-21T15:50:16.5896413-06:00",
  "EndTime": "2024-01-21T15:51:11.9400199-06:00",
  "AverageFps": 72.59055073408857,
  "OnePercentLow": 65.66187745945314,
  "PointOnePercentLow": 62.911318489693706,
  "Success": true,
  "Error": null
}
```

**Full Output**:

```
Games:
1:      Sea of Stars
2:      Resident Evil 4
3:      Steamworks Common Redistributables
Select a game (enter number): 2

Available Jobs:
1:      RE4 POC
Select a job (enter number): 1
Injected.
Log - Hook Entered.
Log - DirectXVersion: Direct3D11
Log - Got present address: 140707779987392
Log - DirectX11 Hook Applied.
Hook Complete.
Initializing job...
KeyPress: RETURN
KeyPress: RETURN
KeyPress: RETURN
Delaying: 15000
Delay Complete.
Initialization Complete.
Starting actions...
Delaying: 5000
Delay Complete.
KeyPress: VK_Q
MouseDown: right
MouseClick: left
MouseUp: right
KeyPress: VK_R
Delaying: 5000
Delay Complete.
Actions Complete.
Starting cleanup...
KeyPress: ESCAPE
KeyPress: UP
KeyPress: RETURN
KeyPress: UP
KeyPress: RETURN
Delaying: 5000
Delay Complete.
Cleanup Complete.
Result:
{
  "JobName": "RE4 POC",
  "GameId": "2050650",
  "StartTime": "2024-01-21T15:48:38.8246086-06:00",
  "InitializationCompleteTime": "2024-01-21T15:50:16.5896413-06:00",
  "EndTime": "2024-01-21T15:51:11.9400199-06:00",
  "AverageFps": 72.59055073408857,
  "OnePercentLow": 65.66187745945314,
  "PointOnePercentLow": 62.911318489693706,
  "Success": true,
  "Error": null
}
Log - DirectX11 Hook Removed.
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

1. Multiplatform would be great. Not sure if it's possible, but an automated testing framework that runs in Windows, Mac, and Linux would be a major time and money saver and would do wonders for game quality.
2. Performant enough to not impact results when benchmarking.
3. Wrap it all up in a nice UI with graphs and easy test management and creation.
4. Simple CLI for use with CI/CD.
