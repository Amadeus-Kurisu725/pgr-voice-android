# 提示窗真实设备夹具

此目录是独立测试构建，不进入产品 APK。产品 `PgrVoice.Android.csproj` 已排除 `Tests/**/*.cs`；夹具另设包名 `cn.pgrvoice.promptprobe`，使用本机默认 Debug 签名，不读发布密钥。它链接真实 `OverlayController`、`PromptBarTouchLease` 和 `GameAdvanceAccessibilityService.TapAsync`。不重写产品的手势派发、前台检查或窗口标志。

`cn.pgrvoice.touchsink` 是另一 UID 的下层 Activity，记录原生 `DispatchTouchEvent` 中的 DOWN/UP、坐标、obscured flags。只有它实际新增 UP 才能证明点击到达；单凭 `TapAsync.Completed` 不算穿透成功。该夹具验证模拟下层应用，不代表真实战双或 ARM64 实测。

## 构建

在本机 PowerShell 运行 `./build.ps1 -Variant baseline`、`./build.ps1 -Variant fixed`、`./build.ps1 -Variant sink`。各次产物、SHA、实际编译输入 SHA 清单、日志写入不同的 `Artifacts/<variant>-<时间>`；可用 `-OutputDirectory` 明确目录。不要同时构建 baseline 与 fixed，它们使用同一测试项目的中间目录；不与产品/core 输出目录混用。

`baseline` 替换五个文件为 `25226cb45f20bf99647c7d40f08e84ffc40a89c5` 的冻结内容：`OverlayController.PromptBar.cs`、`OverlayController.BranchEntered.cs`、`GameAdvanceAccessibilityService.cs`、`PromptBarTouchLease.cs`、`AppSession.PromptBars.cs`。其余产品源码仍链接工作目录，实际输入逐文件留 SHA。冻结文本仅统一为 LF，内容来自 `git show`。`fixed` 链接当前工作源码，并接线新增的 `PromptWindowIdsBeforeTap`，与真实 AppSession 一致；baseline 不调用旧版不存在的入口。准确对照须核五文件外的输入 SHA 是否相同。早期 baseline-v3 构建时第五文件尚未改动，因此当时仅替换四个文件，其准确范围见当次输入清单。

## 安装与启用（只由设备操作者执行）

示例设备是独立 AVD `emulator-5584`、ADB 服务端口 `15047`，请先确认自己的目标，不能把这些命令指向用户真实设备。

```powershell
$adb = 'C:/AndroidVM/sdk/platform-tools/adb.exe'
$deviceArgs = @('-P','15047','-s','emulator-5584')
& $adb @deviceArgs install -r '<测试probe APK绝对路径>'
& $adb @deviceArgs install -r '<测试sink APK绝对路径>'
& $adb @deviceArgs shell appops set cn.pgrvoice.promptprobe SYSTEM_ALERT_WINDOW allow
# 先保存设备原值；这里示例是全新专用AVD，不能覆盖他人已有服务列表。
& $adb @deviceArgs shell settings put secure enabled_accessibility_services 'cn.pgrvoice.promptprobe/cn.pgrvoice.player.GameAdvanceAccessibilityService'
& $adb @deviceArgs shell settings put secure accessibility_enabled 1
& $adb @deviceArgs shell am start -n 'cn.pgrvoice.touchsink/.TouchActivity'
```

不要启动 probe 内的产品 MainActivity；测试广播自行创建独立 OverlayController，主 Activity 会建立另外的 AppSession 并接管 `BeforeTap`。更换 APK 后必须确认无障碍已重新连接。

## 测试命令

```powershell
$receiver = 'cn.pgrvoice.promptprobe/cn.pgrvoice.promptprobe.ProbeReceiver'
& $adb @deviceArgs shell am broadcast --include-stopped-packages -a cn.pgrvoice.promptprobe.COMMAND -n $receiver --es command show --es kind countdown --es id countdown-1
& $adb @deviceArgs shell am broadcast -a cn.pgrvoice.promptprobe.COMMAND -n $receiver --es command tap --es id countdown-tap-1
& $adb @deviceArgs exec-out run-as cn.pgrvoice.promptprobe cat files/prompt-probe.json
& $adb @deviceArgs exec-out run-as cn.pgrvoice.touchsink cat files/touch-sink.json
```

`command=show` 支持 `kind=countdown`、`ending`、`order`、`explore`、`selected`、`both`、`explore-both`、`top-hint`。`both` 和 `explore-both` 在可操作条上叠加真实只读已选条；只读条按产品规则约三秒收起，因此显示后应及时点击。`top-hint` 走真实 `ShowTopHint`。

`command=tap` 默认先按当前下层显示尺寸/旋转 `BeginSession`，再调用产品 `TapAsync`；默认点击当前提示窗口中心，`selected/top-hint` 自动使用只读窗口中心。可指定 `--ef x 400 --ef y 80`，或 `--es window selected`。`--ez background true` 从后台线程调用同一产品入口。`--ez arm false` 复用上一 token，用于旋转/切出后旧会话不能继续的检查。`--ez cancelImmediately true` 在真实 `TapAsync` 返回后立即取消；`--ei cancelAfterMs 20` 在给定毫秒后取消。

其他命令：`arm` 单独建 token；`hold` 获取真实租约；`release` 归还最早租约；`cancel` 取消真实服务会话；`capture --ez hidden true/false` 走真实捕获隐藏；`clear` 清提示及主控制窗。可以 `hold → show explore → show both → release` 检查替换窗口是否继承当前不可触摸状态，多次 hold 可查重入。默认三秒租约超时仍生效。

`tap --ei blockMainMs 700` 是仅夹具提供的主线程阻塞故障注入：真实 TapAsync 返回后1ms排入主线程阻塞（上限2000ms），保留开始/结束时间，验证准备帧超时后的迟到回调不能派发点击。它不会进入产品构建。必须从时间戳核实阻塞发生于派发准备期；如果手势先完成则这次不能算超时覆盖。

`pause` 仅切换夹具模型的 Paused 并刷新真实提示视图，用来查暂停键文本与布局；它不证明真实 AppSession、通知栏、锁屏或耳机状态同步。人工点按恢复应读取回执中的按钮矩形，再由操作者实际点按；如用 adb input 模拟人工点击，必须标为人工按钮测试，不能冒充产品自动点击。

## 证据判读

`prompt-probe.json` 包含运行 API、density、fontScale、UID、服务 availability、每次窗口 flags/alpha/visibility/frame、全部 TextView 文本与可见矩形、按钮坐标、手势结果、下层回传 UP。`tap-inflight` 尽力在20ms取样，不能保证覆盖60ms手势；应另留 `dumpsys window` 和截图作为设备证据。`tap-restored` 在完成后250ms记录恢复和 UP 差值。

`sink` 文件是实际触摸计数主证据；广播回传可能稍晚，不能因 `tap-result` 当下差值为0即下结论。每次用唯一 `id`，同时保存两应用回执和设备日志。截图/窗口观察、基线失败、夹具构建失败都应保留。最终交付不包含这两个测试 APK 的测试入口；产品版本、签名和 ARM64 打包由维护主线独立处理。

增强诊断版同时读取真实无障碍服务 `current.Windows`，记录每个系统窗口的 id/layer/type/focused/active/rootPackage/rootVisible/bounds。三个本应用 View 的 `accessibilityWindowId` 取自 `CreateAccessibilityNodeInfo().WindowId`（.NET 未公开 View 的同名 Java 方法），用于与系统窗口精确对应，不改变窗口或产品判断。Probe 与 sink 文件都先完整写到同目录 `.tmp` 再原子替换，读取者获得完整旧版或新版。探针重装/重启会重置回传计数，所以最终到达差值必须取同一 sink 会话 `touch-sink.json` 的实际前后 UP，不能把探针初始0与sink累积数相减。

从 fixed-v5 起，额外系统窗口 Roots 和 availability 诊断仅在没有手势进行的 `state`、`state-settled`、`hold-settled` 中读取；手势过程、回调及 sink-up 不再重复查询。这避免测试自身同步 Binder 查询遮挡窗口时阻塞主线程。每事件记录 `diagnosticSkippedDuringTap`，顶层 availability 是最近静态取样/arm结果，并非每次事件的实时值。事件历史最多80条，操作者应逐场景保存原始回执，不能用覆盖后的文件代替历史证据。

## 真实听书页面布局检查（fixed-v5）

自动点击矩阵完成后，可广播 `command=listening-setup`。它在测试 APK 的独立私有目录，通过真实 `AppSession.Packages.ImportAsync` 导入原有 `Tests/ListeningDefaultBranch/PlayAlong.pack.json`（原文嵌入、内存封装 ZIP，无音频生成），再走真实 `Listening.Open`、Manual 策略、`SeekListeningItem` 定位第一个三选项菜单、`Play` 启动15秒倒计时并立即 `ToggleCountdownPause`。最后用 `page=Listening` 启动真实 MainActivity。只在测试应用中跳过首次引导并设朝向auto；不读写正式安装和正式章节库。

`command=listening-state` 将真实会话标记、主按钮状态、倒计时和整棵实际页面的文本/按钮可见矩形、ScrollView滚动位置写入独立 `files/listening-probe.json`（同样原子替换）。`command=listening-page --es page Chapters/Listening` 用真实Activity意图切页；`command=listening-reopen` 走真实同章 `Open`，核回到同选项是否清倒计时并等待选择；`command=listening-toggle` 走真实主按钮API。人工暂停键和主按钮验证仍可通过页面中的真实按钮坐标操作。可旋转后再次取 `listening-state`，不伪造布局渲染。

`command=listening-service` 用于后续系统 `media_session dispatch` / `play_pause` 媒体键验证。先完成 `listening-setup`，等待真实 MainActivity 已恢复到前台（`listening-probe.json` 的 `activityResumed=true`），再发送此命令。它只调用产品的 `ListeningForegroundService.EnsureStarted` 建立真实前台服务和 MediaSession，并在产品就绪回调中保存 `listening-service-ready`，500ms 后保存 `listening-service-settled`；未在前台时记录 `listening-service-not-ready`，启动异常记录 `listening-service-error`。命令本身不切换暂停状态，不播放音频。

```powershell
& $adb @deviceArgs shell am broadcast -a cn.pgrvoice.promptprobe.COMMAND -n $receiver --es command listening-service
& $adb @deviceArgs exec-out run-as cn.pgrvoice.promptprobe cat files/listening-probe.json
```

这是测试主动建立服务的步骤：原 `listening-setup` 直接在选择点启动并暂停倒计时，不会自然播放音频，也不会自动建立媒体服务。服务就绪回执只证明启动回调已执行；后续仍须另存系统 MediaSession 状态，并在实际派发媒体键前后核对真实倒计时、主按钮和卡片状态。不能把此步骤记作“自然音频播到分支”，也不能把系统派发模拟等同于实体耳机或锁屏操作已实测。

本轮实际无音频夹具没有成为全局媒体键目标，`dispatch` 与 `input keyevent 85` 未切换状态，保留该证据。`verify_listening_device.py` 改用 `cmd media_session monitor PgrVoice.Listening` 向指定系统会话派发 play/pause，保存控制器输出和前后真实状态；全局按键是否到达单列，不冒充耳机覆盖。start 还核初次与返回相同选项时 PauseToggle 完整可见，HOME 后确认 Activity 已在后台；finish 必须实际经过超过 300 秒，验证剩余秒数、恢复及重开等待。每次 start 使用新目录，不能覆盖历史失败。

真实 AppSession 初始化会按产品行为接管全局自动点击hook，所以听书布局阶段应放在提示窗点击矩阵之后；若要回到独立提示窗矩阵，先结束并重新启动测试应用进程，不在两种会话之间偷偷切换hook。听书夹具不证明通知栏/锁屏/耳机全流程；只能按实际执行项记为已测。
