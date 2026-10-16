"""隔离运行真实生产代码的音频模式回归；--output 必须明确给出。"""
from pathlib import Path
import argparse, hashlib, json, os, re, subprocess, sys
import xml.etree.ElementTree as ET
sys.stdout.reconfigure(encoding='utf-8')

here = Path(__file__).resolve().parent
p = argparse.ArgumentParser(description=__doc__)
p.add_argument('--source-root', type=Path, default=here.parents[2])
p.add_argument('--output', type=Path, required=True)
p.add_argument('--dotnet', default='C:/AndroidVM/runtime/dotnet/dotnet.exe')
a = p.parse_args()
root, out = a.source_root.resolve(), a.output.resolve()
if out == root or root in out.parents:
    raise SystemExit('报告目录必须位于生产源码以外。')
generated = out / 'generated'
generated.mkdir(parents=True, exist_ok=True)
def sha(path): return hashlib.sha256(path.read_bytes()).hexdigest()

def method(file, name):
    text = file.read_text('utf-8-sig')
    found = re.search(r'(?m)^    (?:public |private |internal )?(?:void|bool) '+re.escape(name)+r'\(', text)
    if not found: raise ValueError(f'找不到方法 {file}:{name}')
    start, i = found.start(), text.index('{', found.end())
    depth, state = 0, 'code'
    while i < len(text):
        c, pair = text[i], text[i:i+2]
        if state == 'line':
            if c == '\n': state = 'code'
        elif state == 'comment':
            if pair == '*/': state = 'code'; i += 1
        elif state == 'string':
            if c == '\\': i += 1
            elif c == '"': state = 'code'
        else:
            if pair == '//': state = 'line'; i += 1
            elif pair == '/*': state = 'comment'; i += 1
            elif c == '"': state = 'string'
            elif c == '{': depth += 1
            elif c == '}':
                depth -= 1
                if depth == 0: return text[start:i+1]
        i += 1
    raise ValueError('未闭合方法：'+name)

app = root / 'android/AppSession.cs'
names = ['ShowOverlay','PreviewNode','Command','ConfirmCurrent','PrepareCapture','RequestOcr','PrepareListening','StopGame']
extracted = {name:method(app,name) for name in names}
apptext = app.read_text('utf-8-sig')
assert 'Listening=new(context,Packages,PrepareListening,Diagnostics,' in apptext, '听书beforePlay生产接线已变化，请更新测试'
(generated/'ProductionConnection.cs').write_text('using PgrVoice;\nusing PgrVoice.AndroidApp.Platform;\nnamespace PgrVoice.AndroidApp;\npublic sealed partial class AppSession\n{\n'+'\n'.join(extracted.values())+'\n}\n',encoding='utf-8')
entry_specs = [('AppSession.cs',n) for n in ['LoadPack','PreviewNode','Command','ConfirmCurrent','PrepareCapture','RequestOcr','ShowOverlay']] + [
 ('AppSession.Autoplay.cs','StartAutoPlayback'),('AppSession.ClickFollow.cs','StartClickFollow'),
 ('AppSession.OcrReview.cs','PrepareRecognizedTextCandidates'),('AppSession.Onboarding.cs','PauseForOnboarding'),
 ('AppSession.OverlayBrowse.cs','OnOverlayBrowseRequested')]
bindings=[]
for file,name in entry_specs:
    body=method(root/'android'/file,name)
    bindings.append({'file':file,'method':name,'usesPauseForGame':bool(re.search(r'Listening\.PauseForGame\(\)',body)), 'sourceSha256':sha(root/'android'/file)})

core = root/'core'
corexml = ET.parse(core/'PgrVoice.Core.csproj').getroot()
props=ET.Element('Project'); items=ET.SubElement(props,'ItemGroup')
ET.SubElement(items,'Compile',Include=(core/'**/*.cs').as_posix(),Exclude=';'.join((core/s).as_posix() for s in ['bin/**','obj/**']))
inputs=[]
for item in corexml.iter():
    if item.tag not in {'Compile','EmbeddedResource'}: continue
    file=(core/item.attrib['Include']).resolve()
    attrs={'Include':file.as_posix()}
    if 'LogicalName' in item.attrib: attrs['LogicalName']=item.attrib['LogicalName']
    ET.SubElement(items,item.tag,attrs); inputs.append(file)
ET.indent(props)
ET.ElementTree(props).write(generated/'CoreInputs.props',encoding='utf-8',xml_declaration=True)
entry=next(e for e in json.loads((core/'NativeVoiceCatalog.json').read_text('utf-8-sig'))['entries']
           if e.get('status')=='confirmed' and e.get('pathId')=='' and e.get('nativeDurationMs',0)>0)
node={key:entry[key] for key in ['sectionId','pathId','speaker','text','rawText']}
node.update(id=entry['nodeId'],kind='line',audio='sample.wav',nextId='audit-end')
fixture={'schemaVersion':3,'id':entry['packId'],'title':'原配身份测试，仅临时元数据',
 'chapters':[{'id':'audit-chapter','title':'测试章','sections':[{'id':entry['sectionId'],'title':'测试节','startId':'audit-other'}]}],
 'nodes':[{'id':'audit-other','kind':'line','sectionId':entry['sectionId'],'pathId':'','speaker':'测试','text':'其他位置。','audio':'sample.wav','nextId':entry['nodeId']},node,
          {'id':'audit-end','kind':'end','sectionId':entry['sectionId'],'pathId':''}]}
(generated/'NativeFixture.json').write_text(json.dumps(fixture,ensure_ascii=False,indent=2),encoding='utf-8')
inputs += list(core.glob('*.cs')) + [root/'android'/n for n in ['ListeningSession.cs','AppSession.Playback.cs','AutoPlaybackCycle.cs','Platform/ListeningForegroundService.cs','Contracts/IChapterStorage.cs','Contracts/IAudioOutput.cs']]
inputs += [root/'android'/file for file,_ in entry_specs]
evidence={'sourceRoot':str(root),'productionMethods':extracted,'entryBindings':bindings,
          'constructorBindingVerified':True,'inputs':[{'path':str(f),'sha256':sha(f)} for f in sorted(set(inputs))],
          'nativeIdentity':{'packId':entry['packId'],'nodeId':entry['nodeId']},
          'scope':'完整生产听书/前台服务/Playback/核心；六入口及PrepareListening/StopGame原样抽取，Android/UI/audio受控替身，其余6入口查源码接线。'}
(generated/'source-evidence.json').write_text(json.dumps(evidence,ensure_ascii=False,indent=2),encoding='utf-8')
temp=out/'temp'; temp.mkdir(exist_ok=True)
env=dict(os.environ,TEMP=str(temp),TMP=str(temp),DOTNET_CLI_TELEMETRY_OPTOUT='1',DOTNET_NOLOGO='1')
if Path('C:/AndroidVM/nuget/packages').exists(): env['NUGET_PACKAGES']='C:/AndroidVM/nuget/packages'
command=[a.dotnet,'run','--project',str(here/'AudioModeRegression.csproj'),'-c','Release',
 f'-p:ProductionRoot={root.as_posix()}',f'-p:AuditGenerated={generated.as_posix()}',
 f'-p:BaseIntermediateOutputPath={(out/"obj").as_posix()}/',f'-p:OutputPath={(out/"bin").as_posix()}/',
 '--',str(out/'results.json')]
(out/'invocation.json').write_text(json.dumps(command,ensure_ascii=False,indent=2),encoding='utf-8')
with (out/'run.log').open('w',encoding='utf-8') as log:
    completed=subprocess.run(command,cwd=root,env=env,stdout=log,stderr=subprocess.STDOUT)
print((out/'run.log').read_text('utf-8'),end='')
changed=[str(f) for f in set(inputs) if next(x['sha256'] for x in evidence['inputs'] if x['path']==str(f))!=sha(f)]
(out/'run-receipt.json').write_text(json.dumps({'exitCode':completed.returncode,'productionInputsChangedDuringRun':changed,'report':str(out/'results.json')},ensure_ascii=False,indent=2),encoding='utf-8')
sys.exit(completed.returncode if not changed else 2)
