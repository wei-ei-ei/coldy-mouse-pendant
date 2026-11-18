<p align="center">
  <br>
  <img src="assets/coldy-logo.png" alt="COLDY" width="320"/>
  <br>
  <img src="assets/coldy-avatar.png" alt="Coldy" width="120"/>
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

## v1.6.1

- 修复了形态下拉框无法选择选项的问题。
- 修复了颜色盘的自定义颜色无法保存的问题。
- 修复了「规定自定义颜色」按钮一直是灰色的问题。

## 安装

发布包可直接运行，无需编译环境。当前版本是 [v1.6.1](https://github.com/wei-ei-ei/coldy-mouse-pendant/releases/tag/v1.6.1)：[Coldy-1.6.1-win.zip](https://github.com/wei-ei-ei/coldy-mouse-pendant/releases/download/v1.6.1/Coldy-1.6.1-win.zip)。压缩包内是 `Coldy.exe`、`Coldy.exe.config`、`assets/coldy.png`、`assets/coldy-form2.gif`、`assets/coldy-form4.gif`、`assets/coldy-logo.png`。

1. 下载并解压发布包，保持目录结构不变。
2. 运行 `Coldy.exe`。
3. 挂件附着在系统光标上，同时打开设置窗口。

> [!NOTE]
> 从浏览器下载后，Windows SmartScreen 可能显示「Windows 已保护你的电脑」。`Coldy.exe` 没有代码签名，和多数未签名的小型开源 Release 一样。点击「更多信息」，再点击「仍要运行」。

登录时自动启动：按 `Win + R`，输入 `shell:startup`，将 `Coldy.exe` 的快捷方式放入该目录。

`Coldy.exe` 从自身所在目录加载 `assets\coldy.png`、`assets\coldy-form2.gif`、`assets\coldy-form4.gif` 和 `assets\coldy-logo.png`，并依据 `Coldy.exe.config` 选择 .NET Framework 4 运行时。`assets\coldy-form3.gif` 是保留的草稿，程序不再加载。

## 运行环境

构建目标为 AnyCPU，可在 32 位与 64 位 Windows 上运行。进程声明为系统 DPI 感知，挂件坐标与 `GetCursorPos` 使用同一套物理像素。

| 依赖 | 版本 | 说明 |
| --- | --- | --- |
| 操作系统 | Windows 7 SP1 及以上 | 覆盖 Windows 8、8.1、10、11 |
| 运行时 | .NET Framework 4.0 及以上 | 4.5 至 4.8 均可。Windows 10 1903 与 Windows 11 已内置 4.8 |
| 发布包 | v1.6.1 | 无安装程序 |

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

窗口是一块淡冰色的圆角面板，字号偏小，只保留控件本身。形态和光标的具体行为写在下面这张表里，窗口上不再重复说明。左上角是固定的 COLDY 涂鸦字标，不再显示人物预览，按住这一块可以拖动窗口；名字和名字字体都不可编辑。右上角是最小化和关闭。输入框、下拉框、按钮和颜色方块同一高度。

| 项 | 行为 |
| --- | --- |
| 绳子长度 | 0–1000 像素，当前值显示在滑块右侧。绳子越长关节越多，长绳也能平滑弯曲 |
| 绳子颜色 | 「选择颜色」打开颜色盘，红、绿、蓝各 0–255。按钮右侧的方块显示当前颜色 |
| 人物大小 | 按角色实际高度缩放，三个形态的雪人显示高度一致，悬挂点同步缩放 |
| 形态 | 「形态 1」是原来的静图。「形态 2」用同一张图播放蹲跳、眨眼和轻微摇摆。「cocococo」取《Co歌之王》官方 1440×2560 原片 41.2 秒处的唱歌正身：三角墨镜、绿色针织围巾领（右半段红白）、蓝白条纹长尾垂在左侧、金属勺在头顶；话筒按原片姿势贴在嘴边，彩虹粗管导线像手臂一样从话筒柄弯垂进身体右侧的插孔；嘴巴在墨镜正下方中央、随节拍一张一合；点头、身体同拍摆动、导线手臂轻晃，唱歌时右侧飘起两三个音符再淡出。原来的侧身形态 3 已移除，草稿留在 `assets/coldy-form3.gif`；唱歌形态的资源文件仍沿用 `assets/coldy-form4.gif` |
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
| 绳子长度 | `rope` | 0–1000 | 120 | 绳索静止长度，单位为像素。关节数随长度增长 |
| 绳子颜色 | `ropeColor` | `R,G,B`，各 0–255 | `55,55,62` | 绳索颜色。默认是当前的深灰色 |
| 人物大小 | `size` | 30–420 | 190 | 角色显示高度，单位为像素 |
| 形态 | `form` | `1`、`2` 或 `3` | `1` | `1` 为静图，`2` 为 `assets/coldy-form2.gif`，`3` 为「cocococo」`assets/coldy-form4.gif`。旧配置里的 `4` 仍按唱歌形态读取 |
| 光标 | `cursor` | `scheme` 或 `system` | `system` | `system` 使用系统指针，`scheme` 按场景切换 |
| 光标大小 | `cursorSize` | 16–128 | 32 | 指针边长，单位为像素 |
| 隐藏快捷键 | `hotkey`、`hotkeyVk`、`hotkeyVk2` | 两个互异虚拟键码 | `F8` | 同时按下时切换可见性 |
| 开机自启动 | `startup` | `0` 或 `1` | `0` | 同时写入当前用户的 `Run` 启动项，命令为 `Coldy.exe --tray` |
| 后台运行 | `background` | `0` 或 `1` | `0` | `1` 时设置窗口不进入任务栏，关闭窗口改为缩进托盘 |

`cursor` 为 `scheme` 时，普通区域是箭头，链接是手型，文本是工字，忙碌是沙漏，窗口边缘是两端带箭头的双向箭头。这些指针的笔画都是白色，外圈留一道细边，白底上仍然能看见。`system` 会禁用光标大小滑块，并恢复系统指针。旧配置里的 `arrow`、`hand`、`cross`、`ibeam`、`dot` 都会按 `scheme` 读取。

以下为编译期常量，设置窗口不可修改：

| 常量 | 值 | 说明 |
| --- | --- | --- |
| 绳段数 | 2–28，随绳长变化 | 约每 40 像素一个关节，质点数为段数加一。改变长度时会在当前形状上重新插值，不会突然绷直 |
| 重力 | 11000 | 每个模拟步施加的向下加速度系数 |
| 固定步长 | 1/240 秒 | Verlet 积分间隔 |
| 单帧时间上限 | 0.033 秒 | 限制掉帧后的追帧时长 |
| 单帧步数上限 | 8 | 同上 |
| 刷新间隔 | 约 8 毫秒 | 挂件重绘周期。上一帧所占区域会在下一帧被清掉，避免移动时留下残影 |
| 曲线张力 | 0.45 | `DrawCurve` 张力 |
| 热键轮询间隔 | 30 毫秒 | 读取两个虚拟键的按下状态 |
| 悬挂偏移 | 当前生效的指针尺寸 × 0.45 | 绳索上端相对指针热点的距离。`scheme` 用自定义大小，`system` 用系统指针大小 |

`coldy.ini` 在运行时生成，不纳入版本控制。与可执行文件放在同一目录时，绳长、绳子颜色、尺寸、形态、指针、热键、开机自启动和后台运行会被保留；名字固定为 `Coldy`，旧文件里的 `name`、`font`、`fontSize` 键会被忽略。开机自启动以注册表启动项为准。

## 开发

实现为 C# 5 / WinForms，目标运行时为 .NET Framework 4，无 NuGet 依赖。界面、绳索求解与绘制位于 `src/Program.cs`。`src/app.manifest` 声明系统 DPI 感知，以及 Windows 7 至 Windows 10 的兼容性。`src/app.config` 声明可使用 .NET Framework 4.0 及以上运行时。

挂件窗口使用 `WS_EX_LAYERED` 与 `WS_EX_TRANSPARENT`。每帧将绳索和角色绘制到预乘 alpha 的 32 位位图，再通过 `UpdateLayeredWindow` 提交。

形态 2 的动图由 `tools/MakeForm2Gif.cs` 从 `assets/coldy.png` 生成。原先的侧身形态 3 已从软件移除，`assets/coldy-form3.gif` 与 `tools/MakeForm3Gif.cs` 只作为草稿保留；唱歌形态现在叫「cocococo」，但资源文件名仍是 `assets/coldy-form4.gif`。

「cocococo」的静态基底由 `tools/build_form4_hd.py` 从《Co歌之王》官方 1440×2560 原片的 41.2 秒帧重建：这一帧里角色正面开唱，嘴巴圆点在墨镜正下方居中可见，话筒贴在嘴边、彩虹粗管导线像手臂一样弯垂进身体右侧的插孔，领口的绿白与红白两段针织和蓝白长尾全部露出。抠图用 rembg 的 isnet-general-use 模型（u2net 在这帧会把身体右侧咬掉一块），掩码缺口再用「毛绒色约束生长」从原帧恢复——只允许长进亮色低饱和的毛绒像素，暗色舞台背景进不来。唱歌动画由 `tools/animate_form4_singing.py` 生成：话筒保持原片姿势不动，只有一圈平滑高斯位移场把话筒球向右下推约 14 像素，让嘴巴圆点从球的上缘整个露出来（导线管像真实线缆一样平滑拉伸，末端插孔不动）；嘴巴是画在原片嘴点上的软边椭圆精灵，闭合时恰好盖住嘴点、随节拍张合成更大的椭圆。身体用 `cv2.remap` 做**一次**连续形变——旋转角沿 y 在头与身之间平滑过渡，叠加头部上下点头和导线手臂的局部摆动，每个像素只采样一次，因此颈部不会出现两幅画面叠加的重影。为还原雪堆般的毛茸茸质感，白色毛绒在提亮之外叠加了多倍频噪声凹凸；轮廓上的舞台痕迹——亮色逆光晕、暗色台面边缘、底部的台面反光带——全部用内侧毛绒颜色重涂，不腐蚀 alpha、也不加暗色描边，勺柄与彩虹管导线等细结构保持完整，身体轮廓直接收在亮色毛绒上；下摆轮廓的 alpha 也做了平滑，摆动时边缘不会泛起波浪。GIF 调色板由帧像素（另补一段灰阶）中值切分而来，并使用误差扩散抖动，围巾与领口的渐变不会打块。最后叠加方向性明暗和两三个上升淡出的音符，输出 40 帧循环 GIF。绳索的段数按 `round(长度/40)+2` 动态取 2–28，`Rope.Resize` 会在保留当前形状的前提下插值出新关节。

`tools/clean_2d_mascot.py` 剥掉白底 2D 立绘轮廓线外的白色贴纸边；`tools/make_logo.py` 从活动海报抠出彩色涂鸦字标；`tools/repair_form4.py` 是前一版基于手机录屏的实现，产物保留在 `assets/candidates/coldy-form4-screenrecord.gif`。这些脚本需要 Python 3.13、OpenCV、Pillow 与 rembg。原帖视频、完整身体高清精灵、2D 立绘与各版对比图都放在 `assets/candidates/`。

三个形态的缩放都按各自不透明区域的实际高度归一化，`size` 是唯一的大小入口，切换形态不会忽大忽小。绳索锚点按当前生效的指针尺寸计算：`scheme` 用「光标大小」，`system` 用 `SystemInformation.CursorSize`，所以把自定义指针调很大再切回系统默认时不会保留旧偏移。绳索为仓库内实现的 Verlet 求解器。段长由距离约束维持，因此绳索弯曲而不被拉长。角色转角取末段方向。积分使用固定时间步；指针高速移动时限制单帧步数；静止一段时间后冻结，避免静止抖动。悬挂点位于头顶绒毛，金属勺不与绳索重叠。

系统指针由程序绘制。选择样式后替换常用指针资源；选择「系统默认」或正常退出时，通过 `SystemParametersInfo(SPI_SETCURSORS)` 恢复。两个普通按键的组合无法用 `RegisterHotKey` 表达，因此热键改为 `GetAsyncKeyState` 轮询，并在上升沿触发。

在仓库根目录构建：

```bat
C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe /nologo /target:winexe /platform:anycpu /win32manifest:src\app.manifest /win32icon:assets\coldy.ico /r:System.Windows.Forms.dll /r:System.Drawing.dll /r:System.IO.Compression.dll /r:System.IO.Compression.FileSystem.dll /out:Coldy.exe src\Program.cs
copy /Y src\app.config Coldy.exe.config
```

可执行文件与配置文件输出到仓库根目录，以便加载同级 `assets`。`Coldy.exe` 与 `Coldy.exe.config` 不纳入版本控制。改动 `src/Program.cs` 后可直接双击根目录的 `build.bat` 重新编译；若挂件正在运行，先从托盘菜单「退出」，否则 `Coldy.exe` 被占用会写入失败。

```
src/Program.cs          挂件、绳索求解、设置窗口、指针
src/app.manifest        DPI 感知与操作系统兼容声明
src/app.config          .NET Framework 4 运行时声明
assets/coldy.png        运行时加载的透明精灵
assets/coldy-logo.png   README 页眉的彩色涂鸦字标
assets/coldy-avatar.png 文档页眉使用的全身形象
assets/coldy-form3.gif  旧侧身形态草稿，程序不再加载
assets/coldy-form4.gif  形态 3 唱歌动画：点头、晃身、手臂摆动、音符
tools/MakeForm3Gif.cs   旧侧身形态草稿的生成脚本
tools/repair_form4.py   从手机录屏重建唱歌形态（前一版）
tools/build_form4_hd.py 从官方 1440x2560 原片重建唱歌形态静态基底
tools/animate_form4_singing.py 用连续形变生成唱歌动画与音符
tools/clean_2d_mascot.py 去掉白底 2D 立绘的白色贴纸边
tools/make_logo.py      从活动海报抠出涂鸦字标
assets/candidates/      原帖视频、完整身体精灵、2D 立绘与各版备份
屏幕录制 *.mp4          唱歌形态的形象参考（手机录屏）
build.bat               一键重新编译 Coldy.exe
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
