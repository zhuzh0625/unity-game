"""生成原创合成音效与循环配乐，不依赖外部素材。"""
import math, wave, struct
from pathlib import Path
OUT=Path(__file__).resolve().parents[1]/'Assets/CloudSprout/Audio'
OUT.mkdir(parents=True,exist_ok=True)
SR=22050
def save(name,data):
 with wave.open(str(OUT/(name+'.wav')),'w') as w:
  w.setnchannels(1);w.setsampwidth(2);w.setframerate(SR);w.writeframes(b''.join(struct.pack('<h',int(max(-1,min(1,x)) * 27000)) for x in data))
def chirp(start,end,dur,amp=.35):
 return [amp*(1-i/(SR*dur))**1.8*(math.sin(2*math.pi*(start*(i/SR)+(end-start)*(i/SR)**2/(2*dur)))) for i in range(int(SR*dur))]
save('jump',chirp(280,720,.19));save('coin',chirp(1050,1650,.23));save('hurt',chirp(240,65,.35));save('land',chirp(100,40,.10,.25))
def melody(notes,step):
 data=[]
 for n in notes:
  f=440*2**((n-69)/12)
  for i in range(int(SR*step)):
   t=i/SR;e=min(t/.015,1)*math.exp(-t*7)
   data.append(.28*e*(math.sin(2*math.pi*f*t)+.2*math.sin(4*math.pi*f*t)))
 return data
save('checkpoint',melody([72,76,79,84],.13));save('win',melody([72,76,79,84,79,84,88,84],.18))
notes=[72,0,76,79,0,76,74,0,72,0,67,0,69,72,0,0,74,0,77,81,0,77,76,0,74,0,71,0,72,0,0,0]
step=.23;data=[0.0]*int(SR*step*len(notes)*2)
for j in range(len(notes)*2):
 n=notes[j%len(notes)];start=int(j*step*SR)
 if n:
  f=440*2**((n-69)/12)
  for i in range(min(int(SR*.65),len(data)-start)):
   t=i/SR;data[start+i]+=.17*min(t/.012,1)*math.exp(-t*6)*(math.sin(2*math.pi*f*t)+.18*math.sin(4*math.pi*f*t))
 if j%4==0:
  n=[48,53,55,48][(j//8)%4];f=440*2**((n-69)/12)
  for i in range(min(int(SR*.8),len(data)-start)):
   t=i/SR;data[start+i]+=.10*min(t/.02,1)*math.exp(-t*4)*math.sin(2*math.pi*f*t)
save('music',data)
print('Created 7 original audio clips')
