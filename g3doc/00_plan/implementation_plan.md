# 修复 Android 编译由于命名空间冲突失败的问题 (XREAL SDK Duplicate Namespace)

## 背景问题分析

在 Unity 6 (6000.x) 中，由于使用了更高版本的 Android Gradle Plugin (AGP 8.0+) 进行打包，构建系统对包名 (namespace) 进行了更严格的校验。
编译日志显示：
```
Namespace 'nrsdk.pack' is used in multiple modules and/or libraries: :nr_loader:, :nr_common:. Please ensure that all modules and libraries have a unique namespace.
```
这表明 XREAL SDK 提供的两个 Android 插件（`nr_loader.aar` 和 `nr_common.aar`）在它们的 `AndroidManifest.xml` 中使用了完全相同的 `package="nrsdk.pack"`。这在最新的 Gradle 构建要求中是不允许的，导致编译中断。

## User Review Required

> [!WARNING]
> 因为产生报错的 AAR 位于 `com.xreal.xr.tar.gz` 压缩包解压后的 `Library/PackageCache/` 缓存目录中，我们无法直接永久修改缓存。为了根治此问题，我们需要将 SDK 提取为本地 Package，然后再修改 AAR。

请确认是否同意按照以下方案解压 `.tar.gz` 并在本地修改 AAR。

## Open Questions

无。

## Proposed Changes

我们将执行以下步骤：

### 1. 将 XREAL SDK 提取为本地 Package
将项目根目录的 `com.xreal.xr.tar.gz` 解压至 `Packages/com.xreal.xr/` 文件夹。

### 2. 更新依赖路径
#### [MODIFY] [manifest.json](file:///d:/UnityProject/XREALSDKTemplate/Packages/manifest.json)
将 `"com.xreal.xr": "file:../com.xreal.xr.tar.gz"` 更改为 `"com.xreal.xr": "file:com.xreal.xr"`，以强制 Unity 使用本地解压后的包。

### 3. 修改 AAR 文件的命名空间
进入 `Packages/com.xreal.xr/Runtime/Plugins/Android/`，对 `nr_loader.aar` 和 `nr_common.aar` 进行以下操作：
1. 使用 `tar` 工具解压 AAR 文件。
2. 编辑内部的 `AndroidManifest.xml`。
    - `nr_loader.aar`: 将 `package="nrsdk.pack"` 改为 `package="nrsdk.pack.loader"`。
    - `nr_common.aar`: 将 `package="nrsdk.pack"` 改为 `package="nrsdk.pack.common"`。
3. 重新使用 `jar` 或打包工具将其压回 AAR 格式，并清理临时解压的文件夹。

### 4. g3doc 文档归档
按照规范，在执行代码更改时，会将本实施方案同步到 `g3doc/00_plan/implementation_plan.md` 以及生成相关的变更文档。

## Verification Plan

### Automated Tests
无需单元测试。

### Manual Verification
1. 运行 Unity 打包流程（Build for Android），确认不再出现 `Manifest merger failed with multiple errors` 或命名空间冲突的报错。
2. 确认成功生成 `.apk` 或 `.aab` 文件。
