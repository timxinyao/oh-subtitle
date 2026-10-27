# Oh-Subtitle
<img src="https://github.com/Surbowl/Oh-Subtitle/blob/master/images/icon.png?raw=true" width="50" />
看剧学外语时没有合适的工具遮挡字幕？试试这款简洁的小程序，轻松遮盖字幕，还能记笔记 (＾Ｕ＾)ノ

本项目 fork 自 [Surbowl/oh-subtitle](https://github.com/Surbowl/oh-subtitle)，遵循原项目的 [MIT 许可证](LICENSE)。

## Features
- 可随意拖拽改变遮罩范围
- 快捷记录笔记
- 四种主题颜色可选
- 支持半透明窗体，可通过快捷键切换
- 学习模式下，鼠标移到遮罩中间的五分之三区域可查看字幕；按住左右各五分之一区域可拖动遮罩，右侧控制按钮除外

按 `Ctrl+Shift+Q` 或点击右侧的毕业帽按钮切换学习模式。

## 本机验证
- 2026-10-27：Windows 实机确认学习模式遮罩和侧边鼠标指针可见。左右各五分之一区域的边界已通过自动测试；该比例的拖动、穿透及任务栏最小化尚未重新实机验证。

更多信息可浏览上游早期的[介绍文章](https://zhuanlan.zhihu.com/p/256343803)，其中内容对应历史版本。

## 安装包
以下链接来自上游发布，可能不包含本 fork 的修改；可按下方说明自行构建。
下载链接：[百度网盘](https://pan.baidu.com/share/init?surl=upPT-eCSczfRLKlIoZF-Jg)
提码：ydu7
（如果链接失效请提交 Issues）

## 本地构建和测试

需要 Windows 和 .NET 6 SDK。在项目根目录执行：

```powershell
dotnet build src\OhSubtitle.sln -c Release
dotnet run --project tests\OhSubtitle.BehaviorTests\OhSubtitle.BehaviorTests.csproj -c Release
dotnet publish src\OhSubtitle.csproj -c Release -r win-x64 --self-contained true
```

完整行为测试会短暂移动鼠标，结束后恢复位置。发布后的程序位于 `src\bin\Release\net6.0-windows\win-x64\publish\OhSubtitle.exe`。

如果使用 `publish-self-contained.ps1`，脚本会关闭本项目 `src\bin` 内的运行实例，并清理 `src\bin` 和 `src\obj`；请先保存其中需要保留的产物。

## 上游版本记录
- [v1.2.0](https://github.com/Surbowl/oh-subtitle/releases/tag/v1.2.0)
  - **移除翻译与字典功能**
  - 调整按钮布局
  - 鼠标离开悬浮窗后，自动隐藏所有操作按钮
- [v1.1.3](https://github.com/Surbowl/oh-subtitle/releases/tag/v1.1.3)
  - 由于 google.cn 停止服务，替换翻译 API，解决中国大陆地区不能翻译的问题
  - CSV 笔记本模块优化，支持英文逗号与双引号
  - 框架由 dotnet 3.1 升级至 dotnet 6.0
- [v1.1.2](https://github.com/Surbowl/oh-subtitle/releases/tag/v1.1.2)
  - 修复窗体拖动到屏幕边缘时自动全屏的 BUG
  - 翻译结果支持复制
- [v1.1.1](https://github.com/Surbowl/oh-subtitle/releases/tag/v1.1.1)
  - 修复无法识别日语并翻译为中文的 BUG
- [v1.1.0](https://github.com/Surbowl/oh-subtitle/releases/tag/v1.1.0)
  - 添加对日语的支持
  - 添加笔记本功能
  - 启动时自动还原上次的位置、大小、主题色和语言模式
- [v1.0.5](https://github.com/Surbowl/oh-subtitle/releases/tag/v1.0.5)
  - 新增右键菜单，可选亮白、亮灰、暗灰、暗黑四种主题色
- [v1.0.4](https://github.com/Surbowl/oh-subtitle/releases/tag/v1.0.4)
  - 添加切换窗体透明度的快捷键 Ctrl+Q
- [v1.0.3](https://github.com/Surbowl/oh-subtitle/releases/tag/v1.0.3)
  - 增强单词查询功能，输入单个单词时显示更全面的单词释义
  - 解决腾讯视频、优酷视频全屏时无法置顶的问题
  - 扩大透明度切换按钮（眼睛按钮）的鼠标触发范围
  - 隐藏任务栏按钮
- [v1.0.2](https://github.com/Surbowl/oh-subtitle/releases/tag/v1.0.2)
  - 新增透明度切换按钮（眼睛按钮），鼠标悬浮在按钮上方可使窗体透明化
  - 降低窗体默认高度 80 -> 65

## Demonstration
上游历史版本演示：[《分享一款带翻译功能的挡字幕小工具，方便看剧学英语、日语》](https://zhuanlan.zhihu.com/p/256343803)
![sample](https://github.com/Surbowl/Oh-Subtitle/blob/master/images/sample.gif?raw=true)
