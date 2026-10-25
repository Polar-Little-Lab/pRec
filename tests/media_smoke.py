"""Real FFmpeg media pipeline integration tests. Does not test Windows capture/WASAPI."""
import json, pathlib, subprocess, tempfile

def run(*args):
    r = subprocess.run(['ffmpeg', '-hide_banner', '-loglevel', 'error', '-y', *map(str,args)], capture_output=True, text=True)
    if r.returncode: raise AssertionError(r.stderr)

def probe(path):
    return json.loads(subprocess.check_output(['ffprobe','-v','error','-show_streams','-show_format','-of','json',str(path)]))

with tempfile.TemporaryDirectory() as root:
    d = pathlib.Path(root)
    run('-f','lavfi','-i','testsrc2=size=320x240:rate=30','-t','1.2','-an','-c:v','libx264','-preset','veryfast','-crf','22','-pix_fmt','yuv420p','-r','30',d/'raw.mkv')
    run('-f','lavfi','-i','sine=frequency=440:sample_rate=48000','-t','1.2','-ac','2',d/'pc.wav')
    run('-f','lavfi','-i','anullsrc=r=48000:cl=stereo','-t','1.2',d/'mic.wav')
    for count in (0,1,2):
        segments = []
        for n in range(3):
            out = d/f'seg-{count}-{n}.mp4'
            args = ['-i',d/'raw.mkv']
            for path in ['pc.wav','mic.wav'][:count]: args += ['-i',d/path]
            args += ['-map','0:v:0','-c:v','copy']
            if count:
                filt = '[1:a]apad[a]' if count == 1 else '[1:a][2:a]amix=inputs=2:duration=longest:normalize=1,apad[a]'
                args += ['-filter_complex',filt,'-map','[a]','-c:a','aac','-b:a','192k','-ar','48000','-ac','2','-shortest']
            run(*args,'-movflags','+faststart',out)
            segments.append(out)
        listing = d/'concat.txt'
        listing.write_text(''.join(f"file '{p.name}'\n" for p in segments))
        final = d/f'final-{count}.mp4'
        run('-f','concat','-safe','1','-i',listing,'-c','copy','-movflags','+faststart',final)
        data = probe(final)
        streams = data['streams']
        assert sum(s['codec_type']=='video' for s in streams)==1
        assert sum(s['codec_type']=='audio' for s in streams)==int(count>0)
        assert streams[0]['codec_name']=='h264'
        assert 3.5 <= float(data['format']['duration']) <= 4.0, data['format']['duration']
        run('-i',final,'-f','null','-')
        print(f'PASS audio_sources={count}: 3 segments, valid H.264 MP4, duration={data["format"]["duration"]}')
