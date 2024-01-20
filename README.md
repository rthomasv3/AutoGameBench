# AutoGameBench

This is a simple project to explore game automated testing and benchmarking.

The core idea is simple - just hook into the rendering library to collect frame and performance statistics and use something like the SendInput() win32 function to control the game. This should allow basic automated testing and benchmarking.

## Problems
* It's written in dotnet 8, but making this multiplatform may not be possible due to the way hooks work.
* Will need to wrap any SendInput calls in an abstraction, then use something like `xdotool` with Linux running X11 and `uinput` for wayland.
    * For Wayland, maybe `ydotool` or `dotool` as a higher level `xdotool` equivalent.
* All games are different. Writing generic automated tests will be very hard - this isn't like Selenium, we can't just get elements using CSS selectors. Finding text on images may be the only option without integrations into each engine.
* Need to figure out how to hook into more graphics libraries (currently only working with DirectX 11).

## Current State

Currently the project is able to:
1. Read your steam libraries.
2. Launch games.
3. Inject a native assembly into the game.
4. Load additional assemblies via a plugin pattern (including all dependencies).
5. Intercept DirectX 11 Present call to get performance info.
    1. This will allow for screenshots as well.

## Goals

1. Multiplatform would be great. Not sure if it's possible, but an automated testing framework that runs in Windows, Mac, and Linux would be a such a ridiculous time and money saver and would do wonders for game quality.
2. Performant enough to not impact results when benchmarking.
3. Wrap it all up in a nice UI with graphs and easy test management and creation.
4. Simple CLI for use with CI/CD.
