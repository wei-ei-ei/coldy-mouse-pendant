# COLDY · 光标系统设计文档（冰川 + 像素 + 织物与霜）

软件「光标」下拉现有四档：**系统默认 / Ice / Pixel / Frost's Descent（霜降）**。四档的尺寸统一由 `CursorFactory.Apply(scheme, size)` 一个接口管理，设置里的「光标大小」滑块是唯一尺寸来源。

- **Ice（冰川）**：原创矢量光标，运行时由 `src/Program.cs` 的 `CursorFactory` 按尺寸（16–128px）实时绘制并替换 14 个系统指针 ID。
- **Pixel（像素）**：作者 **线框xiankuang** 的动态像素光标（16×16 · 11 帧），以 `.ani` 形式随仓库分发，运行时按尺寸最近邻缩放重建后由 Windows 原生播放动画，覆盖 16 个系统指针 ID。
- **Frost's Descent（霜降）**：原创 IP 材质形状光标（第四套）。把 Coldy 的视觉基因抽象为材质＋母题（毛毡白 / 针织蓝白条纹 / sage绿·红·奶油围巾 / 镀铬勺 / 发光青点 / 彩虹线缆），**只做形状与纹样、不做人物**。运行时由 `CursorFactory.RenderFelt` 矢量绘制，动效态编码为 `.ani` 由 Windows 原生播放，覆盖 14 个系统指针 ID。

本目录存放三套方案的设计源与预览产物。

## 1. 冰川方案调研来源

| 项目 | 作者 | 许可 | 借鉴点 |
|---|---|---|---|
| [Bibata](https://github.com/ful1e5/Bibata_Cursor) | Abdulkaiz Khatri | MIT | Material 式紧凑形体；「实色主体 + 对比描边 + 外光晕」三层构造；Ice 配色变体思路 |
| [Posy's Improved Cursors](https://www.michieldb.nl/other/cursors/) | Michiel de Boer (Posy) | CC BY-NC 4.0 | 指针尖端与热点逐像素对齐；工字衬线改良；精确十字中心留空 |
| [Phinger Cursors](https://github.com/phisch/phinger-cursors) | phisch | CC BY-SA 4.0 | 24/32 网格 + 模块化部件；同一部件派生全部状态；任意尺寸像素完美 |
| [Capitaine Cursors](https://github.com/keeferrourke/capitaine-cursors) | Keefer Rourke | LGPLv3 | macOS 式圆润手型与衬线工字的轮廓处理 |
| [GoogleDot / Google Cursor](https://github.com/ful1e5/Google_Cursor) | ful1e5 | GPLv3 | 白色外描边保证任意背景可见；状态徽章（箭头+角标）组织方式 |

另参考 Dribbble 的 cursor 标签作品作为风格谱系观察，未取用素材。**冰川所有几何均为原创**，只借鉴构造原则，不复制任何参考项目的路径或位图。

## 2. 冰川设计主张

Coldy 是《Colder》MV 里的雪人：冰蓝发光眼、条纹围巾、彩虹线缆、头顶金属勺。光标提取其中元素做成「冰川」语言，整体偏**冰透的雪人情调**而非深海蓝：

- **冰蓝渐变**（发光眼）→ 指针族主体与雪花；
- **冰蓝 / 白 / 冰川蓝条纹**（呼应围巾尾）→ 手型袖口；
- **围巾红** → 仅用于禁止符斜杠（唯一暖色警示）；
- **极夜墨色**（闭合眼与暗部）→ 描边与符号族芯线。

### 三层构造（所有状态共用）

1. **白色光晕**：最外层，深色背景上保证可见；
2. **墨色描边**：中间层，浅色背景上保证可见；
3. **冰蓝渐变主体 / 墨色芯线**：指针族用渐变填充，符号族用墨色 strokes。

白底、深底、冰蓝底三种背景均通过对比测试（见对照表）。

### 几何规则

- 统一 **32 单位网格**，`u = size / 32`；
- 所有笔画 **圆帽圆角**（`LineCap.Round` / `LineJoin.Round`）；
- 描边宽度随尺寸线性缩放并设像素下限（墨线 ≥1.0px、光晕 ≥1.0px），16px 不糊；
- **热点与尖端逐像素对齐**：箭头尖端 (4, 2.6)，手型食指尖 (13, 2.8)，其余居中；
- 指针族带 16% 透明度软阴影，符号族不带；
- 缩放箭头为**单一连通多边形**，move 为 24 点一体四向多边形（避免交叉多边形被光晕切出白缝）。

## 3. 冰川色彩 Tokens

| Token | 值 | 用途 |
|---|---|---|
| Ice Hi | `#F8FEFF` | 渐变高光（0%），冰面白 |
| Ice Mid | `#D8F3FF` | 渐变中间调（45%），冰面蓝 |
| Ice Lo | `#9EDCF5` | 渐变深色（100%），浅冰川蓝，方向 65° |
| Ink | `#1F2838` | 描边、符号芯线 |
| Scarf Red | `#E2574B` | 仅禁止斜杠 |
| Cuff Ice | `#C5ECFF` | 袖口上条 |
| Halo / Cuff White | `#FFFFFF` | 外光晕、袖口中条 |

> 袖口三条自上而下：`#C5ECFF` / `#FFFFFF` / `#9EDCF5`。整体取**冰面般偏白**的冷色（而非深海蓝）：早期 `#AAECFF/#4FB6F5/#1C7CD4` 与围巾绿 `#7EA98C`、奶油 `#F5F0E6` 均已移除（奶油小尺寸显土黄，海蓝不够「雪人」）。

## 4. 冰川状态映射（14 个系统指针 ID）

| style | 中文 | Windows ID | 热点@32 | 构造 |
|---|---|---|---|---|
| arrow | 普通选择 | OCR_NORMAL 32512 | (4, 3) | 经典指针比例，渐变+阴影 |
| hand | 链接选择 | OCR_HAND 32649 | (13, 3) | 圆弧+贝塞尔手型，指缝线，冰蓝/白/冰川蓝条纹袖口 |
| ibeam | 文本选择 | OCR_IBEAM 32513 | (16, 16) | 喇叭衬线工字 + 水晶插入标记 |
| wait | 忙碌等待 | OCR_WAIT 32514 | (16, 16) | 六臂雪花（光晕/墨/冰渐变三层） |
| busy | 后台运行 | OCR_APPSTARTING 32650 | (3, 2) | 0.66× 箭头 + 雪花徽章 |
| help | 帮助选择 | OCR_HELP 32651 | (3, 2) | 0.66× 箭头 + 冰蓝「?」徽章 |
| cross | 精确选择 | OCR_CROSS 32515 | (16, 16) | 中心留空十字 + 水晶点 |
| no | 不可用 | OCR_NO 32648 | (16, 16) | 墨色圆环 + 围巾红斜杠 |
| we | 水平缩放 | OCR_SIZEWE 32644 | (16, 16) | 一体双向箭头（0°） |
| ns | 垂直缩放 | OCR_SIZENS 32645 | (16, 16) | 旋转 90° |
| nwse | 对角缩放 ↘ | OCR_SIZENWSE 32642 | (16, 16) | 旋转 45° |
| nesw | 对角缩放 ↙ | OCR_SIZENESW 32643 | (16, 16) | 旋转 135° |
| move | 移动 | OCR_SIZEALL 32646 | (16, 16) | 24 点一体四向 + 无光晕水晶中心 |
| up | 候选选择 | OCR_UP 32516 | (16, 3) | 竖直箭头，渐变+阴影 |

`ApplyScheme / Restore / SetSystemCursor / SPI_SETCURSORS` 架构与热区机制保持不变。

## 5. 像素光标方案（第三套）

### 来源与署名

- **作者：线框xiankuang** · [bilibili](https://space.bilibili.com/400839602) · [X](https://x.com/xiankuangxk) · [更多光标](https://xk-downloads.ovso.top/)
- 描述：「动态像素光标 … 由简易光标制作器制作」。
- 资产来自作者 installer exe 内嵌的 ZIP（`cursor/EmbeddedCursorPack.zip`），用 `tools/extract_pixel_pack.py` 切出 PK 签名段并解包，**全程只读、未运行该 exe**。包内 `install.inf` 给出注册表映射（方案名 "pixel cusor"），`preview.xml` 给出作者署名。
- **工字（Text）已重绘**：作者原 `Text.ani` 偏居画布左侧（热区 (3,6)）并带灰色阴影像素，打字时显偏移、阴影偏大；而纯直条重绘又缺乏像素味。现用 `tools/fix_pixel_ibeam.py` 重绘为**居中、左右对称、无阴影**的单帧像素工字：1px 墨描边 + 白填充，衬线角与肩台做成**像素阶梯**（与包内箭头同一套像素语言），热区居中 (8,8)。最近邻放大时像素颗粒随尺寸清晰变化，16–128px 均由 `ApplyPixel` 统一重建。其余 16 个光标保持作者原样。

### 格式（字节级实测）

- 17 个互不相同的 `.ani`（RIFF/ACON），各 11 帧、`jifRate=5`（≈12fps）、`flags=1`（icon 帧）、无 rate/seq chunk。
- 每帧 `fram/icon` chunk 是一个完整 mini `.cur`：16×16、**8bpp 调色板**（256 色）、XOR 256B + palette 1024B + AND 64B；BIH 高度为双倍（XOR+AND）。
- **热区存于 entry 的 planes/bit-count 字段**，逐文件不同（如 Normal=(0,0)、Link=(10,0)、Text=(6,12)@32），解析时逐帧读取，不臆造。

### 状态映射（16 个 ID + 1 个仅保留资产）

| 文件 | 中文 | Windows ID |
|---|---|---|
| Normal | 普通选择 | 32512 |
| Text | 文本选择 | 32513 |
| Busy | 忙碌等待 | 32514 |
| Precision | 精确选择 | 32515 |
| Alternate | 候选选择 | 32516 |
| Diagonal1 | 对角缩放 ↘ | 32642 |
| Diagonal2 | 对角缩放 ↙ | 32643 |
| Horizontal | 水平缩放 | 32644 |
| Vertical | 垂直缩放 | 32645 |
| Move | 移动 | 32646 |
| Unavailable | 不可用 | 32648 |
| Link | 链接选择 | 32649 |
| Working | 后台运行 | 32650 |
| Help | 帮助选择 | 32651 |
| Person | 人物选择 | 32652 |
| Pin | 固定选择 | 32653 |
| Handwriting | 手写 | —（NWPen 无 SetSystemCursor ID，仅保留资产不安装） |

### 运行时（`CursorFactory.ApplyPixel`）

1. `EnsurePixel` 一次性解析全部 `.ani`（`ParseAni` → `ParseFrame`，支持 8/32/4/1 bpp），缓存解码后的 RGBA 帧与热区。
2. 目标尺寸等于原生 16px 时直接用原文件；否则 `BuildScaledAni` 用**最近邻**把每帧缩放到目标尺寸（保持像素网格锐利）、按同一比例缩放热区、重编码为 32bpp mini `.cur`，保留原 `jif/flags/rate/seq` 重建 ANI，写入 `%TEMP%\ColdyPixel\<size>\` 并缓存复用。
3. **`LoadImage(LR_LOADFROMFILE, 显式 size)` + `SetSystemCursor`** 安装，动画由 Windows 原生播放。注意：不能用 `LoadCursorFromFile`——它会把 `.ani` 强制归一到系统光标尺寸（恒 32px），导致像素光标大小调不动；`LoadImage` 才尊重重建后的帧尺寸（实测 64→64×64、128→128×128，热区同步）。
4. `Restore`（切回系统默认/退出）调用 `SPI_SETCURSORS` 后清理临时目录。
5. 资产缺失时优雅回退到冰川方案。

预览页与对照表中的像素图为**动画首帧**（浏览器不支持 `.ani` 光标）；软件内为完整动画。`RenderPixelFirst` 供预览工具渲染首帧。

## 6. Frost's Descent（霜降）方案（第四套）

### 设计主张
- 创新不在"再画一个小人"，而在**材质系统＋IP 母题即功能几何**：每个光标是从 Coldy 世界裁出的物件/纹样，母题本身承担光标功能。
- 严格**形状/图案**：无人物、无脸、无身体。IP 一致性来自材质与母题（针织/毛毡/镀铬/冰晶/青点/彩虹线缆），而非角色造型；不引入原版没有的元素（如胡萝卜鼻）。
- 母题→功能：针织拉伸=缩放、镀铬勺=帮助/候选、雪球=移动/后台、围巾打结=禁止、冰晶=等待、发光青点=品牌签名、彩虹线缆=拖尾。

### 色彩 Tokens
| Token | Hex | 用途 |
|---|---|---|
| FeltWhite | #F5F7F8 | 毛毡白身体 |
| FeltShade | #E2E8EC | 毛毡暗部 |
| KnitBlue / KnitWhite | #5B87A6 / #F4F8FA | 针织蓝白条纹（围巾尾） |
| Sage / ScarfRed / ScarfCream | #9DB89A / #E2574B / #EFE7D8 | 围巾 wrap 三色肋 |
| ChromeHi / ChromeLo | #FAFCFD / #8C98A2 | 镀铬勺金属渐变 |
| Cyan | #35C6F4 | 发光青（眼）品牌签名 |
| Ink | #1F2838 | 极夜墨描边＋白光晕 |

### 状态映射（14 ID）与微动效
| 状态 | ID | 形状/母题 | 微动效 | 帧 |
|---|---|---|---|---|
| arrow | 32512 | 冰 shard 箭头＋针织尾嵌＋青点 | 静态 | 1 |
| hand | 32649 | 毛毡手套＋针织袖口 | 静态 | 1 |
| ibeam | 32513 | 针织 caret | 静态 | 1 |
| wait | 32514 | 六角冰晶＋镀铬勺心 | 旋转 | 12 |
| busy | 32650 | 毛毡雪球＋彩虹线缆 | 滚动 | 8 |
| help | 32651 | 镀铬勺＋雪花青点 | 浮动 | 8 |
| cross | 32515 | 冰准星＋青核 | 静态 | 1 |
| no | 32648 | 围巾交叉结 X | 摇动 | 8 |
| we/ns/nwse/nesw | 32644/45/42/43 | 针织拉伸条＋墨箭头 | 沿轴伸缩 | 8 |
| move | 32646 | 滚雪球＋彩虹拖尾 | 滚动 | 8 |
| up | 32516 | 镀铬勺立＋毛毡座 | 静态 | 1 |

### 运行时（`CursorFactory.ApplyFelt`）
1. 静态态：`RenderFelt` → `FromBitmap` → `SetSystemCursor`。
2. 动效态：`BuildFeltAni` 逐帧 `RenderFelt` → `CurFromBitmap`（32bpp mini `.cur`）→ 编码 ANI（jif=5、flags=1），写 `%TEMP%\ColdyFelt\<size>\` 缓存复用。
3. **`LoadImage(LR_LOADFROMFILE, size, size)` + `SetSystemCursor`**（与像素同一修复路径，尊重帧尺寸；实测 32/64/128 均正确、热区同步缩放）。
4. `Restore` 退出时清理 `ColdyFelt` 缓存。
预览由 `RenderFelt` 首帧渲染（单一事实来源）；浏览器预览为静态首帧，软件内为完整帧循环。

### 材质细节（对照概念板的"织物感"）
- `FeltBody`：软接触投影＋绒边 fringe＋三段毛毡渐变＋确定性绒点噪点（固定种子，动画帧间不闪）。
- `KnitStripes`：V 针脚行取代平带；`u<0.9`（小尺寸）LOD 回退平带保可读。
- `ScarfStripes`：罗纹加中心高光＋暗缝，呈针织肋立体感。
- `ChromeSpoon`：五段柱面金属渐变＋specular 条＋凹碗阴影＋碗部热点。
- `CyanGlow`：径向辉光＋径向核（`PathGradientBrush`），取代平涂圆盘。
- `Snowball`：顶光/底影弧＋毛毡绒感＋针织旋转标记。
- `RainbowTrail`：每股线缆暗缝＋色＋顶高光，呈圆股质感。

## 7. 文件清单

```
assets/cursors/
  DESIGN.md                  本文档
  preview.html               交互预览（冰川/像素/织物与霜切换，尺寸/背景/热区可调）
  coldy-cursors-sheet.png    冰川对照表（14 状态 × 16/32/64 × 浅/深/冰蓝）
  coldy-pixel-sheet.png      像素对照表（首帧，同布局，含作者署名）
  coldy-felt-sheet.png       织物与霜对照表（微动效首帧，同布局，含材质 tokens）
  png/<style>-32|64|128.png  冰川透明指针精灵
  png/pixel-<style>-16|32|64|128.png  像素首帧精灵
  png/felt-<style>-32|64|128.png     织物与霜首帧精灵
  png/hotspots.js            热区表（COLDY_HOTSPOTS + COLDY_PIXEL_HOTSPOTS + COLDY_FELT_HOTSPOTS）
  svg/<style>.svg            冰川矢量设计源（32 网格，与 C# 几何一致）
  pixel/*.ani + install.inf  像素光标包（17 个动态光标 + 作者映射）
tools/MakeCursorSheet.cs     引用 Coldy.exe，渲染冰川 PNG/对照表 + 像素首帧/对照表 + 织物与霜首帧/对照表
tools/make_cursor_svg.py     生成冰川 SVG 设计源（Python 3，无第三方依赖）
tools/extract_pixel_pack.py  从作者 installer 内嵌 ZIP 复现像素包提取（只读）
tools/fix_pixel_ibeam.py     重绘对称无阴影像素工字 Text.ani（纯标准库）
```

**单一事实来源**：冰川预览 PNG 由 `CursorFactory.Render`、织物与霜预览由 `CursorFactory.RenderFelt`（均为软件实际安装光标的同一份绘制代码）渲染；像素预览由 `CursorFactory.RenderPixelFirst` 解码同一份 `.ani`。不存在「预览好看、软件不一样」的漂移。改冰川几何时需同步 `src/Program.cs` 与 `tools/make_cursor_svg.py` 两处常量。

## 8. 再生成

```bat
:: 1. 重编译主程序（先从托盘退出正在运行的 Coldy）
build.bat

:: 2. 编译并运行预览工具（引用刚生成的 Coldy.exe；从仓库根运行以定位 pixel 包）
C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe /nologo /target:exe /r:Coldy.exe /r:System.Drawing.dll /r:System.Windows.Forms.dll /out:tools\MakeCursorSheet.exe tools\MakeCursorSheet.cs
tools\MakeCursorSheet.exe assets\cursors

:: 3. 重新生成冰川 SVG 设计源
python tools\make_cursor_svg.py

:: （可选）从作者 installer 复现像素包提取
python tools\extract_pixel_pack.py <installer.exe> assets\cursors\pixel
```

## 9. 迭代修复记录

**第一阶段（冰川重设计）**
1. 旧箭头拉伸不对称、尖端偏离热点 → 经典比例 + 尖端对齐。
2. 旧手型锯齿折线 → 圆弧/贝塞尔轮廓 + 指缝 + 袖口。
3. 旧沙漏两三角形 → 雪花；busy/help 不再退化为普通箭头。
4. 旧缩放箭头头杆分离白缝 → 一体多边形。
5. 渲染缺陷修复：`CrystalDot` 描边二次缩放、工字竖笔穿衬线交点、move 中心光晕蚀白圈。

**第二阶段（本轮验收反馈）**
6. 配色冰蓝化：`#AAECFF/#4FB6F5/#1C7CD4` → `#EAFBFF/#A5E8FF/#4FBEEA`，整体更冰透、贴合雪人情调。
7. 手型袖口去土黄：绿/奶油/红 → 冰蓝/白/冰川蓝条纹；围巾红仅保留给禁止斜杠。
8. 新增第三套像素方案：从作者 installer 提取 17 个动态 `.ani`，运行时最近邻缩放重建并保留动画，覆盖 16 个 ID（含 Person/Pin）。

**第三阶段（本轮验收反馈）**
9. 像素工字重绘：原 Text.ani 偏左 + 灰色阴影 → 居中对称、无阴影、带像素阶梯肩台的单帧工字，热区居中 (8,8)，像素颗粒随尺寸缩放清晰可见。
10. 配色冰面化：`#EAFBFF/#A5E8FF/#4FBEEA` → `#F8FEFF/#D8F3FF/#9EDCF5`，更白、更像冰面；袖口上条 `#8FDDFF` → `#C5ECFF`。
11. 第二档光标方案命名：「跟随场景」→「冰川随行」→ 最终定为 **Cold**（贴合角色与仓库 coldy）。
12. 尺寸统一接口：新增 `CursorFactory.Apply(scheme, size)` 作为三档光标唯一的尺寸/方案入口，`ApplyCursor` 与尺寸滑块统一经此路由（行为不变，仅收敛调用点）。
13. **像素尺寸真 bug 修复**：`ApplyPixel` 原用 `LoadCursorFromFile`，Windows 把 `.ani` 归一到系统 32px，像素光标大小始终不变；改用 `LoadImage(LR_LOADFROMFILE, size, size)` 后帧尺寸生效（64/128 实测正确），动画保留。
14. 像素工字改瘦：衬线 10→8 宽、竖笔 4→2 宽，保留像素阶梯与居中对称。

**第四阶段（获奖级创新 · 织物与霜）**
15. 新增第四套 **织物与霜（Felt & Frost）** 形状光标：否决"再画一个小人"的角色化方向，改为 IP 材质＋母题即功能几何（针织拉伸=缩放、镀铬勺=帮助、雪球=移动、围巾结=禁止、冰晶=等待、青点=签名、彩虹线缆=拖尾），无人物无脸、不引入原版没有的胡萝卜鼻。`CursorFactory.RenderFelt` 矢量绘制 14 态，动效态编码 `.ani`（等待 12 帧/其余 8帧 @12fps）经 `LoadImage` 尊重尺寸安装；下拉第四档「织物与霜」、ini `cursor=felt`、统一 `Apply(3,size)`；预览/对照表/色板/文档同步。
16. 三档重命名 Ice / Pixel / Frost's Descent；移除 cocococo 形态（资产归档不加载）。
17. 霜降材质精细化：绒边+噪点+软投影、针织 V 针脚、围巾罗纹高光、镀铬多段金属+specular、青点径向辉光、雪球绒感、线缆描边高光；小尺寸 LOD 保可读。