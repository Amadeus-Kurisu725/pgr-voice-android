param(
    [ValidateSet('baseline','fixed','sink')][string]$Variant = 'baseline',
    [string]$OutputDirectory = ''
)
$ErrorActionPreference = 'Stop'
. 'C:/AndroidVM/environment.ps1'
$dotnet = 'C:/AndroidVM/runtime/dotnet/dotnet.exe'
$repo = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../../..'))
if (!$OutputDirectory) { $OutputDirectory = Join-Path $PSScriptRoot ('Artifacts/' + $Variant + '-' + (Get-Date -Format 'yyyyMMdd-HHmmss')) }
$OutputDirectory = [IO.Path]::GetFullPath($OutputDirectory)
New-Item -ItemType Directory -Path $OutputDirectory -Force | Out-Null
if (Test-Path -LiteralPath (Join-Path $OutputDirectory 'build.json')) { throw '已有构建回执，不覆盖；请给新的输出目录。' }
$project = Join-Path $PSScriptRoot $(if($Variant -eq 'sink'){'Sink/TouchSink.csproj'}else{'Probe/PromptWindowProbe.csproj'})
$baseline = if($Variant -eq 'baseline'){'true'}else{'false'}
$arguments = @("-p:Baseline=$baseline", '-p:AndroidSdkDirectory=C:/AndroidVM/sdk', '-p:JavaSdkDirectory=C:/AndroidVM/runtime/jdk')
function Read-Inputs([string]$Path) {
    $raw = & $dotnet msbuild $Path @arguments '-getItem:Compile,AndroidResource,AndroidAsset,EmbeddedResource' -nologo
    if($LASTEXITCODE -ne 0) { throw '未能枚举实际构建输入。' }
    $items = (($raw -join "`n") | ConvertFrom-Json).Items
    foreach($group in @('Compile','AndroidResource','AndroidAsset','EmbeddedResource')) {
        foreach($item in $items.$group) {
            if(Test-Path -LiteralPath $item.FullPath -PathType Leaf) {
                [pscustomobject]@{ type=$group; path=[IO.Path]::GetFullPath($item.FullPath); sha256=(Get-FileHash -Algorithm SHA256 -LiteralPath $item.FullPath).Hash.ToLowerInvariant() }
            }
        }
    }
}
$inputs = @(Read-Inputs $project)
if($Variant -ne 'sink') { $inputs += @(Read-Inputs (Join-Path $PSScriptRoot 'Core/PromptWindowCore.csproj')) }
$projectInputs = @($project, $PSCommandPath)
if($Variant -ne 'sink') { $projectInputs += (Join-Path $PSScriptRoot 'Core/PromptWindowCore.csproj'); $projectInputs += (Join-Path $repo 'android/Properties/AndroidManifest.xml') }
foreach($path in $projectInputs) { $inputs += [pscustomobject]@{type='Project';path=[IO.Path]::GetFullPath($path);sha256=(Get-FileHash -Algorithm SHA256 -LiteralPath $path).Hash.ToLowerInvariant()} }
if($Variant -eq 'baseline') {
    $baselineInputs = @($inputs | Where-Object { $_.path -like '*\PromptWindowDevice\Baseline\*.cs.txt' })
    if($baselineInputs.Count -ne 5) { throw '基线必须且只能编入五个冻结源，拒绝错标当前源码为基线。' }
}
$inputs | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $OutputDirectory 'inputs-before.json') -Encoding utf8
$log = Join-Path $OutputDirectory 'build.log'
& $dotnet build $project -c Debug @arguments --nologo -v:minimal *> $log
$exit = $LASTEXITCODE
Get-Content -LiteralPath $log -Tail 18
if($exit -ne 0) { throw "构建失败，保留 $log" }
$changed = @($inputs | Where-Object { (Get-FileHash -Algorithm SHA256 -LiteralPath $_.path).Hash.ToLowerInvariant() -ne $_.sha256 })
if($changed.Count -ne 0) { $changed | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $OutputDirectory 'inputs-changed.json'); throw '构建中输入变化，不能把本次产物当作冻结源版本。' }
$apkName = if($Variant -eq 'sink'){'cn.pgrvoice.touchsink-Signed.apk'}else{'cn.pgrvoice.promptprobe-Signed.apk'}
$apk = Join-Path (Split-Path $project) ('bin/Debug/net10.0-android/android-x64/' + $apkName)
$destination = Join-Path $OutputDirectory $apkName
Copy-Item -LiteralPath $apk -Destination $destination
[pscustomobject]@{
    variant=$Variant; utc=[DateTime]::UtcNow.ToString('o'); baselineCommit='25226cb45f20bf99647c7d40f08e84ffc40a89c5';
    repositoryHead=(& git -C $repo rev-parse HEAD); fixture=$PSScriptRoot; apk=$destination;
    sha256=(Get-FileHash -Algorithm SHA256 -LiteralPath $destination).Hash.ToLowerInvariant(); inputCount=$inputs.Count;
    scope='独立测试APK；debug签名；不是产品发布；仅编译通过不等于设备验证通过'
} | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $OutputDirectory 'build.json') -Encoding utf8
Get-Content -LiteralPath (Join-Path $OutputDirectory 'build.json')
