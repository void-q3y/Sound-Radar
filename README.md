Sound Radar Overlay
===================

What it does
------------
- Shows an always-on-top radar-style overlay.
- Estimates left/right sound direction from the current default playback device.
- Works best with stereo audio and borderless/fullscreen-windowed apps.
- Lets you drag and resize the overlay.

Important limitation
--------------------
This app estimates direction from stereo balance. It does not know true real-world sound direction unless the hardware/app provides spatial information or a microphone array.

Hotkeys
-------
- F8  = switch to edit mode
- F9  = toggle lock/click-through mode
- F10 = exit

How to use
----------
1. Run SoundRadarOverlay.exe
2. In edit mode, drag the overlay to move it and drag its edges/corners to resize it.
3. Press F9 to lock it so mouse clicks pass through to the game/app under it.

Visual design
-------------
- Very transparent black radar background
- White center line
- Blue baseline
- Red direction pointer and red end indicator
- Yellow distance points along the pointer

Settings
--------
Position, size, and mode are saved here:
%AppData%\SoundRadarOverlay\settings.json


//Msg : The "SoundRadarOverlay.exe" can work on its own. Other files are its source code.
