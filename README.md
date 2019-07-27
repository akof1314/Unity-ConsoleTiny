# Unity-ConsoleTiny
Unity Console Tiny

## Feature
- Text Search Filter
- Multi-line Display
- Colored Callstacks
- Callstack Navigation
- DLL Support

## How to use
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
