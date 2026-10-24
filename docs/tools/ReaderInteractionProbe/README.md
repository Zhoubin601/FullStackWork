# ReaderInteractionProbe

Linux X11 原生交互验收工具，启动实际 App、DI 和 ReaderView，以 xdotool 产生真实鼠标/键盘事件。仅使用隔离数据目录、自建文本和本地 Mock，不配置凭据或发起真实API请求。测试八种面板组合、位置与选区保持、选区整段书摘、确认框、撤销、范围输入、明暗主题及窄窗口，并保存屏幕截图和desktop.json。

需要 .NET 8、独立X11显示（例如Xvfb）、xdotool、ImageMagick import，以及Avalonia的原生依赖和中文字体。在仓库根目录执行：

```sh
dotnet build docs/tools/ReaderInteractionProbe/ReaderInteractionProbe.csproj -c Release --locked-mode
DISPLAY=:97 dotnet docs/tools/ReaderInteractionProbe/bin/Release/net8.0/ReaderInteractionProbe.dll /tmp/new-reader-interaction-data /tmp/reader-interaction-evidence /path/to/xdotool
```

第一个目录必须不存在；运行数据只存该目录，第二个目录存JSON与截图。窗口在坐标0,0、渲染缩放1进行原生拖选；不能把此结果视为Windows或物理DPI验收。Linux无窗口管理器时不验证F11的系统全屏行为；该快捷键与平台实现仍沿用生产代码。
