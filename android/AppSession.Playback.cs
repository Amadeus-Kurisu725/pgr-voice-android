using Android.OS;
using PgrVoice.AndroidApp.Platform;
using PgrVoice.Listening;

namespace PgrVoice.AndroidApp;

public sealed partial class AppSession
{
    string? playingSpeaker;
    bool playingNative;
    public const string NativeVoiceStatus="正在听游戏原配";
    /// <summary>边玩边听时这一句由游戏播原声、配音包静音占位（开关打开、本章没选“用配音包”、且是清单核过的原配台词）。</summary>
    public bool IsNativeVoiceLine(PlaybackEngine? owner,Node? node)=>
        owner!=null&&Settings.NativeVoiceMute&&!Settings.NativeVoiceDubPacks.Contains(owner.Pack.Id)&&NativeVoiceCatalog.Shared?.FindLineVoice(owner.Pack,node)!=null;
    public int NativeVoiceCount(string packId)=>NativeVoiceCatalog.Shared?.CountConfiguredVoices(packId)??0;
    public void SetNativeVoiceMute(bool value){Settings.NativeVoiceMute=value;SaveSettings();Notify();}
    public void SetNativeVoiceForPack(string packId,bool useNative)
    {if(useNative)Settings.NativeVoiceDubPacks.Remove(packId);else Settings.NativeVoiceDubPacks.Add(packId);SaveSettings();Notify();}
    void PlayNode(PlaybackEngine owner,Node? node)
    {
        if(!ReferenceEquals(owner,Engine)||node==null)return;
        long request=++playGeneration;
        if(autoPlay.Running&&IsSilentPunctuation(node)){StartAutoSilentPause(owner,node,request);return;}
        string? path=owner.Pack.ResolveAudio(node);
        if((path==null||!File.Exists(path))&&PassesWithoutVoice(node))
        {
            // 0.4.7（J）没有可用音频（待配音 / 无配音）：自动播放按字数停留后继续；手动 / 点按跟随只显示字幕，一次“下一句”即越过。
            audio.Stop();
            if(autoPlay.Running){StartAutoSilentPause(owner,node,request,PendingVoice.DwellMilliseconds(node.Text));return;}
            Status=PendingVoice.Tag(node)+(string.IsNullOrWhiteSpace(node.Speaker)?"":node.Speaker+" · ")+UiText.PendingVoiceManual;Notify();return;
        }
        if(path==null||!File.Exists(path)){audio.Stop();if(autoPlay.Running)PauseAutoPlayback("这一句没有可用配音，自动播放已暂停。");else{Status=owner.Pack.AudioNotice(node);Notify();}return;}
        VoiceForegroundService.EnsureStarted(context,()=>
        {
            if(request!=playGeneration||!ReferenceEquals(Engine,owner))return;
            if(autoPlay.Running&&!CheckAutoPlaybackEnvironment())return;
            if(autoPlay.Running && (autoPlay.Phase!=AutoPlaybackPhase.Playing||autoPlay.NodeId!=node.Id||!AutoLinePermitted(owner)))
            {PauseAutoPlayback("当前播放不属于本次已确认的路线，自动播放已暂停。");return;}
            playingSpeaker=node.Speaker;
            bool native=IsNativeVoiceLine(owner,node);
            playingNative=native;
            audio.SetVolume(native?0:SpeakerVolume.Apply(Settings.Volume,Settings.SpeakerVolumes,playingSpeaker));
            long ticket=native?audio.PlayTaggedWithoutFocus(path):audio.PlayTagged(path);
            if(autoPlay.Running&&!autoPlay.BindAudio(autoPlay.Epoch,node.Id,ticket,SystemClock.ElapsedRealtime()))PauseAutoPlayback("播放请求已变化或等待超时，请重新确认自动播放位置。");
        });
        if(request!=playGeneration)return;
        string action=IsNativeVoiceLine(owner,node)?NativeVoiceStatus:"播放配音";
        Status=string.IsNullOrWhiteSpace(node.Speaker)?action:node.Speaker+" · "+action;
    }
}
