<p align="center">
  <br>
  <img src="assets/coldy-avatar.png" alt="Coldy" width="220"/>
  <br>
  <span>挂在鼠标上的 Coldy。绳子跟着光标弯曲，这个仓库只做这一个角色。</span>
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

## 安装

发布包解压后就能用，不需要安装开发工具，也不需要自己编译。

| 发布 | 文件 | 包含 |
| --- | --- | --- |
| [v1.0.0](https://github.com/wei-ei-ei/coldy-mouse-pendant/releases/tag/v1.0.0) | [Coldy-1.0.0-win.zip](https://github.com/wei-ei-ei/coldy-mouse-pendant/releases/download/v1.0.0/Coldy-1.0.0-win.zip) | `Coldy.exe`、`Coldy.exe.config`、`assets/coldy.png` |

把挂件加到电脑上：

1. 下载上面的压缩包，解压到一个不会随便删除的文件夹。
2. 双击 `Coldy.exe`。
3. 光标下面会出现 Coldy，旁边同时打开控制台。

想开机自动挂上：

1. 按 `Win + R`，输入 `shell:startup`，回车。
2. 把 `Coldy.exe` 的快捷方式放进这个启动文件夹。

压缩包里的三个文件要留在原来的相对位置。`Coldy.exe` 从自己所在的目录读取 `assets\coldy.png`，并按 `Coldy.exe.config` 选择 .NET Framework 4 运行库。

## 运行环境

程序按 AnyCPU 编译，32 位和 64 位 Windows 都能运行。界面坐标按系统 DPI 处理，高分屏上挂件仍对齐光标。

| 依赖 | 版本 | 说明 |
| --- | --- | --- |
| 系统 | Windows 7 SP1 及以上 | 包含 Windows 8、Windows 8.1、Windows 10、Windows 11 |
| 运行库 | .NET Framework 4.0 及以上 | 4.5、4.6、4.7、4.8 都可以。Windows 10 1903 和 Windows 11 已经自带 4.8 |
| 发布包 | v1.0.0 | 解压即用，不附带额外安装器 |

没有装过 .NET Framework 4 的旧系统，先安装 [4.8 运行库](https://dotnet.microsoft.com/download/dotnet-framework/net48)，再打开 `Coldy.exe`。

## 使用

### 启动后看到什么

- 光标下方的 Coldy，绳子会跟着鼠标甩动，停稳后垂直挂着。
- 控制台，标题是当前名字。
- 通知区域里的 Coldy 图标。

挂件窗口置顶、透明，并且点击穿透，鼠标操作会落到它后面的程序。

### 控制台

| 项目 | 怎么用 |
| --- | --- |
| 名字 | 改窗口标题和托盘提示。留空时按 Coldy 处理 |
| 绳子长度 | 向右拖更长。数值显示在滑条右边 |
| 人物大小 | 改变人物高度，挂点一起缩放 |
| 光标 | 箭头、手型、十字、工字、圆点，选中后立刻替换系统指针。系统默认会恢复原来的指针 |
| 光标大小 | 只对上面五种自绘指针有效 |
| 隐藏快捷键 | 记录两个一起按下的键 |
| 隐藏人物 | 立刻隐藏或显示，不需要按快捷键 |
| 退出 | 关闭挂件和控制台，并恢复系统光标 |

改动会马上生效，并写进 `Coldy.exe` 旁边的 `coldy.ini`。

### 隐藏快捷键

初次启动的隐藏键是 `F8`。改成两个键：

1. 点「隐藏快捷键」，按钮变成「请按两个键」。
2. 两个键可以一起按，也可以先后按。按钮会先显示第一个键，再显示完整组合，例如 `A + S`。
3. 记录过程中按 `Esc` 取消，沿用原来的组合。
4. 记好之后，只有这两个键都按住时才隐藏或显示。按一下切换一次，按住不放不会连续切换。

控制台没有焦点时快捷键仍然有效，人物隐藏之后也有效。

### 托盘

右键通知区域的图标：

| 菜单 | 作用 |
| --- | --- |
| 显示控制台 | 把控制台带到前面。双击图标也是这个作用 |
| 隐藏人物 / 显示人物 | 切换挂件 |
| 退出 | 结束程序，并恢复系统光标 |

### 退出

用控制台上的「退出」、托盘里的「退出」，或者直接关掉控制台。这三种都会把系统光标恢复回去。

不要在任务管理器里直接结束 `Coldy.exe`。那样会跳过恢复，自定义指针会留在系统里，直到重新打开本程序再正常退出。

## 运行参数

控制台里的范围就是滑条的范围。

| 参数 | 配置项 | 范围 | 初次启动 | 作用 |
| --- | --- | --- | --- | --- |
| 名字 | `name` | 任意文本 | `Coldy` | 窗口标题和托盘提示 |
| 绳子长度 | `rope` | 40–320 | 120 | 绳子静止长度，单位是像素 |
| 人物大小 | `size` | 80–420 | 190 | 人物显示高度，单位是像素 |
| 光标 | `cursor` | 见下表 | `arrow` | 替换后的系统指针 |
| 光标大小 | `cursorSize` | 16–128 | 32 | 指针边长，单位是像素 |
| 隐藏快捷键 | `hotkey`、`hotkeyVk`、`hotkeyVk2` | 两个不同的键 | `F8` | 同时按下时隐藏或显示 |

光标配置值：

| 界面 | 配置值 |
| --- | --- |
| 箭头 | `arrow` |
| 手型 | `hand` |
| 十字 | `cross` |
| 工字 | `ibeam` |
| 圆点 | `dot` |
| 系统默认 | `system` |

选「系统默认」时，光标大小滑条不起作用。

这些参数写在程序里，控制台不能改：

| 参数 | 值 | 作用 |
| --- | --- | --- |
| 绳段数 | 7 | 活动段数，绳点比段数多一个 |
| 重力 | 11000 | 每步加到绳子上的下拉力 |
| 模拟步长 | 1/240 秒 | 物理更新的固定间隔 |
| 单帧最长时间 | 0.033 秒 | 卡住之后不会一次补太多步 |
| 单帧最多步数 | 8 | 同上 |
| 画面刷新 | 约 16 毫秒 | 挂件重画间隔 |
| 绳子曲线张力 | 0.45 | 把绳点连成平滑曲线 |
| 快捷键轮询 | 30 毫秒 | 检查两个隐藏键是否都按下 |
| 悬挂偏移 | 光标大小的 0.45 倍 | 绳子上端离开指针热点的距离 |

`coldy.ini` 在运行时生成，不进入版本库。把这份文件和 exe 放在一起，名字、绳子、大小、光标和快捷键会保留。

## 开发

程序用 C# 5 和 WinForms 写成，目标运行库是 .NET Framework 4，没有 NuGet 包。界面、绳子和绘制都在 `src/Program.cs`。`src/app.manifest` 声明系统 DPI 感知，以及 Windows 7 到 Windows 10 的兼容性。`src/app.config` 声明接受 .NET Framework 4.0 及以上的运行库。

挂件是分层窗口。每帧把绳子和人物画进带透明通道的位图，再用 `UpdateLayeredWindow` 贴到屏幕上。

绳子是仓库里自己写的 Verlet 求解器。每一段的长度被约束住，所以绳子会弯曲而不是被拉长。人物转角取最后一段的方向。求解按固定时间步推进，鼠标移动过快时限制单帧步数，静止一会儿后停住，避免挂件原地微抖。绳子从脑袋顶上的绒毛引出来，勺子留在旁边。

光标由程序绘制。选中样式后替换系统里常用的几套指针；选择「系统默认」或正常退出时恢复。两个普通键的组合无法用系统全局热键表示，所以隐藏快捷键改为轮询按键状态。

在仓库根目录编译：

```bat
C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe /nologo /target:winexe /platform:anycpu /win32manifest:src\app.manifest /out:Coldy.exe src\Program.cs
copy /Y src\app.config Coldy.exe.config
```

编译结果和配置文件放在仓库根目录，旁边原有的 `assets` 就能被程序读到。`Coldy.exe` 和 `Coldy.exe.config` 不进入版本库。

```
src/Program.cs          挂件、绳子、控制台、光标
src/app.manifest        DPI 感知和系统兼容声明
src/app.config          .NET Framework 4 运行库声明
assets/coldy.png        程序使用的透明形象
assets/coldy-avatar.png 说明文档开头的头像
Coldy.exe               编译结果，不进入版本库
Coldy.exe.config        运行库配置，不进入版本库
coldy.ini               本机配置，不进入版本库
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
