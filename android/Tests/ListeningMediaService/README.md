# 听书媒体服务暂停回归

直接编译当前 `ListeningSession.cs`、`ListeningForegroundService.cs`、`ListeningMediaCallback`，使用已有 `ListeningDefaultBranch/PlayAlong.pack.json` 合成老夹具。测试目录外的正式配音包、音频与用户设置不参与。

```powershell
& 'C:/AndroidVM/runtime/dotnet/dotnet.exe' run --project android/Tests/ListeningMediaService/ListeningMediaServiceRegression.csproj -- '<结果JSON绝对路径>'
```

覆盖：

- 卡片暂停到 13 秒后经过六分钟，服务与媒体控制仍存在，耳机播放从 13 秒继续。
- 通知中键、MediaSession 暂停/播放、主按钮与卡片的倒计时状态同步；重复暂停保持原状态。
- 倒计时运行时持有唤醒锁，暂停释放，继续重新取得。
- 先前排定的空闲退出回调即使迟到，也不能销毁新倒计时。
- 普通空闲仍在五分钟退出，主动停止仍立即清理倒计时和服务。

Android API、通知对象、音频和虚拟时钟为受控替身。五分钟回调会在虚拟时钟中实际执行；未操作真实系统休眠、蓝牙耳机、锁屏界面或 ARM64 设备。该回归不能代替这些设备验证。

2026-10-11 修复前五项中三项失败（六分钟保留、唤醒锁、迟到退出回调），修复后五项通过。生产修复仅改变前台服务的空闲退出资格及唤醒锁持有条件，`ServiceStopped` 的强制清理和 15 秒倒计时规则保持。
