<p align="center">
  <br>
  <img src="assets/coldy-avatar.png" alt="Coldy" width="180"/>
  <br>
  <span>Coldy 的 Windows 鼠标挂件。角色悬挂于光标下方，绳索随指针运动弯曲。</span>
  <br>
</p>

<p align="center">
  <a href="#安装">安装</a>
  &nbsp;&nbsp;&nbsp;•&nbsp;&nbsp;&nbsp;
  <a href="#使用">使用</a>
  &nbsp;&nbsp;&nbsp;•&nbsp;&nbsp;&nbsp;
  <a href="#开发">开发</a>
  &nbsp;&nbsp;&nbsp;•&nbsp;&nbsp;&nbsp;
  <a href="#贡献">贡献</a>
</p>

<p align="center">
  <a href="https://github.com/wei-ei-ei/coldy-mouse-pendant/releases/latest"><img alt="release" src="https://img.shields.io/github/v/release/wei-ei-ei/coldy-mouse-pendant?label=release"></a>
  &nbsp;
  <img alt=".NET Framework 4.0 or newer" src="https://img.shields.io/badge/.NET%20Framework-4.0%2B-512BD4">
  &nbsp;
  <img alt="Windows" src="https://img.shields.io/badge/platform-Windows-0078D6">
</p>

本仓库只包含 Coldy 这一个角色。形象来自《Colder》MV 中的雪人：圆身、条纹围巾、头顶金属勺、右侧彩色线缆，以及一只闭合的眼睛和一只蓝色发光眼睛。

## 安装

发布包可直接运行，无需编译环境。当前版本是 [v1.5.0](https://github.com/wei-ei-ei/coldy-mouse-pendant/releases/tag/v1.5.0)：[Coldy-1.5.0-win.zip](https://github.com/wei-ei-ei/coldy-mouse-pendant/releases/download/v1.5.0/Coldy-1.5.0-win.zip)。压缩包内是 `Coldy.exe`、`Coldy.exe.config`、`assets/coldy.png`、`assets/coldy-form2.gif`。

1. 下载并解压发布包，保持目录结构不变。
2. 运行 `Coldy.exe`。
3. 挂件附着在系统光标上，同时打开设置窗口。

> [!NOTE]
> 从浏览器下载后，Windows SmartScreen 可能显示「Windows 已保护你的电脑」。`Coldy.exe` 没有代码签名，和多数未签名的小型开源 Release 一样。点击「更多信息」，再点击「仍要运行」。

登录时自动启动：按 `Win + R`，输入 `shell:startup`，将 `Coldy.exe` 的快捷方式放入该目录。

`Coldy.exe` 从自身所在目录加载 `assets\coldy.png` 和 `assets\coldy-form2.gif`，并依据 `Coldy.exe.config` 选择 .NET Framework 4 运行时。

## 运行环境

构建目标为 AnyCPU，可在 32 位与 64 位 Windows 上运行。进程声明为系统 DPI 感知，挂件坐标与 `GetCursorPos` 使用同一套物理像素。

| 依赖 | 版本 | 说明 |
| --- | --- | --- |
| 操作系统 | Windows 7 SP1 及以上 | 覆盖 Windows 8、8.1、10、11 |
| 运行时 | .NET Framework 4.0 及以上 | 4.5 至 4.8 均可。Windows 10 1903 与 Windows 11 已内置 4.8 |
| 发布包 | v1.5.0 | 无安装程序 |

未安装 .NET Framework 4 的系统，请先安装 [.NET Framework 4.8](https://dotnet.microsoft.com/download/dotnet-framework/net48)。

## 使用

启动后包含三部分：

- 跟随光标的挂件。绳索在指针移动时摆动，静止后保持下垂。
- 设置窗口。标题为当前角色名。
- 系统托盘图标，任务栏使用同一枚缩小的 Coldy。
- 可选开机自启动，可选后台运行。
- 发现新的 GitHub Release 时询问是否更新。

挂件为置顶的分层窗口，启用点击穿透，输入会传递给下层应用。

### 设置窗口

窗口是一块淡冰色的圆角面板，字号偏小，只保留控件本身。形态和光标的具体行为写在下面这张表里，窗口上不再重复说明。上方是角色和当前名字，按住这一块可以拖动窗口。右上角是最小化和关闭。输入框、下拉框、按钮和颜色方块同一高度，名字在输入框内垂直居中。

| 项 | 行为 |
| --- | --- |
| 名字 | 更新窗口标题与托盘提示。空值回退为 `Coldy` |
| 绳子长度 | 调整绳索静止长度，当前值显示在滑块右侧 |
| 绳子颜色 | 「选择颜色」打开颜色盘，红、绿、蓝各 0–255。按钮右侧的方块显示当前颜色 |
| 人物大小 | 按显示高度缩放角色，悬挂点同步缩放 |
| 形态 | 「形态 1」是原来的静图。「形态 2」用同一张图播放蹲跳、眨眼和轻微摇摆 |
| 光标 | 默认是「系统默认」，使用系统指针。「跟随场景」按所在控件切换箭头、手型、工字、等待和缩放，笔画为白色，外圈留有细边。拖动窗口边缘时为对应方向的双向箭头 |
| 光标大小 | 只在「跟随场景」下生效，整套指针使用同一边长 |
| 隐藏快捷键 | 录制由两个按键组成的切换热键。点击后同时或依次按下两个键，`Esc` 取消。录制完成后，同时按下这两个键隐藏或显示人物 |
| 隐藏人物 | 立即切换挂件可见性 |
| 开机自启动 | 写入当前用户的启动项。开机后只出现在托盘，不弹出设置窗口 |
| 后台运行 | 设置窗口不出现在任务栏。关闭或最小化都会缩进托盘，挂件继续运行。勾选之后，只能从托盘菜单的「退出」结束程序 |
| 退出 | 只在托盘菜单里。结束进程并恢复系统光标 |

修改立即生效，并写入可执行文件同目录的 `coldy.ini`。

### 显示切换热键

默认热键为 `F8`。重新录制：

1. 点击「隐藏快捷键」。按钮文字变为「请按两个键」。
2. 两个按键可同时按下，也可依次按下。界面先显示第一键，完成后显示组合，例如 `A + S`。
3. 录制过程中按 `Esc` 取消，保留原热键。
4. 两个键同时处于按下状态时触发一次切换。按住不重复触发。

热键为全局热键：设置窗口失焦、挂件隐藏时仍然有效。

### 系统托盘

托盘图标和任务栏按钮都是缩小后的 Coldy。双击托盘图标打开设置窗口。

| 菜单 | 行为 |
| --- | --- |
| 显示控制台 | 激活设置窗口。双击托盘图标等效 |
| 隐藏人物 / 显示人物 | 切换挂件可见性 |
| 退出 | 结束进程并恢复系统光标 |

### 退出

「退出」只在托盘菜单里，会结束进程并恢复系统光标。未勾选「后台运行」时，右上角关闭同样会退出。勾选之后，关闭和最小化只把窗口收进托盘，设置窗口里不能再结束程序。

再次启动已经在运行的程序时，会打开现有的设置窗口，而不会再开一个进程。

请勿从任务管理器结束 `Coldy.exe`。强制结束会跳过光标恢复，自定义指针会保留到本程序再次正常退出。

### 更新

程序启动后，以及之后大约每 10 分钟，会查看 GitHub 上的最新 Release。只有发布了比当前版本更新、且带有 `Coldy-*-win.zip` 的 Release 时才会询问。选择更新后，程序下载压缩包，退出，替换同目录下的文件，然后重新打开。选择稍后则继续使用当前版本。仓库需要保持公开，这样未登录的程序才能读到 Release。

## 运行参数

滑块范围即下表中的取值范围。

| 参数 | 配置键 | 范围 | 默认值 | 说明 |
| --- | --- | --- | --- | --- |
| 名字 | `name` | 字符串 | `Coldy` | 窗口标题与托盘提示 |
| 绳子长度 | `rope` | 40–320 | 120 | 绳索静止长度，单位为像素 |
| 绳子颜色 | `ropeColor` | `R,G,B`，各 0–255 | `55,55,62` | 绳索颜色。默认是当前的深灰色 |
| 人物大小 | `size` | 80–420 | 190 | 角色显示高度，单位为像素 |
| 形态 | `form` | `1` 或 `2` | `1` | `1` 为静图，`2` 为 `assets/coldy-form2.gif` |
| 光标 | `cursor` | `scheme` 或 `system` | `system` | `system` 使用系统指针，`scheme` 按场景切换 |
| 光标大小 | `cursorSize` | 16–128 | 32 | 指针边长，单位为像素 |
| 隐藏快捷键 | `hotkey`、`hotkeyVk`、`hotkeyVk2` | 两个互异虚拟键码 | `F8` | 同时按下时切换可见性 |
| 开机自启动 | `startup` | `0` 或 `1` | `0` | 同时写入当前用户的 `Run` 启动项，命令为 `Coldy.exe --tray` |
| 后台运行 | `background` | `0` 或 `1` | `0` | `1` 时设置窗口不进入任务栏，关闭窗口改为缩进托盘 |

`cursor` 为 `scheme` 时，普通区域是箭头，链接是手型，文本是工字，忙碌是沙漏，窗口边缘是两端带箭头的双向箭头。这些指针的笔画都是白色，外圈留一道细边，白底上仍然能看见。`system` 会禁用光标大小滑块，并恢复系统指针。旧配置里的 `arrow`、`hand`、`cross`、`ibeam`、`dot` 都会按 `scheme` 读取。

以下为编译期常量，设置窗口不可修改：

| 常量 | 值 | 说明 |
| --- | --- | --- |
| 绳段数 | 7 | 距离约束的段数，质点数为段数加一 |
| 重力 | 11000 | 每个模拟步施加的向下加速度系数 |
| 固定步长 | 1/240 秒 | Verlet 积分间隔 |
| 单帧时间上限 | 0.033 秒 | 限制掉帧后的追帧时长 |
| 单帧步数上限 | 8 | 同上 |
| 刷新间隔 | 约 8 毫秒 | 挂件重绘周期。上一帧所占区域会在下一帧被清掉，避免移动时留下残影 |
| 曲线张力 | 0.45 | `DrawCurve` 张力 |
| 热键轮询间隔 | 30 毫秒 | 读取两个虚拟键的按下状态 |
| 悬挂偏移 | 指针尺寸 × 0.45 | 绳索上端相对指针热点的距离 |

`coldy.ini` 在运行时生成，不纳入版本控制。与可执行文件放在同一目录时，名称、绳长、绳子颜色、尺寸、形态、指针、热键、开机自启动和后台运行会被保留。开机自启动以注册表启动项为准。

## 开发

实现为 C# 5 / WinForms，目标运行时为 .NET Framework 4，无 NuGet 依赖。界面、绳索求解与绘制位于 `src/Program.cs`。`src/app.manifest` 声明系统 DPI 感知，以及 Windows 7 至 Windows 10 的兼容性。`src/app.config` 声明可使用 .NET Framework 4.0 及以上运行时。

挂件窗口使用 `WS_EX_LAYERED` 与 `WS_EX_TRANSPARENT`。每帧将绳索和角色绘制到预乘 alpha 的 32 位位图，再通过 `UpdateLayeredWindow` 提交。

形态 2 的动图由 `tools/MakeForm2Gif.cs` 从 `assets/coldy.png` 生成，不替换静图。绳索为仓库内实现的 Verlet 求解器。段长由距离约束维持，因此绳索弯曲而不被拉长。角色转角取末段方向。积分使用固定时间步；指针高速移动时限制单帧步数；静止一段时间后冻结，避免静止抖动。悬挂点位于头顶绒毛，金属勺不与绳索重叠。

系统指针由程序绘制。选择样式后替换常用指针资源；选择「系统默认」或正常退出时，通过 `SystemParametersInfo(SPI_SETCURSORS)` 恢复。两个普通按键的组合无法用 `RegisterHotKey` 表达，因此热键改为 `GetAsyncKeyState` 轮询，并在上升沿触发。

在仓库根目录构建：

```bat
C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe /nologo /target:winexe /platform:anycpu /win32manifest:src\app.manifest /win32icon:assets\coldy.ico /r:System.Windows.Forms.dll /r:System.Drawing.dll /r:System.IO.Compression.dll /r:System.IO.Compression.FileSystem.dll /out:Coldy.exe src\Program.cs
copy /Y src\app.config Coldy.exe.config
```

可执行文件与配置文件输出到仓库根目录，以便加载同级 `assets`。`Coldy.exe` 与 `Coldy.exe.config` 不纳入版本控制。

```
src/Program.cs          挂件、绳索求解、设置窗口、指针
src/app.manifest        DPI 感知与操作系统兼容声明
src/app.config          .NET Framework 4 运行时声明
assets/coldy.png        运行时加载的透明精灵
assets/coldy-avatar.png 文档页眉使用的全身形象
Coldy.exe               构建产物，不纳入版本控制
Coldy.exe.config        运行时配置，不纳入版本控制
coldy.ini               本地配置，不纳入版本控制
```

## 贡献

<a href="https://github.com/wei-ei-ei">
  <img src="https://github.com/wei-ei-ei.png" width="80" height="80" alt="wei-ei-ei"/>
</a>

<br>

[wei-ei-ei](https://github.com/wei-ei-ei)

<br>

<a href="https://github.com/wei-ei-ei/coldy-mouse-pendant/graphs/contributors">
  <img src="https://contrib.rocks/image?repo=wei-ei-ei/coldy-mouse-pendant" alt="contributors"/>
</a>
