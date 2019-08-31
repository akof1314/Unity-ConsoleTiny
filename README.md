# Unity-ConsoleTiny
Console Tiny is a powerful replacement for Unity's editor console. 

## Feature
- Text Search Filter
- Multi-line Display
- Colored Callstacks
- Callstack Navigation
- Custom Filters
- DLL Support

![](https://github.com/akof1314/Unity-ConsoleTiny/raw/master/DLLTest/screenshot.png)

## Install
- Unity 5.x
	- put [UnityEditor.Facebook.Extensions.dll](https://github.com/akof1314/Unity-ConsoleTiny/blob/master/5.6/Test/Assets/Editor/UnityEditor.Facebook.Extensions.dll) to `Assets\Editor\`
- Unity 2017.x
	- `UnityPackageManager\manifest.json`
- Unity 2018.x (or later)
	- `Packages\manifest.json`

`manifest.json` file add line:

```
"com.wuhuan.consoletiny": "file:../PackagesCustom/com.wuhuan.consoletiny"

```

## Usage
Open window: `Ctrl+Shift+T` (Linux/Windows) or `Cmd+Shift+T` (OS X).

## License
MIT