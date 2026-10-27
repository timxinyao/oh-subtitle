# 项目协作约定

- 本项目是 Windows 上的 .NET 6 WPF 应用；构建、测试和发布方法见 [README.md](README.md)。
- 完整行为测试会短暂移动鼠标并恢复位置；运行前通知用户，避免打断操作。
- `publish-self-contained.ps1` 会关闭本项目 `src/bin` 内的运行实例并清理 `src/bin`、`src/obj`；执行前保留需要的产物。
