using AVAMoonPatrol.Audio;

namespace AVAMoonPatrol.Engine;

/// <summary>
/// Plays the game's sounds through OpenAL Soft, which mixes as many at once as the game needs. The
/// title tune and the short course tunes play on the music channel, the driving music on its own loop.
/// In a virtual machine a larger mixing buffer keeps the sound from breaking up.
/// </summary>
public sealed class SoundManager : IDisposable
{
    private readonly IAudioEngine _engine;
    private readonly string _folder;

    public SoundManager(bool silent = false)
    {
        _folder = Path.Combine(AppContext.BaseDirectory, "Sounds");
        _engine = silent
            ? new NullAudioEngine("silent")
            : OpenAlAudioEngine.CreateOrSilent(AudioBuffer.PeriodFrames(AudioBuffer.Automatic, VirtualMachine.Detect()));
        _engine.SetVolumes(0.9f, 0.8f, 0.7f);
        _engine.SetAmbienceVolume(0.4f);
    }

    public bool IsAvailable => _engine.IsAvailable;

    public bool IsMuted
    {
        get => _muted;
        set
        {
            _muted = value;
            if (value) StopAll();
        }
    }
    private bool _muted;

    public void Play(Sfx sfx)
    {
        if (_muted) return;
        switch (sfx)
        {
            case Sfx.TitleTune: _engine.PlayMusic(Path.Combine(_folder, "TitleTune.wav"), loop: true); return;
            case Sfx.CourseStart: _engine.PlayMusic(Path.Combine(_folder, "CourseStart.wav"), loop: false); return;
            case Sfx.GameOver: _engine.PlayMusic(Path.Combine(_folder, "GameOver.wav"), loop: false); return;
            // The driving music loops on its own channel while a buggy is on the road.
            case Sfx.MusicStart: _engine.PlayAmbience(Path.Combine(_folder, "Drive.wav")); return;
            case Sfx.MusicStop: _engine.StopAmbience(); return;
        }
        _engine.PlayEffect(Path.Combine(_folder, sfx + ".wav"), sfx switch
        {
            Sfx.Fire => 0.35f,
            Sfx.Land => 0.4f,
            Sfx.Jump => 0.6f,
            _ => 1f,
        });
    }

    public void StopAll()
    {
        _engine.StopAmbience();
        _engine.StopMusic();
    }

    public void Dispose() => _engine.Dispose();
}
