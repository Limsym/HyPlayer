# HyPlayer Music Atlas

基于 HyPlayer `2.1.38.6029`（`1fdd8073efaa2cde557d73018f777a698e7a972b`）的非官方集成版本。本轮版本 `2.1.38.6043`，Windows x64。

这是包含音乐地图的完整播放器更新包，不是可以直接加载到任意 HyPlayer 版本的独立插件。播放、登录及会员曲目权限继续由 HyPlayer 处理。

## 功能

- 歌单页的“音乐地图”打开完整歌单，先显示本机缓存，再检查更新。
- 曲风、气质、来源三维度；支持同维度 OR、跨维度 AND 的组合筛选。
- 网易云曲风保留细分原文；地图按大类计票，编辑器可搜索细类和创建自定义标签。
- 气质接入 15 个网易云标签：放松、治愈、悲伤、平静、活力、思念、浪漫、孤独、欢快、苦情、狂欢、热血励志、自信、抑郁、甜蜜；共享人工校正逐曲增删气质，补充已有的“热烈 / 昂扬”和“神圣 / 仪式”。
- 来源使用本机歌名/专辑/歌手规则初判；手动修改只覆盖同维度。
- 每首歌曲在一个维度的每个不同大类各投一票；折叠票数进入“其它”，“未分类”独立，待查询/查询失败不伪装为无标签。
- 推荐按分类票数累计 90% 展开，有歌曲的类别不超过 10 个时全部展开。
- 大封面播放页与底部播放栏“…”菜单中的“曲目分类”打开小浮窗，与地图共享分类。切歌不改变已打开浮窗绑定的歌曲。

## 安装到另一台电脑

下载并解压完整发布 ZIP，阅读其中 `INSTALL.txt`，运行 `Install-MusicAtlas.ps1`。包内包括 x64 依赖、公开签名证书，以及经作者明确授权公开的全部人工分类修正 `classifications.json`。它们是真实使用中的修正，不是演示样例；不包含登录信息、账号凭据或签名私钥。

仅当现有包名为 `48848aaaaaaccd.HyPlayer`、发布者为 `CN=HyPlayer Team`，且版本较低时，安装脚本执行原位更新。它不会卸载原应用。高版本或其它包身份不自动覆盖。

安装脚本支持 `-CheckOnly`，先核对包、依赖和证书状态。首次信任此本地构建证书，需要在管理员 PowerShell 中显式使用 `-TrustCertificate`，导入 LocalMachine\TrustedPeople，不导入根证书存储。签名证书并非上游官方发行证书。

其它电脑需要使用该电脑自己的网易云登录。发布包不带歌单快照，第一次打开大歌单需要联网获取详情和标签；之后使用缓存。

安装成功后，若目标设备没有 `music-atlas-state-v4.json`，安装脚本会自动载入随包分类，音乐地图打开后即可使用。已有分类配置不会被覆盖；需要主动采用发布的整套修正时，先关闭 HyPlayer，再运行：

```powershell
powershell -ExecutionPolicy Bypass -File .\Transfer-AtlasSettings.ps1 -Mode Import -Path .\classifications.json
```

显式导入会先备份目标设备已有配置。`-CheckOnly` 只检查文件与安装条件，不写入分类或修改证书。源码中的对应文件为 `MusicAtlasData/classifications.json`，仅保存歌曲 ID、类别目录、各维度人工修正及地图展开配置，不包含歌单快照、歌名清单、登录 Cookie 或音源。

类别编辑采用一个标签池：高亮表示当前生效，★ 表示网易云识别，二者互不混淆；曲风细分在行旁说明。地图类别名称字号不小于曲目数。

## 共享气质校正（6043）

优先级为：本机同维度手动修改（包括清空）＞共享逐曲校正＞网易云原始标签。共享表位于 [`MusicAtlasData/mood-curation.json`](MusicAtlasData/mood-curation.json)，在构建时嵌入地图资源，离线生效。升级后即可采用这些规则，无须导入或覆盖已有个人配置。

首批涉及 55 首具体录音：为 44 首补充“热烈 / 昂扬”、10 首补充“神圣 / 仪式”（3 首重叠），校正另外 4 首明显误标。包括移除 Creep 的甜蜜、欢快、放松，保留思念和悲伤并补充孤独。类别没有合并，热血励志与活力仍独立。

每条规则都有歌曲 ID、增删标签和理由，不按艺人或标题批量套用，也不改写网易云缓存。编辑器仍以 ★ 表示原始识别，以高亮表示实际生效；“人工校正”说明的悬浮提示可查看理由。恢复自动标签会回到包含共享校正的默认结果。

这是一轮基于已知作品与录音元数据的人工审阅，未进行整库逐曲试听或音频分析。[完整改动及待审清单](MusicAtlasData/mood-curation-review.md) 保留判断依据；有争议的曲目暂不应用。发布包还包含最新 53 首个人修正的快照，仍只在新设备没有个人配置时导入。

## 数据与回退

分类修改按歌曲 ID 保存在应用 LocalState 的 `music-atlas-state-v4.json`，地图浏览器存储保留兼容副本；曲风和情绪缓存为 `music-atlas-genres-v1.json`。歌单元数据缓存与上述文件分开，不保存音源 URL 或登录 Cookie。

使用发布包内的 `Transfer-AtlasSettings.ps1 -Mode Export -Path <文件路径>` 可导出之后新增的分类配置，另一台电脑关闭 HyPlayer 后运行 `-Mode Import` 导入。分类配置是个人数据，本次随包数据已获得作者明确公开授权；其它导出文件是否公开，由数据所有者自行决定。导入会先备份目标设备已有配置。

升级前保留自己的旧安装包和分类导出文件。需要回退时关闭播放器，使用可信旧包执行 `Add-AppxPackage -Path <旧包> -ForceUpdateFromAnyVersion -ForceApplicationShutdown`；不要先卸载。旧版地图不认识新标签结构，回退 UI 前同时保存当前配置。发行包不提供自动降级，也不会替你恢复旧版登录设置。

## 从源码构建

可直接使用随版本发布的完整源码 ZIP（含构建使用的子模块源码），或从上游 clone 并检出上述基线后应用源码改动。

构建环境：Windows、Visual Studio Build Tools，MSBuild，UWP 构建工具及 .NET Native 2.2、Windows SDK 10.0.26100.0、Node.js。本分支是旧 UWP/.NET Native 项目，不能用现代主分支的 .NET 10 命令替代。

```powershell
git submodule update --init --recursive
powershell -ExecutionPolicy Bypass -File .\Build-MusicAtlas.ps1
```

完整源码 ZIP 已包含子模块；使用 ZIP 时不用执行 git submodule。构建脚本针对完整 `HyPlayer.sln`、Release/x64 和 C# 13，输出未签名 MSIX 到 `Artifacts`。签名必须使用自己的代码签名证书（Subject 与包 Publisher 一致），再用 Windows SDK `makeappx bundle` 和 `signtool sign` 生成安装 bundle。私钥不在源码包中。

界面修改：编辑 `MusicAtlasBuild`，运行 `node MusicAtlasBuild/build-hyplayer-atlas.cjs`。它生成 `HyPlayer/Assets/MusicAtlas/index.html`；已安装播放器不能直接从工作目录热加载。仅界面更新可复用同版本本机二进制，替换资源、递增清单版本后重新打包签名，无需重编译原生代码。

```powershell
cd MusicAtlasBuild
npm install
npx playwright install chromium
npm test
```

测试使用虚构歌曲及公开校正表中的固定 ID，不依赖私人歌单或账户。原生桥接和实际安装仍需在 Windows 上验证。

## GitHub fork / 上游贡献

## 下一阶段待办：Android 客户端

记录日期：2026-11-09。状态：已规划，尚未启动开发。

目标是在 Android 版 HyPlayer 内完成音乐地图、网易云登录、会员歌曲播放和分类连续播放，不采用跳转网易云客户端播放的伴侣网页方案。

- [ ] 搭建 Android 应用与构建、签名流程，复用音乐地图前端和逐曲校正规则。
- [ ] 移植并验证网易云登录、账号状态保存及会员音源访问。
- [ ] 接入原生播放、分类队列、上一首/下一首与组合筛选。
- [ ] 保留曲风、气质、来源三维度、手动修改优先和分类数据导入/导出。
- [ ] 完成锁屏与后台播放、通知栏及耳机控制、网络中断恢复。
- [ ] 与用户共同进行 Android 真机测试；用户可提供自己的手机参与测试。

初步估计：可上手机测试的完整闭环需 3–7 个工作日，较稳定的日常使用版需 2–4 周；地图适配约半天到一天。主要不确定性是登录与会员音源接口、后台播放和机型兼容。以上为开发及联调估计，不是交付承诺，启动后按实际验证结果调整。

## GitHub fork / 上游贡献说明

发布 ZIP、源代码 ZIP 和 SHA256 清单适合上传到自己的 fork 的 GitHub Release。先创建 fork，再基于上述旧版 commit 创建分支，复制本版本的改动文件；不要上传 `Artifacts`、私人备份、账户缓存或证书私钥。

音乐地图的主要修改位于 `Pages/MusicAtlas*`、`Assets/MusicAtlas`、歌单按钮、两处播放菜单及 `MusicAtlasBuild`。清单版本提升和本地发布签名是分发事项；向上游提交时应单独审查。上游当前主分支的结构与此旧版本不同，不能把此包描述为已兼容主分支或已被上游接受。

保留原项目 `LICENCE` 和第三方许可。提供二进制时同时提供对应的完整修改源码。本版本未改变歌曲版权或会员播放权限。
