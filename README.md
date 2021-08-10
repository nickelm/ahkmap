# AHKeyMap
A visual AutoHotKey map.

**AHKeyMap** is a keyboard layout visualizer for AutoHotKey scripts. The purpose of this little utility is to create a visual representation of the hotkeys in an [AutoHotKey](http://www.autohotkey.org/) script to enable you to more easily understand, memory, and recall your keyboard bindings. The tool parses an AHK script looking for hotkey specifications and descriptive comments. These are then draw on top of a visual representation of your keyboard.

## Usage
AHKeyMap is designed to be run both as a standalone application as well as from an AHK script. For the former use, merely launch the program, open the AHK script file you want to visualize, and view it.

To launch AHKeyMap from inside an AHK script, place the AHKeyMap executable inside your script directory and use the following syntax in your script:

    ; Launch visualizer
    $^!v::Run, %A_ScriptDir%\AutoHotKeyMap.exe %A_ScriptFullPath%   

This will allow you to hit Ctrl+Alt+V to visualize the current script.

## Commenting

In order for AHKeyMap to work properly, you will need to have commented each hotkey with a descriptive comment **prior** to the hotkey in the script file. Please use the semi-colon for commenting (';'). Here is an example of a proper use of commenting:

    ; Cast magic
    $m::Send, x

## Feedback

Please direct feeback to Madgrim Laeknir (BelomarFleetfoot#0319 on Discord).
